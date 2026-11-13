# Codelab: port a Linux `timerfd` ticker to QNX 8.0 pulses and test it in QEMU

In this codelab you take a small periodic timer written for Linux, find out why it does not build for
QNX 8.0, rewrite it with QNX native timer pulses, and prove the port works by running its tests on a real
QNX 8.0 kernel in a headless QEMU guest. No QNX hardware is needed.

**You will learn**
- how a QNX timer delivers expirations as *pulses* through a channel, the QNX replacement for `timerfd`;
- how to bound a blocking kernel call with `TimerTimeout()`, the QNX replacement for a `poll()` timeout;
- how to cross-build a CMake project with the SDP 8.0 `qcc` toolchain;
- how to run its CTest tests inside a QNX 8.0 guest and get a JUnit report.

**Time:** about 30 minutes, plus a one-time SDP install. Everything below was run on 2026-11-13 on
Ubuntu 24.04 (x86_64, KVM) with QNX SDP 8.0 and `qcc` 12.2.0.

**Code:** [`tools/qnx-ctest/examples/timer-pulses/`](../tools/qnx-ctest/examples/timer-pulses/) —
`start/` is the Linux original, `src/` the QNX port, `tests/` the tests.

## 0. Before you start
You need:
- QNX SDP 8.0 installed in `~/qnx800` (a free non-commercial license from myQNX is enough).
  For a headless install without a desktop, follow [qemu-headless.md](qemu-headless.md), section *Install*.
- `cmake` 3.21+, `python3`, `qemu-system-x86_64`, and access to `/dev/kvm` (your user in the `kvm` group).
- Optionally the .NET 10 SDK, for the scanner in step 1.

Clone the repository and check the toolchain:
```sh
git clone https://github.com/efimov-agent/qnx-portscan && cd qnx-portscan
. ~/qnx800/qnxsdp-env.sh
qcc -V 2>&1 | head -3
```

## 1. Find what will not port
The starting point, [`start/ticker.c`](../tools/qnx-ctest/examples/timer-pulses/start/ticker.c), has three functions:
`ticker_start(period_ms)`, `ticker_wait_for(t, timeout_ms)` (1 on a tick, 0 on timeout, -1 on error)
and `ticker_stop(t)`. It uses a `timerfd` for the period and `poll()` for the timeout.

Try to compile it for QNX:
```sh
cd tools/qnx-ctest/examples/timer-pulses
qcc -Vgcc_ntox86_64 -c start/ticker.c
```
```
start/ticker.c:5:10: fatal error: sys/timerfd.h: No such file or directory
```
The compiler stops at the first problem. To see every Linux-only call at once, and what to use instead,
run qnx-portscan from the repository root:
```sh
dotnet run --project src/QnxPortScan.Cli -- scan tools/qnx-ctest/examples/timer-pulses
```
```
| QNX009 | Error | timerfd | 2 | 1 |
| QNX010 | Error | timerfd | 1 | 1 |
...
**Suggestion:** Create a POSIX timer with timer_create() and a SIGEV_PULSE sigevent, arm it with
timer_settime(), receive expirations with MsgReceivePulse().
...
- L5:11 **QNX010** `sys/timerfd.h` — `#include <sys/timerfd.h>`
- L15:13 **QNX009** `timerfd_create` — ...
- L20:24 **QNX009** `timerfd_settime` — ...
```
Only `start/` is reported: the finished port in `src/` is already clean.
(The scanner skips files that look like a platform backend, such as `*_linux.c` or a `linux/` directory.
That is why the starting file is called `start/ticker.c`.)

`poll()` itself exists on QNX, but it cannot wait on a timer, so the timeout needs a new mechanism too.

## 2. The QNX model in three objects
On Linux the kernel turns timer expirations into a readable file descriptor. On QNX the kernel delivers
them as **pulses**: small, non-blocking messages that carry an 8-bit code and a value.
A pulse travels to a **channel**, the endpoint a QNX thread receives messages on, through a
**connection** to that channel. A **sigevent** structure tells the timer what to send and where.

| Linux | QNX 8.0 |
|---|---|
| `timerfd_create()` | `ChannelCreate()` + `ConnectAttach()` + `timer_create()` with a `SIGEV_PULSE` event |
| `timerfd_settime()` | `timer_settime()` |
| `read()` on the timerfd | `MsgReceivePulse()` on the channel |
| `poll(..., timeout)` | `TimerTimeout(..., _NTO_TIMEOUT_RECEIVE, ...)` before the receive |
| `close()` | `timer_delete()` + `ConnectDetach()` + `ChannelDestroy()` |

## 3. Create the channel, connection and timer
Open [`src/ticker.c`](../tools/qnx-ctest/examples/timer-pulses/src/ticker.c). `ticker_start()` builds the three objects:
```c
t->chid = ChannelCreate(_NTO_CHF_PRIVATE);
t->coid = ConnectAttach(0, 0, t->chid, _NTO_SIDE_CHANNEL, 0);

