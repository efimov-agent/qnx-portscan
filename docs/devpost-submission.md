# qnx-portscan — Devpost submission draft

> Draft text for the QNX Everywhere Hackathon (Developer Tooling / Ports-adjacent).
> Not yet submitted — submission requires myQNX + Devpost accounts (owner registers; reCAPTCHA cannot be automated).
> Fill the links marked `<…>` before submitting.

## Elevator pitch (≤ 200 chars)
Static scanner that finds Linux-only APIs in C/C++ code and tells you the QNX Neutrino 8.0 replacement for each — so you get a porting estimate in seconds, no SDP install required.

## Inspiration
Porting a mature Linux C/C++ codebase to QNX Neutrino starts with the same tedious question every time: *which calls won't compile, and what do I use instead?* Today you find out the slow way — run `qcc`, read the error, search the Porting Guide, repeat. We wanted the answer up front, as a report, before anyone touches an SDP.

## What it does
`qnx-portscan` scans a C/C++ source tree on your host (Linux/macOS/Windows, .NET 10) and reports every Linux-only API, header, and `/proc`-style path that QNX Neutrino SDP 8.0 does not provide — each with a concrete QNX replacement and a link to the official documentation.

- **55 rules** covering `epoll`, `kqueue`, `eventfd`, `signalfd`, `timerfd`, `inotify`, `prctl`, `sendfile`/`splice`, `clone`/namespaces, raw `syscall()`, CPU affinity (`cpu_set_t`), `memfd_create`, `getrandom`, glibc `backtrace`, `io_uring`, Linux `/proc` and `/sys` paths, GNU libc extensions, `itoa` (removed in 8.0), `<linux/*.h>` headers, and more.
- **Actionable fixes**, not just flags: ionotify + pulses for `epoll`/`inotify`, `timer_create(SIGEV_PULSE)` for `timerfd`, `ThreadCtl(_NTO_TCTL_RUNMASK)` for affinity, `shm_open(SHM_ANON)` for `memfd_create`, `/proc/<pid>/exefile` for Linux procfs, …
- **Low noise**: comments and string literals are lexed out; code already guarded for other platforms (`#ifdef __linux__`, `#if 0`, `HAVE_EPOLL`, platform backend files like `ae_epoll.c`) is skipped by default and shown only with `--include-guarded`. Feature tests (`#if defined(SOCK_CLOEXEC)`) are never reported.
- **Output**: Markdown (summary, "how to port", per-file findings) and SARIF 2.1.0 for GitHub code scanning / the VS Code SARIF viewer. Exit code gates CI (`--fail-on error`).

Run it:
```
dotnet run --project src/QnxPortScan.Cli -- scan ~/src/libuv --exclude test/
dotnet run --project src/QnxPortScan.Cli -- scan . --format sarif --out qnx.sarif --fail-on error
```

## Demo — four real projects
Run against four well-known open-source codebases (default settings, shallow clone):

| Project | Files | Findings | Errors |
|---|---:|---:|---:|
| libuv | 378 | 25 | 19 |
| redis | 856 | 59 | 44 |
| mosquitto | 605 | 7 | 2 |
| libzmq | 465 | 18 | 13 |

Reports are generated, not hand-edited (see `docs/demo/`). libuv and mosquitto already carry partial QNX support, which is why their counts are lower — the scanner still surfaces the Linux-only call sites a port must revisit. Findings are substantive: `epoll`/`kqueue` event loops, `cpu_set_t` affinity, raw `syscall()`, glibc `backtrace()`, `/proc` reads, `getrandom`.

## How we built it
- **.NET 10 / C#**, clean architecture: a line-and-token lexer that strips comments and string literals (handles raw strings and digit separators), a preprocessor-aware guard tracker, a rules engine, and Markdown/SARIF emitters.
- **Rules as data**: `tools/gen_rules.py` generates `Rules/rules.json` deterministically (byte-identical on re-run), each rule carrying severity, a QNX replacement, and a docs link, written from the public QNX SDP 8.0 C Library Reference and Porting Guide.
- **58 xUnit tests** plus a CLI smoke test, all wired into `./check.sh`. Nullable reference types enabled throughout; no runtime dependency on a QNX SDP.

## Challenges we ran into
- **Separating real Linux dependencies from platform-guarded code.** A naive grep over redis or libuv is almost all false positives — the signal lives inside `#ifdef`/backend-file noise. Getting the guard model right (and offering `--include-guarded` to see everything) was most of the work.
- **Honesty about confidence.** "Not documented in SDP 8.0" is not proof a symbol is absent. We encoded that as severity (`error` / `warning` / `note`) and say so plainly in the report, rather than overclaiming.

## Accomplishments we're proud of
A tool that runs **without the SDP installed** and still gives a credible, reproducible porting estimate for large real-world codebases — with fixes developers can act on, not just a wall of warnings.

## What we learned
How much of a Linux→QNX port is mechanical and knowable in advance, and how QNX's message-passing primitives (pulses, `MsgSend`, ionotify) map cleanly onto the Linux readiness/eventing APIs people reach for by habit.

## What's next
- A ctest runner that executes the ported code headless on **QEMU + SDP 8.0** (bonus tutorial: building a headless SDP image on Ubuntu 24.04).
- More rules as we port real packages; community-contributed rule entries.

## Built with
`.net-10` `c#` `c` `c++` `qnx-neutrino` `sarif` `static-analysis` `python`

## Links
- Repository: `<github-repo-url>`
- Demo reports: `docs/demo/`
- Video: `<youtube-url>`
