#!/usr/bin/env bash
# Cross-builds the QNX ticker and its tests, then runs them in a throwaway QNX 8.0 QEMU guest.
# Usage: run.sh [BUILD_DIR]   (needs the SDP in $QNX_SDP or ~/qnx800, cmake, QEMU and KVM)
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
runner=$(cd "$here/../.." && pwd)
sdp=${QNX_SDP:-$HOME/qnx800}
build=${1:-/tmp/qnx-timer-pulses}
source "$sdp/qnxsdp-env.sh" >/dev/null
cmake -S "$here" -B "$build" -DCMAKE_TOOLCHAIN_FILE="$runner/qnx-x86_64.cmake" >/dev/null
cmake --build "$build" >/dev/null
python3 -I "$runner/qnx_ctest.py" "$build" --sdp "$sdp" --junit "$build/junit.xml"
