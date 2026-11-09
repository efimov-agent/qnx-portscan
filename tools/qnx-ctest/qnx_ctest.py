#!/usr/bin/env python3
"""Run a CTest suite cross-built with qcc inside a throwaway QNX 8.0 QEMU guest.

The guest has no working sshd, so the tests are baked into a dedicated image and
started from the startup snippet. Each result is framed by per-run markers on the
serial console, which the host parses into a summary and a JUnit XML report.
"""
from __future__ import annotations

import argparse
import fcntl
import json
import math
import os
import re
import secrets
import shlex
import shutil
import signal
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree as ET

GUEST_DIR = "/data/data/tests"  # "/data/x" in data_files.custom appears as /data/data/x.
BOOT_MARGIN_S = 120.0
# CTest properties that decide a test's outcome and cannot be honoured here; refuse them rather than misreport.
UNSUPPORTED_PROPERTIES = (
    "PASS_REGULAR_EXPRESSION", "FAIL_REGULAR_EXPRESSION", "SKIP_REGULAR_EXPRESSION",
    "ENVIRONMENT_MODIFICATION", "TIMEOUT_AFTER_MATCH",
)
CONTROL_CHARS = re.compile("[\x00-\x08\x0b\x0c\x0e-\x1f\x7f\ufffe\uffff]")
OK_STATUSES = ("passed", "skipped", "disabled")


@dataclass(frozen=True)
class Test:
    name: str
    executable: Path
    args: tuple[str, ...]
    timeout_s: int
    disabled: bool = False
    will_fail: bool = False
    skip_return_code: int | None = None
    env: tuple[str, ...] = ()  # "KEY=value" entries.


@dataclass(frozen=True)
class Result:
    test: Test
    status: str  # passed | failed | timeout | notrun | skipped | disabled
    exit_code: int | None
    output: str


def discover(build_dir: Path, default_timeout: int) -> list[Test]:
    raw = subprocess.run(
        ["ctest", "--show-only=json-v1"], cwd=build_dir, check=True, capture_output=True, text=True
    ).stdout
    tests: list[Test] = []
    for item in json.loads(raw).get("tests", []):
        name = item.get("name")
        props = {p["name"]: p["value"] for p in item.get("properties", [])}
        if props.get("DISABLED"):
            tests.append(Test(name, Path(), (), default_timeout, disabled=True))
            continue
        for prop in UNSUPPORTED_PROPERTIES:
            if prop in props:
                raise SystemExit(f"test {name!r}: CTest property {prop} is not supported")
        command = item.get("command")
        if not command:
            raise SystemExit(f"test {name!r} has no command (target not built?)")
        executable = Path(command[0])
        if not executable.is_file():
            raise SystemExit(f"test {name!r}: {executable} does not exist; build the project first")
        env = props.get("ENVIRONMENT", [])
        env = [env] if isinstance(env, str) else env
        for entry in env:
            if not re.match(r"[A-Za-z_][A-Za-z0-9_]*=", entry):
                raise SystemExit(f"test {name!r}: bad ENVIRONMENT entry {entry!r}")
        skip_rc = int(props.get("SKIP_RETURN_CODE", -1))
        # CTest treats TIMEOUT 0 as "no limit"; the guest watchdog always needs one.
        timeout = math.ceil(float(props.get("TIMEOUT", 0)))
        timeout = timeout if timeout > 0 else default_timeout
        tests.append(
            Test(
                name, executable, tuple(command[1:]), timeout,
                will_fail=bool(props.get("WILL_FAIL")),
                skip_return_code=skip_rc if skip_rc >= 0 else None,
                env=tuple(env),
            )
        )
    return tests


def guest_script(tests: list[Test], nonce: str) -> str:
    lines = [f"cd {GUEST_DIR}", f"echo; echo {nonce}:BEGIN"]
    for i, test in enumerate(tests):
        if test.disabled:
            continue
        # Quote only the value: a quoted "KEY=value" word would be taken for a command name.
        env = (f"{k}={shlex.quote(v)}" for k, v in (e.split("=", 1) for e in test.env))
        args = (shlex.quote(a) for a in test.args)
        cmd = " ".join((*env, f"./t{i}", *args))
        lines += [
            f"echo; echo {nonce}:START:{i}",
            f"{cmd} </dev/null 2>&1 &",
            "qct_pid=$!",
            f"(sleep {test.timeout_s}; echo; echo {nonce}:TIMEOUT:{i}; kill -9 $qct_pid 2>/dev/null) &",
            "qct_dog=$!",
            "wait $qct_pid",
            "qct_rc=$?",
            "kill -9 $qct_dog 2>/dev/null",
            f"echo; echo {nonce}:END:{i}:$qct_rc",
        ]
    lines += [f"echo; echo {nonce}:DONE", "shutdown -b"]
    return "\n".join(lines) + "\n"