struct sigevent ev;
SIGEV_PULSE_INIT(&ev, t->coid, SIGEV_PULSE_PRIO_INHERIT, TICK_CODE, 0);
timer_create(CLOCK_MONOTONIC, &ev, &t->timer);

struct itimerspec its = { .it_value = ms_to_timespec(period_ms), .it_interval = ms_to_timespec(period_ms) };
timer_settime(t->timer, 0, &its, NULL);
```
- `_NTO_CHF_PRIVATE` makes the channel private to this process; nobody else can connect to it.
- `ConnectAttach(0, 0, chid, ...)` connects to a channel in the same process (node 0, pid 0 = self).
  `_NTO_SIDE_CHANNEL` keeps the connection id out of the file-descriptor range, so it cannot collide with `dup2()` or `close()` on fds.
- `SIGEV_PULSE_INIT` fills the sigevent: send a pulse with code `TICK_CODE` on connection `coid`.
  Codes from `_PULSE_CODE_MINAVAIL` up are for applications; lower codes are reserved for the system.
- `SIGEV_PULSE_PRIO_INHERIT` lets the pulse take the priority of the thread that receives it.
  In a real-time design you would pass an explicit priority here instead.
- `CLOCK_MONOTONIC` is not affected when someone sets the wall clock, same as in the Linux version.

Each step can fail, so the function releases what it already created in reverse order (the `goto fail_*` chain).
`ticker_start(0)` sets `errno = EINVAL` before creating anything: a zero `it_value` would disarm the timer and the
caller would wait forever.

## 4. Wait for a tick, with a timeout
On Linux, `poll()` waited for the timerfd with a timeout. On QNX, a thread blocks in `MsgReceivePulse()` on the
channel, and a kernel timeout bounds that block:
```c
uint64_t ns = (uint64_t)timeout_ms * 1000000u;
TimerTimeout(CLOCK_MONOTONIC, _NTO_TIMEOUT_RECEIVE, NULL, &ns, NULL);

struct _pulse pulse;
if (MsgReceivePulse(t->chid, &pulse, sizeof pulse, NULL) == -1)
    return errno == ETIMEDOUT ? 0 : -1;
if (pulse.code == TICK_CODE)
    return 1;
