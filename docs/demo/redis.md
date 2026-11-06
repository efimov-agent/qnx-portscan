# QNX portability report: redis

Files scanned: **856** · Findings: **59** (errors 44, warnings 7, notes 8)

## Summary by rule

| Rule | Severity | Title | Hits | Files |
|---|---|---|---:|---:|
| QNX018 | Error | raw syscall() | 10 | 2 |
| QNX024 | Error | cpu_set_t macros | 10 | 3 |
| QNX049 | Error | /proc/<pid> Linux files | 7 | 4 |
| QNX023 | Error | CPU affinity | 6 | 2 |
| QNX046 | Error | sysfs | 3 | 1 |
| QNX033 | Error | backtrace() | 3 | 1 |
| QNX019 | Error | raw syscall() | 1 | 1 |
| QNX025 | Error | sched_getcpu() | 1 | 1 |
| QNX034 | Error | backtrace() | 1 | 1 |
| QNX006 | Error | eventfd | 1 | 1 |
| QNX005 | Error | eventfd | 1 | 1 |
| QNX054 | Warning | Other Linux /proc files | 7 | 2 |
| QNX029 | Note | MAP_ANONYMOUS | 3 | 3 |
| QNX045 | Note | GNU strerror_r | 3 | 3 |
| QNX044 | Note | SOCK_NONBLOCK/SOCK_CLOEXEC | 2 | 1 |

## How to port

### QNX005: eventfd

eventfd: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** For a poll()-based loop use a self-pipe via pipe2(O_NONBLOCK|O_CLOEXEC); in a channel-based design send a pulse with MsgSendPulse().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/pipe2.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/m/msgsendpulse.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/lib-e.html>

### QNX006: eventfd

<sys/eventfd.h>: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Replace with a self-pipe (pipe2) or pulses (MsgSendPulse).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/pipe2.html>

### QNX018: raw syscall()

syscall(): Not documented in the QNX SDP 8.0 C Library Reference. Linux syscall numbers do not apply to QNX.

**Suggestion:** Call the libc wrapper, or the QNX kernel call (MsgSend, ThreadCtl, ...).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/lib-s.html>

### QNX019: raw syscall()

<sys/syscall.h> (SYS_* numbers): Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Drop it; use libc wrappers. For thread id use gettid() from <process.h>.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/g/gettid.html>

### QNX023: CPU affinity

Linux affinity API: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use ThreadCtl(_NTO_TCTL_RUNMASK...) from the thread itself (the mask must match a CPU cluster in 8.0), pthread_cluster_set(), or posix_spawnattr_setrunmask() for children.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/threadctl.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/pthread_cluster_set.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/posix_spawnattr_setrunmask.html>

### QNX024: cpu_set_t macros

cpu_set_t / CPU_* macros: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use a uint64_t run mask with ThreadCtl(_NTO_TCTL_RUNMASK).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/threadctl.html>

### QNX025: sched_getcpu()

sched_getcpu(): Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use SchedGetCpuNum() from <sys/neutrino.h>.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/schedgetcpunum.html>

### QNX029: MAP_ANONYMOUS

Only MAP_ANON is documented for QNX 8.0 mmap(); a MAP_ANONYMOUS alias is not confirmed.

**Suggestion:** Prefer MAP_ANON (with fd = NOFD / -1) for portability.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/m/mmap.html>

### QNX033: backtrace()

glibc backtrace(): Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** QNX ships libbacktrace (bt_* API) but marks it unsupported; prefer core dumps + gdb.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.technotes/topic/backtrace.html>

### QNX034: backtrace()

<execinfo.h>: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** See QNX033.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.technotes/topic/backtrace.html>

### QNX044: SOCK_NONBLOCK/SOCK_CLOEXEC

io-sock follows FreeBSD 14.3 socket(2), which has these flags, but QNX does not confirm them explicitly.

**Suggestion:** If the build fails, use fcntl(O_NONBLOCK)/ioctl(FIONBIO) and fcntl(FD_CLOEXEC) after socket().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.io_sock/topic/socket_api.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.devctl/topic/fio/fionbio.html>

### QNX045: GNU strerror_r

_GNU_SOURCE suggests reliance on GNU extensions; e.g. QNX has only the POSIX int strerror_r(), not the GNU char* variant.

**Suggestion:** Review GNU-only calls in this file (strerror_r return value, qsort_r, program_invocation_name, ...).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/strerror_r.html>

### QNX046: sysfs

QNX has no /sys filesystem.

**Suggestion:** Query the information through the system page, devctl() or io-sock sysctl() (network).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.prog/topic/process_proc_filesystem.html>

