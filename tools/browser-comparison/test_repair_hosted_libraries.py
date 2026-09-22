import copy
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import repair_hosted_libraries as repair


class HostedLibraryRepairTests(unittest.TestCase):
    def setUp(self):
        self.links = [dict(path=path, target=target, owner=f'python3-lldb-18:amd64: {path}\n', ownerExitCode=0)
                      for path, target in repair.KNOWN_LINKS.items()]

    def test_known_owned_links_accepted(self):
        repair.validate_links(self.links)

    def test_unknown_path_target_and_owner_refused(self):
        for field, value in [('path', '/usr/lib/other'), ('target', '/tmp/other'), ('owner', 'other: /usr/lib/other\n'), ('ownerExitCode', 1)]:
            links = copy.deepcopy(self.links)
            links[0][field] = value
            with self.assertRaises(ValueError):
                repair.validate_links(links)

    def test_plan_must_be_only_allowed_removals(self):
        self.assertEqual({'lldb-18', 'python3-lldb-18'}, repair.removal_plan('Remv lldb-18 [1]\nRemv python3-lldb-18 [1]\n'))
        for output in ('', 'Remv libc6 [1]', 'Remv python3-lldb-18 [1]\nInst libc6 [2]', 'Remv python3-lldb-18 [1]\nConf libc6 [2]'):
            with self.assertRaises(ValueError):
                repair.removal_plan(output)

    def test_actual_mutations_must_match_simulation(self):
        before = {'python3-lldb-18:amd64': '1', 'libc6:amd64': '2'}
        repair.verify_package_changes(before, {'libc6:amd64': '2'}, {'python3-lldb-18'})
        for after in ({}, {'libc6:amd64': '3'}, {**before, 'new': '1'}, before):
            with self.assertRaises(ValueError):
                repair.verify_package_changes(before, after, {'python3-lldb-18'})

    def test_inspection_never_invokes_apt(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(repair, 'inventory', return_value=self.links), patch.object(repair, 'packages', return_value={}), patch.object(repair, 'command') as command:
            repair.run(Path(directory) / 'evidence', True)
            command.assert_not_called()
            self.assertTrue((Path(directory) / 'evidence/repair.json').exists())

    def test_unknown_link_failure_preserves_evidence_without_apt(self):
        self.links[0]['target'] = '/unexpected'
        with tempfile.TemporaryDirectory() as directory, patch.object(repair, 'inventory', return_value=self.links), patch.object(repair, 'packages', return_value={}), patch.object(repair, 'command') as command:
            output = Path(directory) / 'evidence'
            with self.assertRaises(ValueError):
                repair.run(output, False)
            command.assert_not_called()
            self.assertIn('failure', (output / 'repair.json').read_text())
            self.assertTrue((output / 'links-before.json').exists())

    def test_successful_removal_records_before_after_and_plan(self):
        before = {'python3-lldb-18': '1', 'libc6': '2'}
        after = {'libc6': '2'}
        simulation = subprocess.CompletedProcess([], 0, 'Remv python3-lldb-18 [1]\n', '')
        actual = subprocess.CompletedProcess([], 0, 'Removing python3-lldb-18 ...\n', '')
        with tempfile.TemporaryDirectory() as directory, patch.object(repair, 'inventory', side_effect=[self.links, []]), patch.object(repair, 'packages', side_effect=[before, after]), patch.object(repair, 'command', side_effect=[simulation, actual]) as command:
            output = Path(directory) / 'evidence'
            repair.run(output, False)
            self.assertTrue((output / 'apt-removal.log').exists())
            self.assertIn('actualRemovals', (output / 'repair.json').read_text())
            self.assertEqual(['sudo', '-n', 'apt-get', 'remove', '--no-auto-remove', 'python3-lldb-18', '--assume-yes'], command.call_args_list[1].args[0])

    def test_timeout_preserves_partial_package_log(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(repair, 'command', side_effect=subprocess.TimeoutExpired(['apt-get'], 300, output=b'partial output')):
            log = Path(directory) / 'apt.log'
            with self.assertRaises(subprocess.TimeoutExpired):
                repair.logged_command(['apt-get'], log)
            self.assertEqual('partial output', log.read_text())