```
`TimerTimeout()` applies to the **next** kernel call of this thread that enters one of the listed blocking
states, here *receive-blocked*. If the timeout passes first, the call fails with `ETIMEDOUT`. The kernel clears
the timeout after that call, so it is armed again on every loop iteration.

One difference from Linux: `read()` on a timerfd returns how many periods expired since the last read. This port
only reports *that* a tick arrived; if you need the count of missed periods, keep your own clock comparison.

## 5. Describe the tests in CMake
[`CMakeLists.txt`](../tools/qnx-ctest/examples/timer-pulses/CMakeLists.txt) builds the ticker as a static library
and registers three tests:

| Test | What it checks |
|---|---|
| `ticks` | 10 ticks of 20 ms arrive and take between 195 ms and 1 s |
| `timeout` | with a 300 ms period, a 50 ms wait returns 0 after at least 45 ms; a 1 s wait returns the tick |
| `bad_period` | `ticker_start(0)` returns `NULL` with `errno == EINVAL` |

The library is static on purpose: the test runner copies the test executables into the guest, not shared
libraries built by the project. Each test has a CTest `TIMEOUT` of 10 seconds, so a ticker that never fires fails
the test instead of hanging the run.

Cross-build it with the SDP toolchain file:
```sh
. ~/qnx800/qnxsdp-env.sh
cmake -S . -B /tmp/qnx-timer-pulses -DCMAKE_TOOLCHAIN_FILE=$PWD/../../qnx-x86_64.cmake
cmake --build /tmp/qnx-timer-pulses
file /tmp/qnx-timer-pulses/test_ticks
```
`file` should report an `ELF 64-bit LSB pie executable, x86-64` with the interpreter `/usr/lib/ldqnx-64.so.2`:
a QNX binary, which you cannot run on the Linux host.

## 6. Run the tests on QNX 8.0
[`qnx_ctest.py`](../tools/qnx-ctest/qnx_ctest.py) reads the test list from CTest, builds a small QNX 8.0 image with
`mkqnximage` that contains the executables, boots it in QEMU without a display or network, runs each test and
collects the results from the serial console:
```sh
python3 ../../qnx_ctest.py /tmp/qnx-timer-pulses --junit /tmp/qnx-timer-pulses/junit.xml
```
(Run it through `sg kvm -c "..."` if your current shell was started before you joined the `kvm` group.)
```
PASSED   ticks
PASSED   timeout
PASSED   bad_period
3 passed of 3 on QNX; serial log: /home/you/.cache/qnx-ctest/serial.log
```
On the machine above, building the image, booting and running took about 35–50 seconds. The JUnit file keeps each
test's output, so you can see the measured times:
```
10 ticks in 201 ms
first=0 after 51 ms, second=1
ticker=0 errno=22
```
Steps 5 and 6 together are in one script: `./run.sh [BUILD_DIR]`.

The exit code of `qnx_ctest.py` is 0 only if every test passed, so the same command can gate a CI job on a
self-hosted runner that has the SDP installed.

## 7. If something goes wrong
| Symptom | Cause and fix |
|---|---|
| `qcc: command not found` | `qnxsdp-env.sh` was not sourced in this shell. |
| `Could not access KVM kernel module` / permission denied on `/dev/kvm` | Add yourself to the `kvm` group and log in again, or use `sg kvm -c "..."`. |
| A test fails at once and the serial log shows a loader error | A shared library of your project was not copied into the guest. Link it statically. |
| A test is reported as `timeout` | No tick arrived: check that `it_value` is not zero and that the sigevent uses the connection id (`coid`), not the channel id (`chid`). |
| `ticks` fails on a heavily loaded host | The 195 ms lower bound is strict, the upper bound is not. Run with fewer parallel jobs on the host. |

## 8. Where to go next
- Receive the pulses in the same `MsgReceive()` loop as your other messages: a resource manager or server can
  handle timer ticks and client requests on one channel, with no extra thread.
- Give the pulse an explicit priority instead of `SIGEV_PULSE_PRIO_INHERIT` so tick handling preempts lower-priority work.
- Scan a larger project with `qnx-portscan` and follow the suggestions it gives for `epoll`, `eventfd` and `inotify`:
  they lead to the same channel-and-pulse model.
- A larger example of the test runner, the libuv test suite on QNX 8.0: [libuv-on-qnx.md](libuv-on-qnx.md).

## References
- [timer_create()](https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/timer_create.html),
  [sigevent](https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/sigevent.html),
  [struct _pulse](https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/_pulse.html)
- [ChannelCreate()](https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/c/channelcreate.html),
  [ConnectAttach()](https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/c/connectattach.html),
  [MsgReceivePulse()](https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/m/msgreceivepulse.html),
  [TimerTimeout()](https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/timertimeout.html)
