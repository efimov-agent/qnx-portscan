#!/usr/bin/env bash
# Acceptance check for qnx-portscan: build, unit tests, CLI smoke on a fixture.
set -euo pipefail
cd "$(dirname "$0")"
dotnet build -c Release -v q -nologo
dotnet test -c Release -v q -nologo
BIN=src/QnxPortScan.Cli/bin/Release/net10.0/qnx-portscan
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/p/src"
cat > "$tmp/p/src/loop.c" <<'C'
#include <sys/epoll.h>
#include <sys/timerfd.h>
int main(void) {
  int ep = epoll_create1(0);
  int t = timerfd_create(CLOCK_MONOTONIC, 0);
  /* epoll_wait(ep) in a comment must be ignored */
#ifdef __linux__
  prctl(0);
#endif
  return ep + t;
}
C
set +e; "$BIN" scan "$tmp/p" --format both --out "$tmp/r" --fail-on error >/dev/null; rc=$?; set -e
[ "$rc" = 1 ] || { echo "FAIL: expected exit 1, got $rc"; exit 1; }
for id in QNX002 QNX003 QNX009 QNX010; do grep -q "\"ruleId\": \"$id\"" "$tmp/r.sarif" || { echo "FAIL: $id missing"; exit 1; }; done
n=$(grep -c '"ruleId"' "$tmp/r.sarif"); [ "$n" = 4 ] || { echo "FAIL: expected 4 results, got $n"; exit 1; }
python3 -c "import json,sys; d=json.load(open(sys.argv[1])); assert d['version']=='2.1.0'" "$tmp/r.sarif"
echo "qnx-portscan check OK"
