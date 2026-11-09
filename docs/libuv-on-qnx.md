# Tutorial: from a portability report to libuv's tests running on QNX 8.0

This walks through a real port check: scan libuv with qnx-portscan, cross-build it with the
QNX SDP 8.0 toolchain, and run its test suite in a QEMU guest. Everything below was run
on 2026-11-09 against libuv commit `9e40720a` (x86_64 guest, KVM host, no target hardware).

## Reproduce it
You need the SDP in `~/qnx800` (see `qemu-headless.md`), cmake, QEMU, KVM, git and access to GitHub.
```sh
tools/qnx-ctest/examples/libuv/run.sh /tmp/qnx-libuv
```
It takes about five minutes and leaves `results.txt`, `junit.xml` and the guest's `vm/serial.log`.

## 1. What the scanner predicted
`docs/demo/libuv.md` reports `QNX042: dup3()` at `src/unix/core.c:1254`.
The first link of the test binaries fails on exactly that symbol:
```
core.c:(.text+0x22f6): undefined reference to `dup3'
```
`dup3` was the only undefined symbol in the library itself. The fix is the fallback libuv already has for
other platforms without `dup3`: `dup2` followed by setting close-on-exec. Note that this pair is not atomic.

The second link error was libuv's own build: its test binaries use `isfinite` and `isinf`,
which on QNX need `-lm`. The scanner looks at API use, not link lines, so it could not see this one.

Both changes are in `tools/qnx-ctest/examples/libuv/qnx-sdp8.patch` (two lines).

## 2. Running the tests
libuv's test runner refuses to start as root, and some tests assert permission errors, so the tests run as
`qnxuser` (`--user`). Its fixtures are carried with `--files`. One CTest entry per libuv test is generated from
`uv_run_tests_a --list` executed in the guest, so the list matches what was compiled for QNX.
Exit status 7 is libuv's "skipped", declared with `SKIP_RETURN_CODE`.

## 3. Result
| Outcome | Tests |
|---|---|
| passed | 413 |
| skipped (libuv's own `# SKIP`) | 43 |
| failed | 34 |
| total | 490 |

`run.sh` checks that every listed test was reported and none was left unexecuted.

Of the 43 skips, 22 are filesystem watching, which libuv does not support on this platform.
The others include tests for other operating systems and tests that depend on the run environment.

26 of the 34 failures report `EINVAL`, nearly all from the Unix-domain socket operations `uv_pipe_bind` and
`uv_pipe_connect`. The cause has not been established yet. The remaining 8 are `fs_chown`, `platform_output`,
`poll_duplex`, `poll_unidirectional`, `tcp_connect_timeout`, `udp_multicast_join`, and two that hit libuv's own
test timeout (`udp_mmsg`, `fs_write_alotof_bufs_with_offset`; the runner reports these as failed, as libuv exits non-zero).
None of these failures have been analysed as bugs in libuv or in QNX.
