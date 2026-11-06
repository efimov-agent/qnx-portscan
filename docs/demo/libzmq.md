# QNX portability report: libzmq

Files scanned: **465** · Findings: **18** (errors 13, warnings 0, notes 5)

## Summary by rule

| Rule | Severity | Title | Hits | Files |
|---|---|---|---:|---:|
| QNX001 | Error | Linux kernel headers | 8 | 8 |
| QNX024 | Error | cpu_set_t macros | 4 | 1 |
| QNX023 | Error | CPU affinity | 1 | 1 |
| QNX044 | Note | SOCK_NONBLOCK/SOCK_CLOEXEC | 5 | 4 |

## How to port

### QNX001: Linux kernel headers

<linux/...> headers expose the Linux kernel ABI; no such headers are documented for QNX SDP 8.0.

**Suggestion:** Guard with #ifdef __linux__ and provide a QNX path: device control goes through devctl() or a resource manager.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/d/devctl.html>

### QNX023: CPU affinity

Linux affinity API: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use ThreadCtl(_NTO_TCTL_RUNMASK...) from the thread itself (the mask must match a CPU cluster in 8.0), pthread_cluster_set(), or posix_spawnattr_setrunmask() for children.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/threadctl.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/pthread_cluster_set.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/posix_spawnattr_setrunmask.html>

### QNX024: cpu_set_t macros

cpu_set_t / CPU_* macros: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use a uint64_t run mask with ThreadCtl(_NTO_TCTL_RUNMASK).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/threadctl.html>

### QNX044: SOCK_NONBLOCK/SOCK_CLOEXEC

io-sock follows FreeBSD 14.3 socket(2), which has these flags, but QNX does not confirm them explicitly.

**Suggestion:** If the build fails, use fcntl(O_NONBLOCK)/ioctl(FIONBIO) and fcntl(FD_CLOEXEC) after socket().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.io_sock/topic/socket_api.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.devctl/topic/fio/fionbio.html>

## Findings by file

### `src/ip.cpp`

- L80:14 **QNX044** `SOCK_CLOEXEC` — `type_ |= SOCK_CLOEXEC;`
- L792:13 **QNX044** `SOCK_CLOEXEC` — `type |= SOCK_CLOEXEC;`

### `src/ipc_listener.cpp`

- L278:44 **QNX044** `SOCK_CLOEXEC` — `fd_t sock = ::accept4 (_s, NULL, NULL, SOCK_CLOEXEC);`

### `src/tcp_listener.cpp`

- L179:37 **QNX044** `SOCK_CLOEXEC` — `&ss_len, SOCK_CLOEXEC);`

### `src/thread.cpp`

- L334:9 **QNX024** `cpu_set_t` — `cpu_set_t cpuset;`
- L335:9 **QNX024** `CPU_ZERO` — `CPU_ZERO (&cpuset);`
- L339:13 **QNX024** `CPU_SET` — `CPU_SET ((int) (*it), &cpuset);`
- L342:11 **QNX023** `pthread_setaffinity_np` — `pthread_setaffinity_np (pthread_self (), sizeof (cpu_set_t), &cpuset);`
- L342:60 **QNX024** `cpu_set_t` — `pthread_setaffinity_np (pthread_self (), sizeof (cpu_set_t), &cpuset);`

### `src/tipc_address.hpp`

- L16:11 **QNX001** `linux/tipc.h` — `#include <linux/tipc.h>`

### `src/tipc_listener.cpp`

- L28:11 **QNX001** `linux/tipc.h` — `#include <linux/tipc.h>`

### `src/vsock_address.hpp`

- L12:11 **QNX001** `linux/vm_sockets.h` — `#include <linux/vm_sockets.h>`

### `src/vsock_connecter.cpp`

- L20:11 **QNX001** `linux/vm_sockets.h` — `#include "linux/vm_sockets.h"`

### `src/vsock_listener.cpp`

- L20:11 **QNX001** `linux/vm_sockets.h` — `#include "linux/vm_sockets.h"`

### `src/ws_listener.cpp`

- L227:37 **QNX044** `SOCK_CLOEXEC` — `&ss_len, SOCK_CLOEXEC);`

### `tests/test_pair_vsock.cpp`

- L9:11 **QNX001** `linux/vm_sockets.h` — `#include "linux/vm_sockets.h"`

### `tests/test_reqrep_vsock.cpp`

- L9:11 **QNX001** `linux/vm_sockets.h` — `#include "linux/vm_sockets.h"`

### `tests/testutil.cpp`

- L44:11 **QNX001** `linux/vm_sockets.h` — `#include "linux/vm_sockets.h"`