### QNX049: /proc/<pid> Linux files

Per-process Linux procfs files are not available; QNX offers as, cmdline, ctl, exefile, mappings, pmap and vmstat.

**Suggestion:** Memory map: /proc/<pid>/mappings or pmap, or devctl DCMD_PROC_MAPINFO; process info: devctl DCMD_PROC_INFO.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.prog/topic/process_proc_filesystem.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.prog/topic/process_DCMD_PROC_MAPINFO.html>

### QNX054: Other Linux /proc files

QNX documents only per-process /proc entries (as, cmdline, ctl, exefile, mappings, pmap, vmstat); this Linux system file is not documented (not individually verified).

**Suggestion:** Expect it to be missing; get the data from the system page, devctl() or io-sock sysctl() (network).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.prog/topic/process_proc_filesystem.html>

## Findings by file

### `deps/jemalloc/include/jemalloc/internal/jemalloc_internal_decls.h`

- L23:15 **QNX019** `sys/syscall.h` — `#    include <sys/syscall.h>`

### `deps/jemalloc/include/jemalloc/internal/jemalloc_internal_inlines_a.h`

- L16:25 **QNX025** `sched_getcpu` — `return (malloc_cpuid_t)sched_getcpu();`

### `deps/jemalloc/include/jemalloc/internal/malloc_io.h`

- L80:16 **QNX018** `syscall` — `long result = syscall(SYS_write, fd, buf, count);`
- L94:16 **QNX018** `syscall` — `long result = syscall(SYS_read, fd, buf, count);`

### `deps/jemalloc/src/background_thread.c`

- L84:2 **QNX024** `cpu_set_t` — `cpu_set_t cpuset;`
- L94:2 **QNX024** `CPU_ZERO` — `CPU_ZERO(&cpuset);`
- L95:2 **QNX024** `CPU_SET` — `CPU_SET(cpu, &cpuset);`
- L101:10 **QNX023** `sched_setaffinity` — `return (sched_setaffinity(0, sizeof(cpu_set_t), &cpuset) != 0);`
- L101:38 **QNX024** `cpu_set_t` — `return (sched_setaffinity(0, sizeof(cpu_set_t), &cpuset) != 0);`
- L104:12 **QNX023** `pthread_setaffinity_np` — `int ret = pthread_setaffinity_np(pthread_self(), sizeof(cpuset_t),`

### `deps/jemalloc/src/jemalloc.c`

- L739:3 **QNX024** `cpu_set_t` — `cpu_set_t set;`
- L742:3 **QNX023** `sched_getaffinity` — `sched_getaffinity(0, sizeof(set), &set);`
- L744:3 **QNX023** `pthread_getaffinity_np` — `pthread_getaffinity_np(pthread_self(), sizeof(set), &set);`
- L746:12 **QNX024** `CPU_COUNT` — `result = CPU_COUNT(&set);`
- L776:2 **QNX024** `cpu_set_t` — `cpu_set_t set;`
- L779:2 **QNX023** `sched_getaffinity` — `sched_getaffinity(0, sizeof(set), &set);`
- L781:2 **QNX023** `pthread_getaffinity_np` — `pthread_getaffinity_np(pthread_self(), sizeof(set), &set);`
- L783:22 **QNX024** `CPU_COUNT` — `long cpu_affinity = CPU_COUNT(&set);`

### `deps/jemalloc/src/pages.c`

- L68:18 **QNX029** `MAP_ANONYMOUS` — `MAP_PRIVATE|MAP_ANONYMOUS, -1, 0);`
- L627:13 **QNX018** `syscall` — `fd = (int)syscall(SYS_open, "/proc/sys/vm/overcommit_memory", O_RDONLY |`
- L627:32 **QNX054** `/proc/sys/` — `fd = (int)syscall(SYS_open, "/proc/sys/vm/overcommit_memory", O_RDONLY |`
- L630:13 **QNX018** `syscall` — `fd = (int)syscall(SYS_open, "/proc/sys/vm/overcommit_memory", O_RDONLY);`
- L630:32 **QNX054** `/proc/sys/` — `fd = (int)syscall(SYS_open, "/proc/sys/vm/overcommit_memory", O_RDONLY);`
- L637:13 **QNX018** `syscall` — `fd = (int)syscall(SYS_openat,`
- L638:15 **QNX054** `/proc/sys/` — `AT_FDCWD, "/proc/sys/vm/overcommit_memory", O_RDONLY | O_CLOEXEC);`
- L640:13 **QNX018** `syscall` — `fd = (int)syscall(SYS_openat,`
- L641:15 **QNX054** `/proc/sys/` — `AT_FDCWD, "/proc/sys/vm/overcommit_memory", O_RDONLY);`
- L648:14 **QNX054** `/proc/sys/` — `fd = open("/proc/sys/vm/overcommit_memory", O_RDONLY | O_CLOEXEC);`
- L650:14 **QNX054** `/proc/sys/` — `fd = open("/proc/sys/vm/overcommit_memory", O_RDONLY);`
- L663:2 **QNX018** `syscall` — `syscall(SYS_close, fd);`
- L716:16 **QNX018** `syscall` — `int fd = (int)syscall(SYS_open,`
- L717:7 **QNX046** `/sys/` — `"/sys/kernel/mm/transparent_hugepage/enabled", O_RDONLY);`
- L719:16 **QNX018** `syscall` — `int fd = (int)syscall(SYS_openat,`
- L720:18 **QNX046** `/sys/` — `AT_FDCWD, "/sys/kernel/mm/transparent_hugepage/enabled", O_RDONLY);`
- L722:17 **QNX046** `/sys/` — `int fd = open("/sys/kernel/mm/transparent_hugepage/enabled", O_RDONLY);`
- L730:2 **QNX018** `syscall` — `syscall(SYS_close, fd);`

