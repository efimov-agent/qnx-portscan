import unittest
from dataclasses import replace
from pathlib import Path

from qnx_ctest import Test, classify, guest_script, parse_serial

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


class GuestScriptTests(unittest.TestCase):
    def test_arguments_are_shell_quoted(self):
        self.assertIn("./t1 'two words' </dev/null", guest_script(TESTS, N))

    def test_only_environment_values_are_quoted(self):
        test = Test("e", Path("/x"), (), 5, env=("KEY=a b",))
        self.assertIn("KEY='a b' ./t0 </dev/null", guest_script([test], N))


if __name__ == "__main__":
    unittest.main()
