import unittest
from dataclasses import replace
from pathlib import Path

import tempfile

from qnx_ctest import Test, classify, data_file_entries, executable_names, guest_script, parse_serial

N = "QCTabc"
TESTS = [Test("a", Path("/x/a"), (), 5), Test("b", Path("/x/b"), ("two words",), 5), Test("c", Path("/x/c"), (), 5)]


class ParseSerialTests(unittest.TestCase):
    def statuses(self, log):
        return [(r.status, r.exit_code) for r in parse_serial(log, TESTS, N)]

    def test_classifies_pass_fail_timeout(self):
        log = (
            f"boot noise\n{N}:START:0\r\nok\r\n{N}:END:0:0\r\n"
            f"{N}:START:1\n{N}:END:1:3\n{N}:START:2\n{N}:END:2:137\n"
        )
        log = log.replace(f"{N}:END:2:137", f"{N}:TIMEOUT:2\n{N}:END:2:137")
        self.assertEqual(self.statuses(log), [("passed", 0), ("failed", 3), ("timeout", 137)])

    def test_exit_137_without_watchdog_is_a_plain_failure(self):
        results = parse_serial(f"{N}:START:0\n{N}:END:0:137\n", TESTS[:1], N)
        self.assertEqual(results[0].status, "failed")

    def test_control_characters_are_removed_from_output(self):
        result = parse_serial(f"{N}:START:0\n\x1b[0mok\x00\n{N}:END:0:0\n", TESTS[:1], N)[0]
        self.assertEqual(result.output, "[0mok")

    def test_guest_crash_mid_test_is_notrun_with_partial_output(self):
        results = parse_serial(f"{N}:START:0\n{N}:END:0:0\n{N}:START:1\npartial", TESTS, N)
        self.assertEqual([r.status for r in results], ["passed", "notrun", "notrun"])
        self.assertIn("partial", results[1].output)

    def test_markers_from_another_nonce_are_ignored(self):
        self.assertEqual(self.statuses("QCTother:START:0\nQCTother:END:0:0\n"), [("notrun", None)] * 3)

    def test_output_is_captured_between_markers(self):
        result = parse_serial(f"{N}:START:0\r\nline1\r\nline2\r\n{N}:END:0:0\r\n", TESTS[:1], N)[0]
        self.assertEqual(result.output, "line1\nline2")


class ClassifyTests(unittest.TestCase):
    T = Test("t", Path("/x"), (), 5)

    def test_will_fail_inverts_exit_status(self):
        t = replace(self.T, will_fail=True)
        self.assertEqual((classify(t, 1, False), classify(t, 0, False)), ("passed", "failed"))

    def test_skip_return_code_beats_will_fail(self):
        self.assertEqual(classify(replace(self.T, skip_return_code=77, will_fail=True), 77, False), "skipped")

    def test_crash_is_never_a_pass_or_skip(self):
        self.assertEqual(classify(replace(self.T, will_fail=True), 139, False), "failed")
        self.assertEqual(classify(replace(self.T, skip_return_code=139), 139, False), "failed")

    def test_timeout_is_never_a_pass(self):
        self.assertEqual(classify(replace(self.T, will_fail=True), 137, True), "timeout")


class ImageContentTests(unittest.TestCase):
    def test_one_binary_with_many_arguments_is_shipped_once(self):
        same = [Test("x", Path("/x/a"), ("x",), 5), Test("y", Path("/x/a"), ("y",), 5), Test("z", Path("/x/b"), (), 5)]
        self.assertEqual(executable_names(same), {Path("/x/a"): "e0", Path("/x/b"): "e1"})
        script = guest_script(same, N)
        self.assertIn("./e0 x </dev/null", script)
        self.assertIn("./e0 y </dev/null", script)

    def test_disabled_tests_do_not_ship_an_executable(self):
        self.assertEqual(executable_names([Test("d", Path(), (), 5, disabled=True)]), {})

    def test_data_files_keep_their_relative_path(self):
        with tempfile.TemporaryDirectory() as tmp:
            nested = Path(tmp) / "test" / "fixtures"
            nested.mkdir(parents=True)
            (nested / "empty").touch()
            entries = data_file_entries(Path(tmp))
        self.assertEqual(entries, [f"[uid=0 gid=0 perms=0644] /data/tests/test/fixtures/empty = {nested / 'empty'}\n"])

    def test_data_file_clashing_with_runner_files_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "run.sh").touch()
            with self.assertRaises(SystemExit):
                data_file_entries(Path(tmp))

    def test_data_path_that_mkifs_would_split_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "a b").touch()
            with self.assertRaises(SystemExit):
                data_file_entries(Path(tmp))


class GuestScriptTests(unittest.TestCase):
    def test_arguments_are_shell_quoted(self):
        self.assertIn("./e1 'two words' </dev/null", guest_script(TESTS, N))

    def test_user_wraps_each_test_and_owns_the_run_directory(self):
        script = guest_script(TESTS[:1], N, "qnxuser")
        self.assertIn("on -u qnxuser ./e0 </dev/null", script)
        self.assertIn("chown -R qnxuser:$qct_gid /data/data/tests", script)
        self.assertLess(script.index("chown"), script.index(f"{N}:START:0"))
        self.assertIn(f"{N}:SETUP_FAILED", script)

    def test_without_user_nothing_changes_owner(self):
        script = guest_script(TESTS[:1], N)
        self.assertNotIn("chown", script)
        self.assertNotIn("on -u", script)

    def test_only_environment_values_are_quoted(self):
        test = Test("e", Path("/x"), (), 5, env=("KEY=a b",))
        self.assertIn("KEY='a b' ./e0 </dev/null", guest_script([test], N))


if __name__ == "__main__":
    unittest.main()
