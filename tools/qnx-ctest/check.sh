#!/usr/bin/env bash
# End-to-end check: the sample suite runs in a real QNX guest and each outcome is classified correctly.
set -euo pipefail
export PYTHONDONTWRITEBYTECODE=1
here=$(cd "$(dirname "$0")" && pwd)
sdp=${QNX_SDP:-$HOME/qnx800}
python3 -I -m unittest discover -s "$here" -p "test_*.py" 2>&1 | tail -1
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
source "$sdp/qnxsdp-env.sh" >/dev/null
build() { cmake -S "$here/sample" -B "$tmp/$1" -DCMAKE_TOOLCHAIN_FILE="$here/qnx-x86_64.cmake" "${@:2}" >/dev/null; cmake --build "$tmp/$1" >/dev/null; }

build mixed
set +e; python3 -I "$here/qnx_ctest.py" "$tmp/mixed" --work "$tmp/w" --sdp "$sdp" --junit "$tmp/r.xml"; rc=$?; set -e
[ "$rc" = 1 ] || { echo "FAIL: expected exit 1 (suite has failing tests), got $rc"; exit 1; }
python3 -I - "$tmp/r.xml" <<'PY'
import sys, xml.etree.ElementTree as ET
want = {
    "ipc_roundtrip": "passed", "args_pass": "passed", "macro_arg": "passed", "will_fail": "passed",
    "env_check": "passed", "skipped": "skip:skipped", "disabled": "skip:disabled",
    "args_fail": "failed (exit 7)", "exit_137": "failed (exit 137)", "hang": "timeout (exit 137)",
    "crash_will_fail": "failed (exit 139)",
}
def outcome(c):
    if c.find("skipped") is not None: return "skip:" + c.find("skipped").get("message")
    return c.find("failure").get("message") if c.find("failure") is not None else "passed"
root = ET.parse(sys.argv[1]).getroot()
got = {c.get("name"): outcome(c) for c in root}
assert got == want, got
out = {c.get("name"): c.findtext("system-out") or "" for c in root}
assert "reply=Ping" in out["ipc_roundtrip"], out
assert "arg=[two words]" in out["args_pass"], out
assert "arg=[__LINE__]" in out["macro_arg"], out
assert "QCT_VALUE=[a b]" in out["env_check"], out
PY

build green -DONLY_PASSING=ON
python3 -I "$here/qnx_ctest.py" "$tmp/green" --work "$tmp/w" --sdp "$sdp" \
  || { echo "FAIL: an all-passing suite must exit 0"; exit 1; }

# SIGTERM to the runner must not leave the guest behind.
python3 -I "$here/qnx_ctest.py" "$tmp/green" --work "$tmp/w2" --sdp "$sdp" >/dev/null 2>&1 &
runner=$!
for _ in $(seq 1 240); do pgrep -f "file=$tmp/w2/output/disk-qemu" >/dev/null && break; sleep 0.5; done
pgrep -f "file=$tmp/w2/output/disk-qemu" >/dev/null || { echo "FAIL: guest for the SIGTERM check never started"; exit 1; }
kill -TERM "$runner"; wait "$runner" || true
sleep 1
if pgrep -f "file=$tmp/w2/output/disk-qemu" >/dev/null; then echo "FAIL: qemu survived SIGTERM"; exit 1; fi
echo "qnx-ctest check OK"
