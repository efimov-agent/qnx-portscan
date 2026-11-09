"""Builds the per-test CTest file for libuv from `uv_run_tests_a --list`, and validates a finished run."""
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

LIST_LINE = re.compile(r"([a-z0-9_]+)(\s+\(helpers: [^)]*\))?")


def names_from(junit: str) -> list[str]:
    output = ET.parse(junit).getroot().findtext("testcase/system-out") or ""
    names = []
    for line in filter(None, (raw.strip() for raw in output.splitlines())):
        match = LIST_LINE.fullmatch(line)
        if match is None:
            sys.exit(f"unrecognised line in the libuv test list: {line!r}")
        names.append(match[1])
    if not names:
        sys.exit("the libuv test list is empty")
    return names


def entry(name: str, executable: str) -> str:
    # Exit status 7 is TEST_SKIP in libuv's test/task.h.
    return f'add_test({name} "{executable}" "{name}")\nset_tests_properties({name} PROPERTIES SKIP_RETURN_CODE 7)\n'


def generate(junit: str, executable: str, destination: str) -> None:
    names = names_from(junit)
    target = Path(destination)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text("".join(entry(n, executable) for n in names))
    print(f"{len(names)} tests")


def verify(list_junit: str, run_junit: str) -> None:
    """A finished run must report every listed test, and none may have been left unexecuted."""
    expected = names_from(list_junit)
    root = ET.parse(run_junit).getroot()
    reported = [case.get("name") for case in root]
    if sorted(reported) != sorted(expected):
        sys.exit(f"run reported {len(reported)} tests, the list has {len(expected)}")
    notrun = [c.get("name") for c in root if (c.find("failure") is not None and "notrun" in c.find("failure").get("message"))]
    if notrun:
        sys.exit(f"{len(notrun)} tests did not run: {notrun[:5]}")


if __name__ == "__main__":
    {"generate": generate, "verify": verify}[sys.argv[1]](*sys.argv[2:])
