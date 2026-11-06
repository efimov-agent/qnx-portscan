# QNX portability report: nginx

Files scanned: **411** · Findings: **27** (errors 25, warnings 0, notes 2)

## Summary by rule

| Rule | Severity | Title | Hits | Files |
|---|---|---|---:|---:|
| QNX024 | Error | cpu_set_t macros | 11 | 3 |
| QNX001 | Error | Linux kernel headers | 4 | 2 |
| QNX018 | Error | raw syscall() | 3 | 3 |
| QNX040 | Error | statfs() | 2 | 1 |
| QNX013 | Error | prctl | 2 | 1 |
| QNX043 | Error | O_PATH | 1 | 1 |
| QNX023 | Error | CPU affinity | 1 | 1 |
| QNX050 | Error | Linux GNU extensions | 1 | 1 |
| QNX044 | Note | SOCK_NONBLOCK/SOCK_CLOEXEC | 1 | 1 |
| QNX045 | Note | GNU strerror_r | 1 | 1 |

## How to port

### QNX001: Linux kernel headers

<linux/...> headers expose the Linux kernel ABI; no such headers are documented for QNX SDP 8.0.

**Suggestion:** Guard with #ifdef __linux__ and provide a QNX path: device control goes through devctl() or a resource manager.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/d/devctl.html>

### QNX013: prctl

prctl: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** PR_SET_NAME -> pthread_setname_np() or ThreadCtl(_NTO_TCTL_NAME). For privileges see procmgr_ability(). Other options have no direct equivalent.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/pthread_setname_np.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/threadctl.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/procmgr_ability.html>

### QNX018: raw syscall()

syscall(): Not documented in the QNX SDP 8.0 C Library Reference. Linux syscall numbers do not apply to QNX.

**Suggestion:** Call the libc wrapper, or the QNX kernel call (MsgSend, ThreadCtl, ...).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/lib-s.html>

### QNX023: CPU affinity

Linux affinity API: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use ThreadCtl(_NTO_TCTL_RUNMASK...) from the thread itself (the mask must match a CPU cluster in 8.0), pthread_cluster_set(), or posix_spawnattr_setrunmask() for children.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/threadctl.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/pthread_cluster_set.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/posix_spawnattr_setrunmask.html>

### QNX024: cpu_set_t macros

cpu_set_t / CPU_* macros: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use a uint64_t run mask with ThreadCtl(_NTO_TCTL_RUNMASK).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/threadctl.html>

### QNX040: statfs()

statfs/fstatfs: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use POSIX statvfs()/fstatvfs().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/statvfs.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/f/fstatvfs.html>

### QNX043: O_PATH

O_PATH is not among the documented open() flags on QNX 8.0.

**Suggestion:** Open normally (O_RDONLY | O_DIRECTORY where applicable).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/o/open.html>

### QNX044: SOCK_NONBLOCK/SOCK_CLOEXEC

io-sock follows FreeBSD 14.3 socket(2), which has these flags, but QNX does not confirm them explicitly.

**Suggestion:** If the build fails, use fcntl(O_NONBLOCK)/ioctl(FIONBIO) and fcntl(FD_CLOEXEC) after socket().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.io_sock/topic/socket_api.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.devctl/topic/fio/fionbio.html>

### QNX045: GNU strerror_r

_GNU_SOURCE suggests reliance on GNU extensions; e.g. QNX has only the POSIX int strerror_r(), not the GNU char* variant.

**Suggestion:** Review GNU-only calls in this file (strerror_r return value, qsort_r, program_invocation_name, ...).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/strerror_r.html>

### QNX050: Linux GNU extensions

GNU/Linux libc extension: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** qsort_r -> qsort() with TLS context; getdents -> readdir(); syncfs -> fsync()/sync(); fallocate -> posix_fallocate(); pthread_tryjoin_np -> pthread_timedjoin(); explicit_bzero -> volatile memset.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/posix_fallocate.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/pthread_timedjoin.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/q/qsort.html>

