#!/usr/bin/env python3
"""Job-scoped localhost TLS trust for a disposable, explicitly approved macOS CI test."""
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import plistlib
import secrets
import shlex
import shutil
import ssl
import subprocess
import sys


def run(*args, required=True):
    result = subprocess.run(args, text=True, capture_output=True, timeout=60)
    if required and result.returncode:
        # Never echo arguments: an ephemeral keychain password may be among them.
        raise RuntimeError(f'{Path(args[0]).name} failed with exit code {result.returncode}: {result.stderr.strip()}')
    return result


def workspace():
    if (sys.platform != 'darwin' or os.environ.get('RUNNER_ENVIRONMENT') != 'github-hosted'
            or os.environ.get('GITHUB_REPOSITORY', '').lower() != 'avantipoint/avantipoint.packages'):
        raise RuntimeError('TLS fixture is restricted to this repository on disposable GitHub-hosted macOS.')
    return Path(os.environ['RUNNER_TEMP']) / 'avp-native-swift-tls'


def save_state(path, state):
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(state))
    os.chmod(temporary, 0o600)
    temporary.replace(path)


def export_admin_trust(path):
    result = run('security', 'trust-settings-export', '-d', str(path), required=False)
    if result.returncode:
        if 'No Trust Settings were found' not in result.stderr:
            raise RuntimeError('Could not snapshot the existing admin trust settings: ' + result.stderr.strip())
        # Apple's file-only mode creates a valid empty external representation;
        # it does not modify a trust domain or install a certificate.
        run('security', 'add-trusted-cert', '-o', str(path))
    return plistlib.loads(path.read_bytes())


def revoke_fixture_trust(root):
    cert = root / 'localhost.crt'
    before = plistlib.loads((root / 'admin-trust-before.plist').read_bytes())
    if not isinstance(before, dict) or not isinstance(before.get('trustList', {}), dict):
        raise RuntimeError('Unexpected original trust settings schema.')
    fingerprint = hashlib.sha1(ssl.PEM_cert_to_DER_cert(cert.read_text())).hexdigest().upper()
    if any(key.upper() == fingerprint for key in before.get('trustList', {})):
        raise RuntimeError('Fixture certificate unexpectedly existed before this job.')
    # macOS runner trust-removal/import APIs can hang. SetTrustSettings can revoke
    # this unique leaf directly without editing authorization policy or other roots.
    run('sudo', '-n', 'security', 'add-trusted-cert', '-d', '-r', 'deny',
        '-p', 'ssl', '-s', '127.0.0.1', str(cert))
    after = copy.deepcopy(export_admin_trust(root / 'admin-trust-after.plist'))
    if not isinstance(after, dict) or not isinstance(after.get('trustList', {}), dict):
        raise RuntimeError('Unexpected current trust settings schema.')
    records = after.get('trustList', {})
    matching = [key for key in records if key.upper() == fingerprint]
    if len(matching) != 1:
        raise RuntimeError('Expected exactly one fixture certificate trust record.')
    record = records.pop(matching[0])
    settings = record.get('trustSettings', []) if isinstance(record, dict) else []
    if not isinstance(settings, list) or not settings or any(not isinstance(setting, dict)
            or setting.get('kSecTrustSettingsResult') != 3 for setting in settings):
        raise RuntimeError('Fixture trust was not explicitly revoked.')
    if after != before:
        raise RuntimeError('Pre-existing trust settings changed.')
    if run('security', 'verify-cert', '-c', str(cert), '-p', 'ssl', '-s', '127.0.0.1', required=False).returncode == 0:
        raise RuntimeError('Fixture certificate remains trusted.')


