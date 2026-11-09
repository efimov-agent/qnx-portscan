#!/usr/bin/env bash
# Cross-builds libuv with qcc and runs each of its tests in a throwaway QNX 8.0 guest.
# Usage: run.sh [work-dir] [extra.patch]   (default /tmp/qnx-libuv); results land in <work-dir>/junit.xml
set -euo pipefail
export PYTHONDONTWRITEBYTECODE=1
here=$(cd "$(dirname "$0")" && pwd)
tools=$(cd "$here/../.." && pwd)
work=${1:-/tmp/qnx-libuv}
if [ -n "${2:-}" ] && [ ! -f "$2" ]; then echo "no such patch: $2" >&2; exit 2; fi
extra=${2:+$(realpath "$2")}
sdp=${QNX_SDP:-$HOME/qnx800}
commit=9e40720a0cdfd99970eacc43b39fa1404c6df38d
source "$sdp/qnxsdp-env.sh" >/dev/null

mkdir -p "$work"
# The marker holds the patches' content hash, so editing a patch in place also rebuilds the tree.
stamp=$(cat "$here/qnx-sdp8.patch" ${extra:+"$extra"} | sha256sum)
if [ ! -f "$work/src/.qnx-patched" ] || [ "$(cat "$work/src/.qnx-patched")" != "$stamp" ]; then
  rm -rf "$work/src"
  git init -q "$work/src"
  git -C "$work/src" fetch -q --depth 1 https://github.com/libuv/libuv "$commit"
  git -C "$work/src" checkout -q FETCH_HEAD
  git -C "$work/src" apply "$here/qnx-sdp8.patch"
  if [ -n "$extra" ]; then git -C "$work/src" apply "$extra"; fi
  printf '%s\n' "$stamp" >"$work/src/.qnx-patched"
fi
cmake -S "$work/src" -B "$work/build" -DCMAKE_TOOLCHAIN_FILE="$tools/qnx-x86_64.cmake" -DBUILD_TESTING=ON >/dev/null
cmake --build "$work/build" -j"$(nproc)" --target uv_run_tests_a >/dev/null

exe=$work/build/uv_run_tests_a
# The list comes from the QNX binary itself: it is what the test table was compiled with.
mkdir -p "$work/list"
printf 'add_test(list "%s" "--list")\n' "$exe" >"$work/list/CTestTestfile.cmake"
python3 -I "$tools/qnx_ctest.py" "$work/list" --work "$work/vm-list" --sdp "$sdp" --user qnxuser --junit "$work/list.xml" >"$work/list.txt" \
  || { echo "listing the tests in the guest failed, see $work/list.txt" >&2; exit 1; }
python3 -I "$here/suite.py" generate "$work/list.xml" "$exe" "$work/suite/CTestTestfile.cmake"

mkdir -p "$work/files/test"
cp -r "$work/src/test/fixtures" "$work/files/test/"
rm -f "$work/junit.xml" "$work/results.txt"
# A non-zero exit also means failing tests, so the run itself is validated below.
python3 -I "$tools/qnx_ctest.py" "$work/suite" --work "$work/vm" --sdp "$sdp" --junit "$work/junit.xml" \
  --files "$work/files" --user qnxuser --timeout 120 --data-size 256 >"$work/results.txt" || true
python3 -I "$here/suite.py" verify "$work/list.xml" "$work/junit.xml"
tail -1 "$work/results.txt"
