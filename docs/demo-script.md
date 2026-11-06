# Demo recording storyboard — qnx-portscan

Script for the Devpost demo video (target 2–3 min). All commands are real and
reproducible; numbers below match `docs/demo/` reports (default settings,
`git clone --depth 1`). Record a terminal at 100×30, large font, light or
dark theme — no personal paths on screen (use `~/demo`).

## Beat 0 — hook (0:00–0:15)
Narration: "Porting a C/C++ codebase to QNX means hunting down every Linux-only
syscall by hand. qnx-portscan does that pass for you — it reads QNX 8.0's own
C Library Reference and flags each call site a port has to revisit, with the
documented QNX replacement."

Screen: title card or just the README header.

## Beat 1 — what it is (0:15–0:35)
Command:
```
qnx-portscan --help
qnx-portscan rules | head -20
```
Narration: "55 rules, all generated from the QNX SDP 8.0 docs — not guesswork.
Each one carries a severity, a suggested replacement, and a link back to the
canonical QNX reference page." Highlight: `rules` output shows id/severity/title.

## Beat 2 — scan a real project (0:35–1:20)
Command:
```
git clone --depth 1 https://github.com/libuv/libuv ~/demo/libuv
qnx-portscan scan ~/demo/libuv --format both --out ~/demo/libuv-report
```
Narration: "Point it at libuv — a library that already has partial QNX support.
378 files, scanned in a second." Show the console summary: **25 findings**,
19 errors. Then open the Markdown report.

Highlight these lines (from `docs/demo/libuv.md`):
- `QNX004 kqueue` → "Use ionotify() + pulses, or poll()."
- `QNX023/QNX024 CPU affinity / cpu_set_t` → QNX replacement.
- `QNX021 sysinfo()` → "system page (_syspage_ptr) or devctl DCMD_PROC_SYSINFO".

Narration: "Every finding links straight to the QNX doc page, so a developer
goes from 'what do I do here' to the answer in one click."

## Beat 3 — it understands C, not just grep (1:20–1:50)
Command (use `check.sh`'s fixture or a tiny file):
```
cat ~/demo/loop.c        # contains epoll_wait in a comment + #ifdef __linux__
qnx-portscan scan ~/demo --format sarif --out ~/demo/r
```
Narration: "It's not grep. Calls inside comments are ignored; code already
guarded by `#ifdef __QNX__` is suppressed by default; `__linux__`-only blocks
are flagged as foreign. That keeps the noise down." Show SARIF opening in an
editor / CI annotation if easy.

## Beat 4 — fits CI (1:50–2:20)
Command:
```
qnx-portscan scan ~/demo/libuv --fail-on error ; echo "exit=$?"
```
Narration: "`--fail-on error` returns a non-zero exit, so it drops into any
pipeline as a portability gate. SARIF 2.1.0 output means GitHub code scanning
renders the findings inline on the PR."

## Beat 5 — breadth (2:20–2:45)
Screen: the `docs/demo/README.md` table.
Narration: "Across four well-known projects — libuv, redis, mosquitto, libzmq —
it surfaced 109 concrete port tasks, each with a documented QNX path. redis
alone: 59 findings." End on the repo URL.

## Recording notes
- Pre-clone the four repos off-camera; on-camera run only libuv for pace.
- Pre-build Release so there's no first-run compile pause: `dotnet build -c Release`.
- Alias: `alias qnx-portscan='dotnet src/QnxPortScan.Cli/bin/Release/net10.0/qnx-portscan'`.
- Keep total under 3 min; Devpost favours tight demos.
- No QNX hardware/myQNX login is needed for this demo — the tool analyses source,
  it does not compile on target. (On-target compile belongs to a later milestone.)