def prepare(approved):
    if not approved:
        raise RuntimeError('Temporary localhost certificate trust must be explicitly approved.')
    root = workspace()
    root.mkdir(mode=0o700)
    state_path = root / 'state.json'
    state = {'search_list': shlex.split(run('security', 'list-keychains', '-d', 'user').stdout),
             'trust_attempted': False, 'trust_added': False}
    save_state(state_path, state)
    export_admin_trust(root / 'admin-trust-before.plist')
    cert, key, pfx = (root / name for name in ('localhost.crt', 'localhost.key', 'localhost.pfx'))
    keychain = root / 'localhost.keychain-db'
    config = root / 'openssl.cnf'
    config.write_text('''[req]
prompt = no
distinguished_name = subject
x509_extensions = extensions
[subject]
CN = AvantiPoint disposable localhost test
[extensions]
basicConstraints = critical, CA:FALSE
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = IP:127.0.0.1,DNS:localhost
''')
    old_umask = os.umask(0o077)
    try:
        run('openssl', 'req', '-x509', '-newkey', 'rsa:3072', '-sha256', '-nodes', '-days', '1',
            '-config', str(config), '-keyout', str(key), '-out', str(cert))
        run('openssl', 'pkcs12', '-export', '-inkey', str(key), '-in', str(cert), '-out', str(pfx), '-passout', 'pass:')
        password = secrets.token_hex(32)
        run('security', 'create-keychain', '-p', password, str(keychain))
        run('security', 'unlock-keychain', '-p', password, str(keychain))
        run('security', 'list-keychains', '-d', 'user', '-s', str(keychain), *state['search_list'])
        # This helper imports no private key. Only this unique certificate is trusted,
        # only for SSL to 127.0.0.1, in a temporary keychain. Admin trust avoids
        # an interactive per-user authorization dialog on the headless runner.
        # The .NET host separately loads the PFX into its runtime-owned temporary
        # keychain (DefaultKeySet, never PersistKeySet), disposed with the host.
        state['trust_attempted'] = True
        save_state(state_path, state)
        run('sudo', '-n', 'security', 'add-trusted-cert', '-d', '-r', 'trustRoot',
            '-p', 'ssl', '-s', '127.0.0.1', '-k', str(keychain), str(cert))
        state['trust_added'] = True
        save_state(state_path, state)
        run('security', 'verify-cert', '-c', str(cert), '-p', 'ssl', '-s', '127.0.0.1')
        swift = run('xcrun', '--find', 'swift').stdout.strip()
        with open(os.environ['GITHUB_ENV'], 'a') as environment:
            environment.write(f'AVP_SWIFT_EXECUTABLE={swift}\nAVP_SWIFT_CERTIFICATE_PATH={pfx}\n')
        print('Prepared isolated localhost TLS fixture; certificate material is never uploaded.')
    finally:
        os.umask(old_umask)


def cleanup():
    root = workspace()
    if not root.exists():
        return
    state_path = root / 'state.json'
    errors = []
    try:
        state = json.loads(state_path.read_text()) if state_path.exists() else {}
    except (OSError, ValueError):
        state = {}
        errors.append('read saved keychain state')
    cert, keychain = root / 'localhost.crt', root / 'localhost.keychain-db'
    def attempt(label, *command):
        try:
            if run(*command, required=False).returncode:
                errors.append(label)
                return False
        except (OSError, subprocess.TimeoutExpired):
            errors.append(label)
            return False
        return True
    if state.get('trust_attempted'):
        try:
            revoke_fixture_trust(root)
        except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired) as error:
            errors.append('revoke localhost trust: ' + str(error))
    if 'search_list' in state:
        if attempt('restore keychain search list', 'security', 'list-keychains', '-d', 'user', '-s', *state['search_list']):
            try:
                if shlex.split(run('security', 'list-keychains', '-d', 'user').stdout) != state['search_list']:
                    errors.append('verify restored keychain search list')
            except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired):
                errors.append('verify restored keychain search list')
    if keychain.exists():
        attempt('delete temporary keychain', 'security', 'delete-keychain', str(keychain))
    # Remove private material even if restoring trust settings failed.
    for name in ('localhost.key', 'localhost.pfx'):
        (root / name).unlink(missing_ok=True)
    if errors:
        raise RuntimeError('TLS fixture cleanup failed: ' + ', '.join(errors) + '; disposable runner teardown required.')
    shutil.rmtree(root)
    print('Revoked and verified localhost trust; pre-existing trust unchanged; keychain, private material and search list cleaned.')
    print('Only the synthetic certificate deny record remains until this disposable runner is destroyed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('prepare', 'cleanup'))
    parser.add_argument('--allow-temporary-trust', action='store_true')
    args = parser.parse_args()
    if args.action == 'prepare':
        try:
            prepare(args.allow_temporary_trust)
        except BaseException:
            cleanup()
            raise
    else:
        cleanup()
