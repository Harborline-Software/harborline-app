"""Preparation order and fail-fast checks; no SDK, feed or mutation execution."""
import os, subprocess, unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASH = r'C:\Program Files\Git\bin\bash.exe' if os.name == 'nt' else 'bash'
class AppWorkflowTests(unittest.TestCase):
    def test_python_is_pinned_before_full_lock_with_matching_guard(self):
        workflow = (ROOT/'.github/workflows/stryker.yml').read_text()
        setup = workflow.index('      - uses: actions/setup-python@v6')
        full = workflow.index('      - name: Reserved full mutation and isolated feed')
        block = workflow[setup:full]
        self.assertIn("python-version: '3.14'", block)
        self.assertIn("if: github.event_name != 'pull_request'", block)
        self.assertNotIn('continue-on-error', block)
        self.assertIn('python .github/workloads/host-workload-lock.py', workflow[full:])
        self.assertNotIn('always()', workflow[full:workflow.index('      - name: Mutation report')])
    def test_feed_failure_prevents_restore_and_mutation(self):
        command = 'node() { return 23; }; dotnet() { echo SHOULD_NOT_RUN; }; export -f node dotnet; "$BASH" .github/workloads/full-mutation.sh'
        result = subprocess.run([BASH,'-c',command],cwd=ROOT,text=True,capture_output=True)
        self.assertEqual(result.returncode,23,result.stderr)
        self.assertNotIn('SHOULD_NOT_RUN',result.stdout)
    def test_restore_failure_prevents_mutation(self):
        command = 'node() { case "$1" in eng/stryker.mjs) echo SHOULD_NOT_RUN;; *) echo feed;; esac; }; dotnet() { return 17; }; export -f node dotnet; "$BASH" .github/workloads/full-mutation.sh'
        result = subprocess.run([BASH,'-c',command],cwd=ROOT,text=True,capture_output=True)
        self.assertEqual(result.returncode,17,result.stderr)
        self.assertNotIn('SHOULD_NOT_RUN',result.stdout)
if __name__ == '__main__': unittest.main()
