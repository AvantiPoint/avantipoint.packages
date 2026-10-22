import json
import plistlib
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import swift_tls_runner as fixture


class SwiftTlsRunnerTests(unittest.TestCase):
    def test_prepare_requires_explicit_approval_before_any_command(self):
        with patch.object(fixture, 'run') as run:
            with self.assertRaisesRegex(RuntimeError, 'explicitly approved'):
                fixture.prepare(False)
            run.assert_not_called()

    def test_workspace_rejects_non_disposable_runners(self):
        with patch.object(fixture.sys, 'platform', 'darwin'), patch.dict(fixture.os.environ, {
            'RUNNER_ENVIRONMENT': 'self-hosted', 'GITHUB_REPOSITORY': 'AvantiPoint/avantipoint.packages'
        }):
            with self.assertRaisesRegex(RuntimeError, 'disposable'):
                fixture.workspace()

    def test_cleanup_removes_only_own_trust_and_restores_search_list(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.fixture_files(directory)
            with patch.object(fixture, 'workspace', return_value=root), patch.object(fixture, 'revoke_fixture_trust') as revoke, patch.object(fixture, 'run',
                    return_value=subprocess.CompletedProcess([], 0, stdout='"/original/login.keychain-db"')) as run:
                fixture.cleanup()
            commands = [call.args for call in run.call_args_list]
            revoke.assert_called_once_with(root)
            self.assertIn(('security', 'list-keychains', '-d', 'user', '-s', '/original/login.keychain-db'), commands)
            self.assertIn(('security', 'delete-keychain', str(root / 'localhost.keychain-db')), commands)
            self.assertFalse(root.exists())

    def test_cleanup_continues_after_trust_command_timeout_and_erases_private_material(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.fixture_files(directory)
            with patch.object(fixture, 'workspace', return_value=root), patch.object(fixture, 'revoke_fixture_trust',
                    side_effect=subprocess.TimeoutExpired('security', 60)), patch.object(fixture, 'run',
                    return_value=subprocess.CompletedProcess([], 0, stdout='"/original/login.keychain-db"')) as run:
                with self.assertRaisesRegex(RuntimeError, 'revoke localhost trust'):
                    fixture.cleanup()
            self.assertEqual(3, run.call_count)
            self.assertFalse((root / 'localhost.key').exists())
            self.assertFalse((root / 'localhost.pfx').exists())
            self.assertTrue((root / 'state.json').exists())

    def test_cleanup_is_safe_before_setup(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(fixture, 'workspace', return_value=Path(directory) / 'absent'), patch.object(fixture, 'run') as run:
                fixture.cleanup()
            run.assert_not_called()

    def test_cleanup_detects_incomplete_trust_restoration(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.fixture_files(directory)
            with patch.object(fixture, 'workspace', return_value=root), patch.object(fixture, 'run',
                    return_value=subprocess.CompletedProcess([], 0, stdout='"/original/login.keychain-db"')), patch.object(fixture, 'revoke_fixture_trust',
                    side_effect=RuntimeError('Pre-existing trust settings changed.')):
                with self.assertRaisesRegex(RuntimeError, 'Pre-existing trust settings changed'):
                    fixture.cleanup()
            self.assertFalse((root / 'localhost.key').exists())
            self.assertFalse((root / 'localhost.pfx').exists())

    def test_revocation_is_constrained_and_preserves_all_other_trust(self):
        with tempfile.TemporaryDirectory() as directory:
            root, after = self.trust_files(directory)
            with patch.object(fixture, 'export_admin_trust', return_value=after), patch.object(fixture, 'run',
                    side_effect=[subprocess.CompletedProcess([], 0), subprocess.CompletedProcess([], 1)]) as run:
                fixture.revoke_fixture_trust(root)
            self.assertEqual(('sudo', '-n', 'security', 'add-trusted-cert', '-d', '-r', 'deny',
                              '-p', 'ssl', '-s', '127.0.0.1', str(root / 'localhost.crt')), run.call_args_list[0].args)

    def test_revocation_rejects_a_certificate_still_accepted_by_the_os(self):
        with tempfile.TemporaryDirectory() as directory:
            root, after = self.trust_files(directory)
            with patch.object(fixture, 'export_admin_trust', return_value=after), patch.object(fixture, 'run',
                    return_value=subprocess.CompletedProcess([], 0)):
                with self.assertRaisesRegex(RuntimeError, 'remains trusted'):
                    fixture.revoke_fixture_trust(root)

    @staticmethod
    def trust_files(directory):
        root = Path(directory)
        pem = '-----BEGIN CERTIFICATE-----\nZHVtbXk=\n-----END CERTIFICATE-----\n'
        (root / 'localhost.crt').write_text(pem)
        before = {'trustVersion': 1, 'trustList': {'EXISTING': {'unchanged': True}}}
        (root / 'admin-trust-before.plist').write_bytes(plistlib.dumps(before))
        fingerprint = fixture.hashlib.sha1(fixture.ssl.PEM_cert_to_DER_cert(pem)).hexdigest().upper()
        after = {'trustVersion': 1, 'trustList': {'EXISTING': {'unchanged': True}, fingerprint: {
            'trustSettings': [{'kSecTrustSettingsResult': 3}]}}}
        return root, after

    @staticmethod
    def fixture_files(directory):
        root = Path(directory) / 'fixture'
        root.mkdir()
        (root / 'state.json').write_text(json.dumps({'search_list': ['/original/login.keychain-db'],
                                                   'trust_attempted': True, 'trust_added': True}))
        (root / 'admin-trust-before.plist').write_bytes(plistlib.dumps({}))
        for name in ('localhost.crt', 'localhost.key', 'localhost.pfx', 'localhost.keychain-db'):
            (root / name).write_text('synthetic test material')
        return root


if __name__ == '__main__':
    unittest.main()
