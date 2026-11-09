# QNX portability report: libuv

Files scanned: **378** · Findings: **28** (errors 19, warnings 3, notes 6)

Files already mentioning `__QNX__`/`__QNXNTO__`: 18

## Summary by rule

| Rule | Severity | Title | Hits | Files |
|---|---|---|---:|---:|
| QNX024 | Error | cpu_set_t macros | 4 | 1 |
| QNX023 | Error | CPU affinity | 4 | 1 |
| QNX021 | Error | sysinfo() | 3 | 2 |
| QNX022 | Error | sysinfo() | 2 | 2 |
| QNX042 | Error | dup3() | 1 | 1 |
| QNX041 | Error | statfs() | 1 | 1 |
| QNX040 | Error | statfs() | 1 | 1 |
| QNX004 | Error | kqueue | 1 | 1 |
| QNX048 | Error | /proc/self/exe | 1 | 1 |
| QNX025 | Error | sched_getcpu() | 1 | 1 |
| QNX056 | Warning | SOCK_MAXADDRLEN (check AF_UNIX address length) | 3 | 2 |
| QNX044 | Note | SOCK_NONBLOCK/SOCK_CLOEXEC | 6 | 2 |

## How to port

### QNX004: kqueue

kqueue/kevent: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use ionotify() + pulses, or poll().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/i/ionotify.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/lib-k.html>

### QNX021: sysinfo()

sysinfo(): Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** CPU count and memory: system page (_syspage_ptr, SYSPAGE_ENTRY) or devctl DCMD_PROC_SYSINFO; uptime: clock_gettime(CLOCK_MONOTONIC). Note: sysconf(_SC_NPROCESSORS_ONLN) is not documented in 8.0.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/_syspage_ptr.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/syspage_entry.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.prog/topic/process_DCMD_PROC_SYSINFO.html>

### QNX022: sysinfo()

<sys/sysinfo.h>: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** See QNX021.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/_syspage_ptr.html>

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

### QNX040: statfs()

statfs/fstatfs: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use POSIX statvfs()/fstatvfs().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/statvfs.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/f/fstatvfs.html>

### QNX041: statfs()

<sys/vfs.h>: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use <sys/statvfs.h>.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/statvfs.html>

### QNX042: dup3()

dup3(): Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** dup2() + fcntl(fd, F_SETFD, FD_CLOEXEC).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/d/dup2.html>

### QNX044: SOCK_NONBLOCK/SOCK_CLOEXEC

io-sock follows FreeBSD 14.3 socket(2), which has these flags, but QNX does not confirm them explicitly.

**Suggestion:** If the build fails, use fcntl(O_NONBLOCK)/ioctl(FIONBIO) and fcntl(FD_CLOEXEC) after socket().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.io_sock/topic/socket_api.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.devctl/topic/fio/fionbio.html>

### QNX048: /proc/self/exe

QNX has /proc/<pid>/exefile instead of the Linux exe symlink; per a QNX forum answer it is a regular file holding the path, so readlink() is expected to fail.

**Suggestion:** Read /proc/<pid>/exefile as a file, or use _cmdname().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.prog/topic/process_proc_filesystem.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/c/_cmdname.html>

### QNX056: SOCK_MAXADDRLEN (check AF_UNIX address length)

QNX 8.0 defines SOCK_MAXADDRLEN as 255, but bind() and connect() returned EINVAL for AF_UNIX address lengths above sizeof(struct sockaddr_un) (106) at every length tested in an SDP 8.0 guest. A plain buffer of this size is harmless.

**Suggestion:** Check that this size does not reach bind() or connect() as an AF_UNIX address length; if it does, cap the length at sizeof(struct sockaddr_un) on QNX.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.io_sock/topic/socket_api.html>

## Findings by file

### `src/unix/core.c`

