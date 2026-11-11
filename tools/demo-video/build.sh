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

"$venv/bin/python" "$here/build.py" --out "$out"
ffprobe -v error -show_entries format=duration:stream=codec_name,width,height -of compact "$out"
