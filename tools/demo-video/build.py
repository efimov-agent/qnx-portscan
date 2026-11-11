#!/usr/bin/env python3
"""Build the qnx-portscan demo video (storyboard: docs/demo-script.md).

Pipeline: run every on-screen command for real in $DEMO_DIR (default ~/demo),
capture its output, synthesise narration with piper, render terminal frames
with Pillow and encode H.264/AAC with ffmpeg.

Usage: build.py [--out FILE] [--no-tts]
Needs: Pillow, piper (python -m piper), ffmpeg, jq, git, a published
qnx-portscan in $DEMO_DIR/bin (see build.sh).
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import wave
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
OUT = HERE / "out"
DEMO = Path(os.environ.get("DEMO_DIR", Path.home() / "demo"))
VOICE = Path(os.environ.get("PIPER_VOICE", Path.home() / ".cache/piper-voices/en_US-lessac-medium.onnx"))
PIPER = os.environ.get("PIPER_BIN", str(Path(sys.executable).with_name("piper")))

W, H = 1920, 1080
FPS = 30
MONO = "/usr/share/fonts/truetype/jetbrains-mono/JetBrainsMono-Regular.ttf"
MONO_B = "/usr/share/fonts/truetype/jetbrains-mono/JetBrainsMono-Bold.ttf"
SANS = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
SANS_B = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
FONT_PX, LINE_H = 27, 37
TERM_X, TERM_Y, TERM_W, TERM_H = 50, 30, W - 100, 850
PAD = 26
PROMPT = "~/demo $ "
TYPE_CPS = 30.0
REPO_URL = "https://github.com/efimov-agent/qnx-portscan"

BG = (12, 16, 24)
TERM_BG = (22, 27, 36)
FG = (215, 220, 228)
DIM = (140, 150, 165)
GREEN = (120, 220, 140)
RED = (255, 110, 110)
YELLOW = (240, 200, 90)
CYAN = (110, 200, 255)
BLUE = (130, 170, 255)
HL = (255, 214, 64)

JQ = 'jq -r \'.runs[0].results[] | "\\(.ruleId) \\(.level) \\(.message.text[0:58])"\' r.sarif'
JQ_NOTE = JQ.replace("results[] |", 'results[] | select(.level=="note") |')

# --------------------------------------------------------------------------
# Storyboard. Each step: optional clear, commands typed in order, narration
# (caption text; `tts` overrides pronunciation), highlight substrings.
# --------------------------------------------------------------------------
@dataclass
class Step:
    say: str
    cmds: list[str] = field(default_factory=list)
    clear: bool = True
    hl: list[str] = field(default_factory=list)
    card: str | None = None      # "title" | "end"
    badge: str | None = None     # small overlay label
    min_s: float = 0.0


BEATS: list[tuple[str, list[Step]]] = [
    ("", [
        Step(card="title", min_s=3.0,
             say="Porting C and C++ code to QNX means hunting down every Linux-only API by hand. "
                 "qnx-portscan does that pass for you: it flags each call site a port has to revisit, "
                 "with the documented QNX 8.0 replacement."),
    ]),
    ("1 · What it is", [
        Step(cmds=["qnx-portscan --help"],
             say="It's a single .NET 10 command-line tool: scan a source tree, get Markdown or SARIF.",
             hl=["--format md|sarif|both", "--fail-on error|warning|none"]),
        Step(cmds=["qnx-portscan rules | wc -l", "qnx-portscan rules | head -16"],
             say="56 rules, checked against the QNX SDP 8.0 C Library Reference and a real QNX guest. "
                 "Each carries a severity, a suggested replacement, and a link to the QNX docs.",
             hl=["56"]),
    ]),
    ("2 · Scan a real project", [
        Step(cmds=["git clone --depth 1 https://github.com/libuv/libuv"], badge="clone sped up",
             say="Let's point it at libuv, a library that already has partial QNX support."),
        Step(cmds=["qnx-portscan scan libuv --format both --out libuv-report"], clear=False,
             say="About 380 files, scanned in a couple of seconds.",
             hl=["28 findings"]),
        Step(cmds=["head -18 libuv-report.md"],
             say="28 findings, 19 of them errors: real call sites a port has to change.",
             hl=["Findings: **28** (errors 19", "QNX023", "QNX021 "]),
        Step(cmds=["grep -A6 '^### QNX004' libuv-report.md"],
             say="kqueue? Use ionotify with pulses, or poll. And every finding links straight to the QNX "
                 "reference page, so you go from 'what do I do here' to the answer in one click.",
             hl=["Use ionotify() + pulses, or poll().", "Docs: <https://www.qnx.com"]),
        Step(cmds=["grep -A4 -E '^### QNX02(1|3):' libuv-report.md"],
             say="Linux CPU affinity becomes ThreadCtl run masks; sysinfo becomes the QNX system page.",
             hl=["system page (_syspage_ptr", "ThreadCtl(_NTO_TCTL_RUNMASK"]),
    ]),
    ("3 · It understands C", [
        Step(cmds=["cat fixture/src/loop.c"],
             say="It's not grep. The epoll_wait in this comment is ignored. And prctl sits in a Linux-only "
                 "ifdef branch that a QNX build never compiles, so it is trusted by default.",
             hl=["/* epoll_wait", "#ifdef __linux__"]),
        Step(cmds=["qnx-portscan scan fixture --format sarif --out r.sarif", JQ, "jq -r .version r.sarif"],
             say="Result: four errors, exactly the code a port has to touch, as SARIF 2.1.0.",
             hl=["4 findings", "2.1.0"]),
        Step(cmds=["qnx-portscan scan fixture --format sarif --out r.sarif --include-guarded", JQ_NOTE],
             clear=False,
             say="Want the full picture? include-guarded lists those Linux-only branches as notes.",
             hl=["QNX013 note"]),
    ]),
    ("4 · Fits CI", [
        Step(cmds=['qnx-portscan scan libuv --fail-on error --out ci.md ; echo "exit=$?"'],
             say="For CI, fail-on error returns a non-zero exit code, so it drops into any pipeline "
                 "as a portability gate. And GitHub code scanning renders the SARIF findings inline "
                 "on the pull request.",
             hl=["exit=1"], min_s=4.0),
    ]),
    ("5 · Breadth", [
        Step(cmds=["sed -n '/^| Project/,/^| \\[curl/p' qnx-portscan/docs/demo/README.md",
                   "awk -F'|' '/^\\| \\[/ {s+=$4} END {print \"total findings:\", s}' qnx-portscan/docs/demo/README.md"],
             say="Across seven well-known projects, libuv, redis, mosquitto, libzmq, nginx, sqlite and curl, "
                 "it surfaced 175 concrete port tasks, each with a documented QNX path. Redis alone: 59 findings. "
                 "Portable code like sqlite stays quiet.",
             hl=["| 59 |", "total findings: 175", "[sqlite]"]),
    ]),
    ("", [
        Step(card="end", min_s=4.0,
             say="qnx-portscan. Open source, on GitHub."),
    ]),
]

TTS_FIX = [
    (r"qnx-portscan", "Q N X port-scan"), (r"\bQNX\b", "Q N X"), (r"\.NET 10", "dot net 10"),
    (r"libuv", "lib U V"), (r"libzmq", "lib Z M Q"), (r"SARIF", "sarif"), (r"\bSDP\b", "S D P"),
    (r"epoll_wait", "e-poll wait"), (r"kqueue", "K queue"), (r"ionotify", "I O notify"),
    (r"prctl", "P R control"), (r"ifdef", "if-def"), (r"sysinfo", "sys info"), (r"\bCI\b", "C I"),
    (r"ThreadCtl", "thread control"), (r"include-guarded", "include guarded"), (r"sqlite", "S Q lite"),
    (r"nginx", "engine X"), (r"Linux-only", "Linux only"),
]


def tts_text(s: str) -> str:
    for a, b in TTS_FIX:
        s = re.sub(a, b, s)
    return s


# --------------------------------------------------------------------------
def run_capture(cmd: str) -> str:
    env = dict(os.environ)
    env["PATH"] = f"{DEMO / 'bin'}:{env['PATH']}"
    env["NO_COLOR"] = "1"
    if cmd.startswith("git clone") and (DEMO / "libuv").exists():
        shutil.rmtree(DEMO / "libuv")
    for f in ("libuv-report.md", "libuv-report.sarif", "r.sarif", "ci.md"):
        if cmd.startswith(f"qnx-portscan scan") and f in cmd and (DEMO / f).exists():
            (DEMO / f).unlink()
    p = subprocess.run(["bash", "-c", cmd + " 2>&1"], cwd=DEMO, env=env,
                       capture_output=True, text=True)
    out = p.stdout.rstrip("\n")
    for bad in ("/home/", "@", "TOKEN", "KEY="):
        if bad in out:
            raise SystemExit(f"refusing: output of {cmd!r} contains {bad!r}")
    return out


def synth(text: str, path: Path, no_tts: bool) -> float:
    if no_tts:
        return 0.065 * len(text) + 0.5
    if not path.exists():
        subprocess.run([PIPER, "-m", str(VOICE), "-f", str(path), "--length-scale", "0.92",
                        "--sentence-silence", "0.25"],
                       input=tts_text(text), text=True, check=True, capture_output=True)
    with wave.open(str(path)) as w:
        return w.getnframes() / w.getframerate()


# --------------------------------------------------------------------------
fm = ImageFont.truetype(MONO, FONT_PX)
fmb = ImageFont.truetype(MONO_B, FONT_PX)
CHAR_W = fm.getlength("M")
COLS = int((TERM_W - 2 * PAD) // CHAR_W)
ROWS = int((TERM_H - 60 - 2 * PAD) // LINE_H)

TOKEN_RE = re.compile(r"(QNX0\d\d|\b(?:Error|error)\b|\b(?:Warning|warning)\b|\b(?:Note|note)\b|https?://\S+|exit=\d)")


def colorize(text: str):
    segs, pos = [], 0
    for m in TOKEN_RE.finditer(text):
        if m.start() > pos:
            segs.append((text[pos:m.start()], FG))
        t = m.group(0)
        c = (CYAN if t.startswith("QNX") else RED if t.lower() == "error" else YELLOW if t.lower() == "warning"
             else BLUE if t.startswith("http") else GREEN if t.startswith("exit") else (160, 200, 255))
        segs.append((t, c))
        pos = m.end()
    if pos < len(text):
        segs.append((text[pos:], FG))
    return segs


def wrap(text: str, kind: str) -> list[tuple[str, str]]:
    """Split a logical line into (row_text, kind) rows of at most COLS chars."""
    text = text.expandtabs(4)
    rows = [text[i:i + COLS] for i in range(0, max(len(text), 1), COLS)] or [""]
    return [(r, kind) for r in rows]


@dataclass(frozen=True)
class Screen:
    rows: tuple = ()          # tuple of (text, kind) where kind in out|cmd
    hl: tuple = ()
    cursor: bool = True
    card: str | None = None
    label: str = ""
    badge: str | None = None


def draw_card(d: ImageDraw.ImageDraw, kind: str):
    big = ImageFont.truetype(MONO_B, 120)
    sub = ImageFont.truetype(SANS, 46)
    small = ImageFont.truetype(SANS, 34)
    url = ImageFont.truetype(MONO, 40)
    title = "qnx-portscan"
    tw = d.textlength(title, font=big)
    d.text(((W - tw) / 2, 250), title, font=big, fill=(255, 255, 255))
    lines = ([("Find Linux-only APIs in C/C++ code,", FG),
              ("get the documented QNX 8.0 replacement.", FG)] if kind == "title"
             else [("56 rules · Markdown + SARIF 2.1.0 · --fail-on for CI", FG),
                   ("MIT-licensed, built with .NET 10", DIM)])
    y = 430
    for t, c in lines:
        lw = d.textlength(t, font=sub)
        d.text(((W - lw) / 2, y), t, font=sub, fill=c)
        y += 66
    chips = "QNX SDP 8.0  ·  C / C++  ·  .NET 10  ·  static analysis" if kind == "title" else ""
    if chips:
        cw = d.textlength(chips, font=small)
        d.text(((W - cw) / 2, y + 40), chips, font=small, fill=CYAN)
    uw = d.textlength(REPO_URL, font=url)
    d.text(((W - uw) / 2, 760), REPO_URL, font=url, fill=GREEN)


def render(scr: Screen, caption: str, path: Path):
    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img, "RGBA")
    if scr.card:
        draw_card(d, scr.card)
    else:
        d.rounded_rectangle([TERM_X, TERM_Y, TERM_X + TERM_W, TERM_Y + TERM_H], 18, fill=TERM_BG,
                            outline=(50, 58, 72), width=2)
        d.rounded_rectangle([TERM_X, TERM_Y, TERM_X + TERM_W, TERM_Y + 52], 18, fill=(34, 40, 52))
        d.rectangle([TERM_X, TERM_Y + 30, TERM_X + TERM_W, TERM_Y + 52], fill=(34, 40, 52))
        for i, c in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
            d.ellipse([TERM_X + 22 + i * 34, TERM_Y + 16, TERM_X + 42 + i * 34, TERM_Y + 36], fill=c)
        tf = ImageFont.truetype(SANS, 24)
        d.text((TERM_X + 140, TERM_Y + 12), "bash — ~/demo", font=tf, fill=DIM)
        if scr.label:
            lf = ImageFont.truetype(SANS_B, 26)
            lw = d.textlength(scr.label, font=lf)
            d.text((TERM_X + TERM_W - lw - 24, TERM_Y + 11), scr.label, font=lf, fill=HL)
        rows = list(scr.rows)[-ROWS:]
        x0, y0 = TERM_X + PAD, TERM_Y + 52 + PAD
        for i, (text, kind) in enumerate(rows):
            y = y0 + i * LINE_H
            if kind == "out" and any(h and h in text for h in scr.hl):
                d.rectangle([x0 - 8, y - 3, x0 + CHAR_W * max(len(text), 1) + 8, y + LINE_H - 5],
                            fill=(HL[0], HL[1], HL[2], 52), outline=HL + (200,), width=2)
            if kind == "cmd":
                x = x0
                if text.startswith(PROMPT):
                    d.text((x, y), PROMPT, font=fmb, fill=GREEN)
                    x += CHAR_W * len(PROMPT)
                    text = text[len(PROMPT):]
                d.text((x, y), text, font=fmb, fill=(255, 255, 255))
            else:
                x = x0
                for seg, c in colorize(text):
                    d.text((x, y), seg, font=fm, fill=c)
                    x += CHAR_W * len(seg)
        if scr.cursor and rows:
            last, _ = rows[-1]
            cx = x0 + CHAR_W * len(last)
            cy = y0 + (len(rows) - 1) * LINE_H
            d.rectangle([cx + 2, cy + 2, cx + CHAR_W, cy + LINE_H - 6], fill=(220, 220, 220))
        if scr.badge:
            bf = ImageFont.truetype(SANS_B, 30)
            t = f"⏩ {scr.badge}"
            bw = d.textlength(t, font=bf)
            d.rounded_rectangle([W - bw - 120, TERM_Y + 80, W - 80, TERM_Y + 130], 10, fill=(200, 60, 60, 230))
            d.text((W - bw - 100, TERM_Y + 87), t, font=bf, fill=(255, 255, 255))
    if caption:
        cf = ImageFont.truetype(SANS, 38)
        words, lines, cur = caption.split(), [], ""
        for w_ in words:
            t = (cur + " " + w_).strip()
            if d.textlength(t, font=cf) > W - 240:
                lines.append(cur)
                cur = w_
            else:
                cur = t
        lines.append(cur)
        lines = lines[-2:]
        top = H - 40 - len(lines) * 52 - 20
        d.rounded_rectangle([90, top, W - 90, H - 30], 14, fill=(0, 0, 0, 200))
        for i, ln in enumerate(lines):
            lw = d.textlength(ln, font=cf)
            d.text(((W - lw) / 2, top + 16 + i * 52), ln, font=cf, fill=(255, 255, 255))
    img.save(path, optimize=False, compress_level=1)


def split_captions(text: str, t0: float, dur: float):
    parts = [p.strip() for p in re.split(r"(?<=[.?!:])\s+", text) if p.strip()]
    # merge very short fragments into the next one
    merged: list[str] = []
    for p in parts:
        if merged and len(merged[-1]) < 30:
            merged[-1] += " " + p
        else:
            merged.append(p)
    total = sum(len(p) for p in merged)
    out, t = [], t0
    for p in merged:
        d = dur * len(p) / total
        out.append((t, t + d, p))
        t += d
    return out


# --------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=str(OUT / "qnx-portscan-demo.mp4"))
    ap.add_argument("--no-tts", action="store_true")
    args = ap.parse_args()
    OUT.mkdir(exist_ok=True)
    frames = OUT / "frames"
    shutil.rmtree(frames, ignore_errors=True)
    frames.mkdir()

    timeline: list[tuple[float, Screen]] = []   # screen changes
    captions: list[tuple[float, float, str]] = []
    audio: list[tuple[float, Path]] = []
    t = 0.0
    scr = Screen()
    n = 0
    log = []
    for label, steps in BEATS:
        for step in steps:
            n += 1
            wav = OUT / f"n{n:02d}.wav"
            adur = synth(step.say, wav, args.no_tts)
            start = t
            if step.card:
                scr = Screen(card=step.card)
                timeline.append((t, scr))
                vis = 0.0
            else:
                rows = [] if step.clear else list(scr.rows)
                if rows and rows[-1][1] == "cmd" and rows[-1][0] == PROMPT:
                    rows.pop()
                scr = Screen(rows=tuple(rows + [(PROMPT, "cmd")]), label=label)
                timeline.append((t, scr))
                tt = t + 0.5
                for cmd in step.cmds:
                    out = run_capture(cmd)
                    log.append(f"$ {cmd}\n{out}\n")
                    base = list(scr.rows[:-1])
                    full = PROMPT + cmd
                    for i in range(1, len(full) - len(PROMPT) + 1, 2):
                        typed = wrap(full[:len(PROMPT) + i], "cmd")
                        timeline.append((tt, Screen(rows=tuple(base + typed), label=label)))
                        tt += 2 / TYPE_CPS
                    typed = wrap(full, "cmd")
                    timeline.append((tt, Screen(rows=tuple(base + typed), label=label)))
                    tt += 0.35
                    outrows = []
                    for ln in (out.split("\n") if out else []):
                        outrows += wrap(ln, "out")
                    rows = base + typed + outrows + [(PROMPT, "cmd")]
                    scr = Screen(rows=tuple(rows), label=label,
                                 badge=step.badge if cmd.startswith("git clone") else None)
                    timeline.append((tt, scr))
                    tt += 0.9
                if step.hl:
                    scr = Screen(rows=scr.rows, label=label, hl=tuple(step.hl), badge=scr.badge)
                    timeline.append((tt, scr))
                vis = tt - t + 1.0
            dur = max(adur + 0.7, vis, step.min_s)
            audio.append((start + 0.25, wav))
            captions += split_captions(step.say, start + 0.25, adur)
            t = start + dur
    total = t + 0.5
    (OUT / "commands.log").write_text("\n".join(log))

    # merge screen changes and caption changes into keyframes
    cuts = sorted({round(x, 3) for x in [ts for ts, _ in timeline] + [c[0] for c in captions] +
                   [c[1] for c in captions] + [0.0]})
    cuts = [c for c in cuts if c < total] + [total]
    concat = []
    cache: dict = {}
    for i in range(len(cuts) - 1):
        a, b = cuts[i], cuts[i + 1]
        s = [sc for ts, sc in timeline if ts <= a + 1e-6]
        s = s[-1] if s else Screen()
        cap = next((c[2] for c in captions if c[0] <= a + 1e-6 < c[1]), "")
        key = (s, cap)
        if key not in cache:
            p = frames / f"f{len(cache):05d}.png"
            render(s, cap, p)
            cache[key] = p
        concat.append((cache[key], b - a))
    with open(OUT / "frames.txt", "w") as f:
        for p, d in concat:
            f.write(f"file '{p}'\nduration {d:.3f}\n")
        f.write(f"file '{concat[-1][0]}'\n")

    # narration track
    rate, chans, sw = 22050, 1, 2
    buf = bytearray(int(total * rate) * sw)
    if not args.no_tts:
        for st, wav in audio:
            with wave.open(str(wav)) as w:
                rate_w = w.getframerate()
                assert rate_w == rate and w.getnchannels() == 1 and w.getsampwidth() == 2
                data = w.readframes(w.getnframes())
            off = int(st * rate) * sw
            buf[off:off + len(data)] = data[: len(buf) - off]
    with wave.open(str(OUT / "narration.wav"), "wb") as w:
        w.setnchannels(chans); w.setsampwidth(sw); w.setframerate(rate)
        w.writeframes(bytes(buf))

    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", str(OUT / "frames.txt"),
           "-i", str(OUT / "narration.wav"), "-vf", f"fps={FPS},format=yuv420p", "-c:v", "libx264",
           "-preset", "medium", "-crf", "20", "-tune", "stillimage", "-c:a", "aac", "-b:a", "160k",
           "-ar", "44100", "-t", f"{total:.2f}", "-movflags", "+faststart", args.out]
    subprocess.run(cmd, check=True)
    print(f"wrote {args.out}: {total:.1f}s, {len(cache)} unique frames, cols={COLS} rows={ROWS}")


if __name__ == "__main__":
    main()
