#!/usr/bin/env bash
# Build the demo video. Prepares ~/demo (published CLI, fixture, repo clone),
# then runs build.py (real commands -> piper narration -> frames -> ffmpeg).
# Needs: ffmpeg, jq, git, dotnet 10, a venv with piper-tts + pillow
#   python3 -m venv ~/.venvs/piper && ~/.venvs/piper/bin/pip install piper-tts pillow
#   voice: en_US-lessac-medium.onnx(.json) from huggingface.co/rhasspy/piper-voices
#          in ~/.cache/piper-voices/
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
demo="${DEMO_DIR:-$HOME/demo}"
venv="${PIPER_VENV:-$HOME/.venvs/piper}"
out="${1:-$here/out/qnx-portscan-demo.mp4}"

mkdir -p "$demo/fixture/src"
dotnet publish "$repo/src/QnxPortScan.Cli" -c Release -o "$demo/bin" -v q -nologo >/dev/null
cat > "$demo/fixture/src/loop.c" <<'C'
#include <sys/epoll.h>
#include <sys/timerfd.h>

int setup_loop(void) {
  int ep = epoll_create1(0);
  int t  = timerfd_create(CLOCK_MONOTONIC, 0);
  /* epoll_wait(ep, ev, 64, -1) in a comment: ignored */
#ifdef __linux__
  prctl(PR_SET_NAME, "loop");   /* Linux-only branch */
#endif
  return ep + t;
}
C
[ -d "$demo/qnx-portscan" ] || git clone -q --depth 1 https://github.com/efimov-agent/qnx-portscan "$demo/qnx-portscan"
git -C "$demo/qnx-portscan" pull -q --ff-only || true

# QNX beat: a libuv test binary cross-built by tools/qnx-ctest/examples/libuv/run.sh
# (needs ~/qnx800 and KVM); a few of its tests are run live in a QEMU guest.
libuv_qnx="${LIBUV_QNX:-/tmp/qnx-libuv-fix}"
[ -x "$libuv_qnx/build/uv_run_tests_a" ] || { echo "run tools/qnx-ctest/examples/libuv/run.sh $libuv_qnx first" >&2; exit 2; }
rm -rf "$demo/qnx-run" && mkdir -p "$demo/qnx-run/suite"
cp "$libuv_qnx/build/uv_run_tests_a" "$demo/qnx-run/"
cp -r "$libuv_qnx/files" "$demo/qnx-run/"
for t in tcp_ping_pong udp_send_and_recv fs_file_async timer we_get_signal pipe_ping_pong; do
  printf 'add_test(%s "%s" "%s")\nset_tests_properties(%s PROPERTIES SKIP_RETURN_CODE 7)\n' \
    "$t" "$demo/qnx-run/uv_run_tests_a" "$t" "$t"
done >"$demo/qnx-run/suite/CTestTestfile.cmake"
printf '#!/usr/bin/env bash\nexec python3 -I "%s" "$@"\n' "$repo/tools/qnx-ctest/qnx_ctest.py" >"$demo/bin/qnx-ctest"
chmod +x "$demo/bin/qnx-ctest"

"$venv/bin/python" "$here/build.py" --out "$out"
ffprobe -v error -show_entries format=duration:stream=codec_name,width,height -of compact "$out"
