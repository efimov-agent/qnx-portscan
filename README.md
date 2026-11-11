# qnx-portscan

Static scanner that finds Linux-only APIs in C/C++ code and tells you what to use on **QNX Neutrino (SDP 8.0)** instead.
Runs on the host (Linux/macOS/Windows, .NET 10) — no QNX SDP needed to get a porting estimate.

```
dotnet run --project src/QnxPortScan.Cli -- scan ~/src/libuv --exclude test/
dotnet run --project src/QnxPortScan.Cli -- scan . --format sarif --out qnx.sarif --fail-on error
dotnet run --project src/QnxPortScan.Cli -- rules
```

## What it checks
56 rules (`tools/gen_rules.py` → `Rules/rules.json`) checked against the QNX SDP 8.0 C Library Reference (QNX056 was measured in an SDP 8.0 guest):
epoll, kqueue, eventfd, signalfd, timerfd, inotify (libfsnotify caveats), prctl, sendfile/splice, clone/namespaces,
raw `syscall()`, sysinfo, CPU affinity (`cpu_set_t`), `sched_getcpu`, mremap, memfd_create, getrandom, getauxval,
glibc backtrace, ptrace, io_uring, `<mntent.h>`, statfs, dup3, O_PATH, MAP_POPULATE, Linux `/proc` and `/sys` paths,
GNU libc extensions, `itoa` (removed in 8.0), `<linux/*.h>` headers. Each rule has a suggested QNX replacement
(ionotify + pulses, `timer_create(SIGEV_PULSE)`, `ThreadCtl(_NTO_TCTL_RUNMASK)`, `shm_open(SHM_ANON)`, `/proc/<pid>/exefile`, …)
and links to the official docs.

Severity: **error** — not documented in QNX 8.0; **warning** — exists with caveats, or absence not individually verified; **note** — not confirmed either way.

## Keeping noise down
- Comments and string literals are lexed out (raw strings, digit separators handled); path rules look only inside literals.
- Code in branches for other platforms is skipped: `#ifdef __linux__`, `#if defined(__APPLE__) || defined(_WIN32)`,
  the `#else` of `#ifndef __linux__`, feature macros like `HAVE_EPOLL`, and `#if 0`.
- Platform backend files (`ae_epoll.c`, `src/unix/linux.c`, `kqueue.cpp`, `win/…`) are skipped — the port needs its own backend.
- `--include-guarded` shows all of these as notes.
- Feature tests (`#if defined(SOCK_CLOEXEC)`) are never reported.

## Output
- Markdown: summary per rule, "how to port" section, findings per file.
- SARIF 2.1.0 for GitHub code scanning / VS Code SARIF viewer.
- With `--out`, the report goes to the file and a one-line `Wrote <path>` summary goes to stdout.
- Exit code: 0 ok, 1 findings at `--fail-on` level, 2 usage error, 3 I/O or rules-file error.

## Limits
Text-level analysis, not a compiler: project-specific config macros (e.g. `HAVE_BACKTRACE`) are not evaluated,
and "not documented" is not proof that a symbol is absent from SDP headers. Confirm with `qcc` on a real SDP.
A backslash followed by trailing spaces before the newline is not treated as a line continuation
(GCC accepts it with a warning), so such a continued `//` comment or directive is read as ending there.

## Build and run on QNX 8.0
The scanner itself is a host tool (.NET 10 SDK on Linux/macOS/Windows; `./check.sh` builds and tests it).
Its findings are verified on a real QNX target:

- **QNX version:** QNX Software Development Platform (SDP) 8.0, QNX Neutrino 8.0.
- **Target:** QEMU x86_64 guest built with `mkqnximage` (KVM, headless) — no hardware needed;
  setup on Ubuntu 24.04: [docs/qemu-headless.md](docs/qemu-headless.md).
- **Toolchain:** SDP 8.0 `qcc -Vgcc_ntox86_64` via CMake toolchain file
  [tools/qnx-ctest/qnx-x86_64.cmake](tools/qnx-ctest/qnx-x86_64.cmake).
- **Run:** `tools/qnx-ctest/examples/libuv/run.sh /tmp/qnx-libuv` cross-builds libuv, copies the tests into the guest
  and runs them through the `tools/qnx-ctest` runner — walkthrough and results: [docs/libuv-on-qnx.md](docs/libuv-on-qnx.md).

## Develop
`./check.sh` — build, 77 unit tests, CLI smoke test.

## License
MIT — see [LICENSE](LICENSE). QNX SDP itself is not included and is licensed separately by BlackBerry QNX.
