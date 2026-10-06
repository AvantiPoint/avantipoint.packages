import json
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
            with patch.object(fixture, 'workspace', return_value=root), patch.object(fixture, 'run',
                    return_value=subprocess.CompletedProcess([], 0)) as run:
                fixture.cleanup()
            commands = [call.args for call in run.call_args_list]
            self.assertIn(('sudo', '-n', 'security', 'remove-trusted-cert', '-d', str(root / 'localhost.crt')), commands)
            self.assertIn(('security', 'list-keychains', '-d', 'user', '-s', '/original/login.keychain-db'), commands)
            self.assertIn(('security', 'delete-keychain', str(root / 'localhost.keychain-db')), commands)
            self.assertFalse(root.exists())

    def test_cleanup_continues_after_trust_command_timeout_and_erases_private_material(self):
        with tempfile.TemporaryDirectory() as directory:
            root = self.fixture_files(directory)
            with patch.object(fixture, 'workspace', return_value=root), patch.object(fixture, 'run',
                    side_effect=[subprocess.TimeoutExpired('security', 60), subprocess.CompletedProcess([], 0),
                                 subprocess.CompletedProcess([], 0)]) as run:
                with self.assertRaisesRegex(RuntimeError, 'remove localhost trust'):
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

    @staticmethod
    def fixture_files(directory):
        root = Path(directory) / 'fixture'
        root.mkdir()
        (root / 'state.json').write_text(json.dumps({'search_list': ['/original/login.keychain-db'],
                                                   'trust_attempted': True, 'trust_added': True}))
        for name in ('localhost.crt', 'localhost.key', 'localhost.pfx', 'localhost.keychain-db'):
            (root / name).write_text('synthetic test material')
        return root


if __name__ == '__main__':
    unittest.main()