def parse_serial(log: str, tests: list[Test], nonce: str) -> list[Result]:
    """Map the serial log to results; a test with no complete frame is reported as notrun."""
    starts = {int(m[1]): m.end() for m in re.finditer(rf"^{nonce}:START:(\d+)\r?$", log, re.M)}
    ends = {int(m[1]): (m.start(), int(m[2])) for m in re.finditer(rf"^{nonce}:END:(\d+):(\d+)\r?$", log, re.M)}
    timed_out = {int(m[1]) for m in re.finditer(rf"^{nonce}:TIMEOUT:(\d+)\r?$", log, re.M)}
    results = []
    for i, test in enumerate(tests):
        if test.disabled:
            results.append(Result(test, "disabled", None, ""))
        elif i not in starts or i not in ends:
            results.append(Result(test, "notrun", None, clean(log[starts[i]:]) if i in starts else ""))
        else:
            end, rc = ends[i]
            results.append(Result(test, classify(test, rc, i in timed_out), rc, clean(log[starts[i]:end])))
    return results


def classify(test: Test, rc: int, timed_out: bool) -> str:
    """Apply CTest's rules: timeouts and crashes always lose, then SKIP_RETURN_CODE, then WILL_FAIL."""
    if timed_out:
        return "timeout"
    if rc > 128:  # The shell reports death by signal as 128+N; exit(139) looks the same and fails safe.
        return "failed"
    if rc == test.skip_return_code:
        return "skipped"
    return "passed" if (rc != 0) == test.will_fail else "failed"


def clean(output: str) -> str:
    """Drop what XML 1.0 cannot carry; serial logs may hold terminal escapes."""
    return CONTROL_CHARS.sub("", output.replace("\r", "")).strip("\n")


def write_junit(results: list[Result], path: Path) -> None:
    bad = sum(r.status not in OK_STATUSES for r in results)
    suite = ET.Element("testsuite", name="qnx-qemu", tests=str(len(results)), failures=str(bad))
    for r in results:
        case = ET.SubElement(suite, "testcase", name=r.test.name)
        if r.status in ("skipped", "disabled"):
            ET.SubElement(case, "skipped", message=r.status)
        elif r.status != "passed":
            ET.SubElement(case, "failure", message=f"{r.status} (exit {r.exit_code})").text = r.output
        if r.output:
            ET.SubElement(case, "system-out").text = r.output
    path.parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(suite).write(path, encoding="utf-8", xml_declaration=True)


def run_in_sdp(sdp: Path, command: str, cwd: Path) -> None:
    script = f". {shlex.quote(str(sdp / 'qnxsdp-env.sh'))} >/dev/null && {command}"
    done = subprocess.run(["bash", "-c", script], cwd=cwd, capture_output=True, text=True, timeout=900)
    if done.returncode != 0:
        sys.exit(f"image build failed:\n{done.stdout[-2000:]}\n{done.stderr[-2000:]}")


def build_image(work: Path, sdp: Path, tests: list[Test], nonce: str, data_size_mb: int) -> Path:
    base = "mkqnximage --type=qemu --arch=x86_64 --noprompt"
    size = f"--data-size={data_size_mb}"
    if not (work / "local" / "snippets").is_dir():
        run_in_sdp(sdp, f"{base} {size} --build", work)  # Creates local/ with the snippet templates.
    payload = work / "payload"
    shutil.rmtree(payload, ignore_errors=True)
    payload.mkdir()
    entries = []
    # The script ships as a file because post_start.custom goes through the C preprocessor,
    # which would rewrite arguments such as __LINE__.
    (payload / "run.sh").write_text(guest_script(tests, nonce))
    names = ["run.sh"]
    for i, test in enumerate(tests):
        if not test.disabled:
            shutil.copy2(test.executable, payload / f"t{i}")
            names.append(f"t{i}")
    entries = [f"[uid=0 gid=0 perms=0755] /data/tests/{n} = {payload / n}\n" for n in names]
    snippets = work / "local" / "snippets"
    (snippets / "data_files.custom").write_text("".join(entries))
    (snippets / "post_start.custom").write_text(f"sh {GUEST_DIR}/run.sh\n")
    run_in_sdp(sdp, f"{base} {size} --force --build", work)
    return work / "output" / "disk-qemu"