### `deps/jemalloc/src/prof_sys.c`

- L461:33 **QNX049** `/proc/%d/task/` — `mfd = prof_open_maps_internal("/proc/%d/task/%d/maps", pid, pid);`
- L463:34 **QNX049** `/proc/%d/maps` — `mfd = prof_open_maps_internal("/proc/%d/maps", pid);`

### `modules/vector-sets/fastjson_test.c`

- L84:38 **QNX029** `MAP_ANONYMOUS` — `MAP_PRIVATE | MAP_ANONYMOUS,`

### `src/anet.c`

- L615:35 **QNX044** `SOCK_NONBLOCK` — `fd = accept4(s, sa, len,  SOCK_NONBLOCK | SOCK_CLOEXEC);`
- L615:51 **QNX044** `SOCK_CLOEXEC` — `fd = accept4(s, sa, len,  SOCK_NONBLOCK | SOCK_CLOEXEC);`

### `src/debug.c`

- L38:11 **QNX034** `execinfo.h` — `#include <execinfo.h>`
- L2430:22 **QNX033** `backtrace` — `int trace_size = backtrace(trace, BACKTRACE_MAX_SIZE);`
- L2434:5 **QNX033** `backtrace_symbols_fd` — `backtrace_symbols_fd(trace+uplevel, trace_size-uplevel, fd);`
- L2459:9 **QNX033** `backtrace_symbols_fd` — `backtrace_symbols_fd(&eip, 1, fd);`
- L2611:17 **QNX049** `/proc/self/maps` — `fp = fopen("/proc/self/maps","r");`

### `src/eventnotifier.c`

- L17:11 **QNX006** `sys/eventfd.h` — `#include <sys/eventfd.h>`
- L28:20 **QNX005** `eventfd` — `if ((en->efd = eventfd(0, EFD_NONBLOCK| EFD_CLOEXEC)) != -1) {`

### `src/server.c`

- L2716:24 **QNX049** `/proc/self/oom_score_adj` — `fd = open("/proc/self/oom_score_adj", O_RDONLY);`
- L2741:16 **QNX049** `/proc/self/oom_score_adj` — `fd = open("/proc/self/oom_score_adj", O_WRONLY);`
- L2846:23 **QNX054** `/proc/sys/` — `FILE *fp = fopen("/proc/sys/net/core/somaxconn","r");`

### `src/setcpuaffinity.c`

- L27:9 **QNX045** `_GNU_SOURCE` — `#define _GNU_SOURCE`
- L90:5 **QNX024** `CPU_ZERO` — `CPU_ZERO(&cpuset);`
- L129:13 **QNX024** `CPU_SET` — `CPU_SET(a, &cpuset);`

### `src/setproctitle.c`

- L29:9 **QNX045** `_GNU_SOURCE` — `#define _GNU_SOURCE`

### `src/zmalloc.c`

- L737:21 **QNX049** `/proc/self/stat` — `if ((fd = open("/proc/self/stat",O_RDONLY)) == -1) return 0;`
- L1139:21 **QNX049** `/proc/self/smaps` — `fp = fopen("/proc/self/smaps","r");`

### `src/zmalloc.h`

- L68:9 **QNX045** `_GNU_SOURCE` — `#define _GNU_SOURCE`

### `tests/modules/crash.c`

- L29:57 **QNX029** `MAP_ANONYMOUS` — `char *p = mmap(NULL, 4096, PROT_READ, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);`