## Findings by file

### `src/core/nginx.c`

- L1507:9 **QNX024** `CPU_ZERO` — `CPU_ZERO(&mask[0]);`
- L1509:13 **QNX024** `CPU_SET` — `CPU_SET(i, &mask[0]);`
- L1528:9 **QNX024** `CPU_ZERO` — `CPU_ZERO(&mask[n - 1]);`
- L1547:17 **QNX024** `CPU_SET` — `CPU_SET(i - 1, &mask[n - 1]);`
- L1591:17 **QNX024** `CPU_ISSET` — `if (CPU_ISSET(i % CPU_SETSIZE, mask) && j-- == 0) {`
- L1603:9 **QNX024** `CPU_ZERO` — `CPU_ZERO(&result);`
- L1604:9 **QNX024** `CPU_SET` — `CPU_SET(i % CPU_SETSIZE, &result);`

### `src/core/ngx_bpf.c`

- L16:12 **QNX018** `syscall` — `return syscall(__NR_bpf, cmd, attr, size);`

### `src/core/ngx_bpf.h`

- L14:11 **QNX001** `linux/bpf.h` — `#include <linux/bpf.h>`

### `src/event/ngx_event_accept.c`

- L63:57 **QNX044** `SOCK_NONBLOCK` — `s = accept4(lc->fd, &sa.sockaddr, &socklen, SOCK_NONBLOCK);`

### `src/event/quic/bpf/ngx_quic_reuseport_helper.c`

- L2:11 **QNX001** `linux/string.h` — `#include <linux/string.h>`
- L3:11 **QNX001** `linux/udp.h` — `#include <linux/udp.h>`
- L4:11 **QNX001** `linux/bpf.h` — `#include <linux/bpf.h>`

### `src/os/unix/ngx_files.c`

- L874:9 **QNX040** `statfs` — `if (statfs((char *) name, &fs) == -1) {`
- L897:9 **QNX040** `statfs` — `if (statfs((char *) name, &fs) == -1) {`

### `src/os/unix/ngx_files.h`

- L97:35 **QNX043** `O_PATH` — `#define NGX_FILE_SEARCH          (O_PATH|O_RDONLY|NGX_FILE_DIRECTORY)`

### `src/os/unix/ngx_posix_config.h`

- L26:9 **QNX045** `_GNU_SOURCE` — `#define _GNU_SOURCE             /* accept4() */`

### `src/os/unix/ngx_process_cycle.c`

- L858:17 **QNX013** `prctl` — `if (prctl(PR_SET_KEEPCAPS, 1, 0, 0, 0) == -1) {`
- L886:17 **QNX018** `syscall` — `if (syscall(SYS_capset, &header, &data) == -1) {`
- L908:9 **QNX013** `prctl` — `if (prctl(PR_SET_DUMPABLE, 1, 0, 0, 0) == -1) {`

### `src/os/unix/ngx_setaffinity.c`

- L19:13 **QNX024** `CPU_ISSET` — `if (CPU_ISSET(i, cpu_affinity)) {`
- L41:13 **QNX024** `CPU_ISSET` — `if (CPU_ISSET(i, cpu_affinity)) {`
- L47:9 **QNX023** `sched_setaffinity` — `if (sched_setaffinity(0, sizeof(cpu_set_t), cpu_affinity) == -1) {`
- L47:37 **QNX024** `cpu_set_t` — `if (sched_setaffinity(0, sizeof(cpu_set_t), cpu_affinity) == -1) {`

### `src/os/unix/ngx_setaffinity.h`

- L16:9 **QNX024** `cpu_set_t` — `typedef cpu_set_t  ngx_cpuset_t;`

### `src/os/unix/ngx_setproctitle.h`

- L17:36 **QNX050** `setproctitle` — `#define ngx_setproctitle(title)    setproctitle("%s", title)`

### `src/os/unix/ngx_thread_id.c`

- L23:12 **QNX018** `syscall` — `return syscall(SYS_gettid);`