def qemu_command(disk: Path, serial: Path, memory_mb: int) -> list[str]:
    qemu = [
        "qemu-system-x86_64", "-enable-kvm", "-cpu", "host", "-m", str(memory_mb), "-smp", "2",
        "-display", "none", "-no-reboot", "-serial", f"file:{serial}",
        "-drive", f"file={disk},if=ide,format=raw",
        "-netdev", "user,id=n0,restrict=on", "-device", "virtio-net-pci,netdev=n0",
    ]
    if os.access("/dev/kvm", os.W_OK):
        return qemu
    return ["sg", "kvm", "-c", " ".join(shlex.quote(a) for a in qemu)]  # Group added after login.


def boot_and_wait(disk: Path, serial: Path, nonce: str, deadline_s: float, memory_mb: int) -> bool:
    """Boot the guest until the end marker appears; False means the deadline passed first."""
    serial.unlink(missing_ok=True)
    marker = re.compile(rf"^{nonce}:DONE\r?$".encode(), re.M)
    proc = subprocess.Popen(qemu_command(disk, serial, memory_mb), stderr=subprocess.PIPE, start_new_session=True)
    try:
        end = time.monotonic() + deadline_s
        while time.monotonic() < end:
            if serial.exists() and marker.search(serial.read_bytes()):
                return True
            if proc.poll() is not None:
                err = proc.stderr.read().decode(errors="replace") if proc.stderr else ""
                if proc.returncode != 0:
                    print(f"qemu exited with {proc.returncode}: {err[-1000:]}", file=sys.stderr)
                return bool(serial.exists() and marker.search(serial.read_bytes()))
            time.sleep(0.5)
        return False
    finally:
        for sig in (signal.SIGTERM, signal.SIGHUP):
            signal.signal(sig, signal.SIG_IGN)  # A repeated signal must not abort the cleanup.
        if proc.poll() is None:
            os.killpg(proc.pid, signal.SIGTERM)  # The session also covers qemu started through sg.
            try:
                proc.wait(timeout=20)
            except subprocess.TimeoutExpired:
                os.killpg(proc.pid, signal.SIGKILL)
                proc.wait()


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("build_dir", type=Path, help="CMake build directory configured with the QNX toolchain")
    ap.add_argument("--sdp", type=Path, default=Path.home() / "qnx800")
    ap.add_argument("--work", type=Path, default=Path.home() / ".cache" / "qnx-ctest")
    ap.add_argument("--junit", type=Path, help="write a JUnit XML report here")
    ap.add_argument("--timeout", type=int, default=60, help="seconds per test without a CTest TIMEOUT")
    ap.add_argument("--data-size", type=int, default=64, help="extra MB in the data partition")
    ap.add_argument("--memory", type=int, default=1024, help="guest memory in MB")
    args = ap.parse_args()
    if args.timeout <= 0:
        ap.error("--timeout must be positive")
    # The guest runs in its own session, so turn these into an exception that reaches its cleanup.
    for sig in (signal.SIGTERM, signal.SIGHUP):
        signal.signal(sig, lambda *_: sys.exit(143))

    tests = discover(args.build_dir.resolve(), args.timeout)
    if not tests:
        sys.exit("no tests found in the build directory")
    work = args.work.resolve()
    if re.search(r"[\s,]", str(work)):
        sys.exit("--work must not contain whitespace or commas: mkifs and qemu options split paths on them")
    work.mkdir(parents=True, exist_ok=True)
    lock = (work / ".lock").open("w")
    try:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        sys.exit(f"another run is using {work}; pass a different --work")
    nonce = "QCT" + secrets.token_hex(6)
    disk = build_image(work, args.sdp.resolve(), tests, nonce, args.data_size)
    serial = work / "serial.log"
    finished = boot_and_wait(disk, serial, nonce, BOOT_MARGIN_S + sum(t.timeout_s for t in tests), args.memory)
    log = serial.read_bytes().decode("utf-8", errors="replace") if serial.exists() else ""
    results = parse_serial(log, tests, nonce)

    for r in results:
        print(f"{r.status.upper():8} {r.test.name}" + (f" (exit {r.exit_code})" if r.exit_code else ""))
    counts = {status: sum(r.status == status for r in results) for status in OK_STATUSES}
    summary = ", ".join(f"{n} {status}" for status, n in counts.items() if n) or "none passed"
    print(f"{summary} of {len(results)} on QNX; serial log: {serial}")
    if not finished:
        print("guest did not reach the end of the test list before the deadline", file=sys.stderr)
    if args.junit:
        write_junit(results, args.junit)
    return 0 if finished and all(r.status in OK_STATUSES for r in results) else 1


if __name__ == "__main__":
    sys.exit(main())