- L513:34 **QNX044** `SOCK_NONBLOCK` — `sockfd = socket(domain, type | SOCK_NONBLOCK | SOCK_CLOEXEC, protocol);`
- L513:50 **QNX044** `SOCK_CLOEXEC` — `sockfd = socket(domain, type | SOCK_NONBLOCK | SOCK_CLOEXEC, protocol);`
- L570:46 **QNX044** `SOCK_NONBLOCK` — `peerfd = uv__accept4(sockfd, NULL, NULL, SOCK_NONBLOCK|SOCK_CLOEXEC);`
- L570:60 **QNX044** `SOCK_CLOEXEC` — `peerfd = uv__accept4(sockfd, NULL, NULL, SOCK_NONBLOCK|SOCK_CLOEXEC);`
- L1254:7 **QNX042** `dup3` — `r = dup3(oldfd, newfd, O_CLOEXEC);`

### `src/unix/fs.c`

- L83:12 **QNX041** `sys/statfs.h` — `# include <sys/statfs.h>`
- L684:12 **QNX040** `statfs` — `if (0 != statfs(req->path, &buf))`

### `src/unix/pipe.c`

- L40:15 **QNX056** `SOCK_MAXADDRLEN` — `char path[SOCK_MAXADDRLEN - offsetof(struct sockaddr_un, sun_path)];`

### `src/unix/process.c`

- L1208:9 **QNX004** `kevent` — `if (kevent(loop->backend_fd, &event, 1, NULL, 0, NULL)) {`

### `src/unix/procfs-exepath.c`

- L36:19 **QNX048** `/proc/self/exe` — `n = readlink("/proc/self/exe", buffer, n);`

### `src/unix/sysinfo-loadavg.c`

- L26:11 **QNX022** `sys/sysinfo.h` — `#include <sys/sysinfo.h>`
- L31:7 **QNX021** `sysinfo` — `if (sysinfo(&info) < 0) return;`

### `src/unix/sysinfo-memory.c`

- L26:11 **QNX022** `sys/sysinfo.h` — `#include <sys/sysinfo.h>`
- L31:7 **QNX021** `sysinfo` — `if (sysinfo(&info) == 0)`
- L39:7 **QNX021** `sysinfo` — `if (sysinfo(&info) == 0)`

### `src/unix/tcp.c`

- L640:18 **QNX044** `SOCK_CLOEXEC` — `flags = type | SOCK_CLOEXEC;`
- L642:14 **QNX044** `SOCK_NONBLOCK` — `flags |= SOCK_NONBLOCK;`

### `src/unix/thread.c`

- L212:3 **QNX024** `CPU_ZERO` — `CPU_ZERO(&cpuset);`
- L215:7 **QNX024** `CPU_SET` — `CPU_SET(i, &cpuset);`
- L218:7 **QNX023** `sched_setaffinity` — `if (sched_setaffinity(pthread_gettid_np(*tid), sizeof(cpuset), &cpuset))`
- L223:7 **QNX023** `pthread_setaffinity_np` — `r = pthread_setaffinity_np(*tid, sizeof(cpuset), &cpuset);`
- L244:3 **QNX024** `CPU_ZERO` — `CPU_ZERO(&cpuset);`
- L246:7 **QNX023** `sched_getaffinity` — `if (sched_getaffinity(pthread_gettid_np(*tid), sizeof(cpuset), &cpuset))`
- L251:7 **QNX023** `pthread_getaffinity_np` — `r = pthread_getaffinity_np(*tid, sizeof(cpuset), &cpuset);`
- L256:20 **QNX024** `CPU_ISSET` — `cpumask[i] = !!CPU_ISSET(i, &cpuset);`
- L280:9 **QNX025** `sched_getcpu` — `cpu = sched_getcpu();`

### `test/test-pipe-getsockname.c`

- L388:13 **QNX056** `SOCK_MAXADDRLEN` — `char path[SOCK_MAXADDRLEN - offsetof(struct sockaddr_un, sun_path) + 1];`
- L389:13 **QNX056** `SOCK_MAXADDRLEN` — `char name[SOCK_MAXADDRLEN - offsetof(struct sockaddr_un, sun_path) + 1];`
