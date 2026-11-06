# QNX portability report: curl

Files scanned: **1067** · Findings: **27** (errors 18, warnings 0, notes 9)

## Summary by rule

| Rule | Severity | Title | Hits | Files |
|---|---|---|---:|---:|
| QNX003 | Error | epoll | 8 | 1 |
| QNX009 | Error | timerfd | 4 | 1 |
| QNX018 | Error | raw syscall() | 2 | 1 |
| QNX002 | Error | epoll | 1 | 1 |
| QNX010 | Error | timerfd | 1 | 1 |
| QNX050 | Error | Linux GNU extensions | 1 | 1 |
| QNX030 | Error | getrandom() | 1 | 1 |
| QNX044 | Note | SOCK_NONBLOCK/SOCK_CLOEXEC | 9 | 2 |

## How to port

### QNX002: epoll

<sys/epoll.h>: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use poll()/ppoll()/select(), or ionotify() with pulses for an event-driven QNX design.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/i/ionotify.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/poll.html>

### QNX003: epoll

epoll is Linux-only. Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Simple path: poll()/ppoll(). Native path: ionotify() arms a sigevent per fd; receive pulses on a channel (ChannelCreate + MsgReceivePulse). QNX docs describe ionotify as the epoll/kqueue counterpart.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/i/ionotify.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/m/msgreceivepulse.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/poll.html>

### QNX009: timerfd

timerfd: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Create a POSIX timer with timer_create() and a SIGEV_PULSE sigevent, arm it with timer_settime(), receive expirations with MsgReceivePulse().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/timer_create.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/sigevent.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/timer_settime.html>

### QNX010: timerfd

<sys/timerfd.h>: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use timer_create() with SIGEV_PULSE.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/t/timer_create.html>

### QNX018: raw syscall()

syscall(): Not documented in the QNX SDP 8.0 C Library Reference. Linux syscall numbers do not apply to QNX.

**Suggestion:** Call the libc wrapper, or the QNX kernel call (MsgSend, ThreadCtl, ...).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/lib-s.html>

### QNX030: getrandom()

getrandom/getentropy/arc4random: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Read /dev/urandom (provided by the random service, which must be running in the image).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.utilities/topic/r/random.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/lib-g.html>

### QNX044: SOCK_NONBLOCK/SOCK_CLOEXEC

io-sock follows FreeBSD 14.3 socket(2), which has these flags, but QNX does not confirm them explicitly.

**Suggestion:** If the build fails, use fcntl(O_NONBLOCK)/ioctl(FIONBIO) and fcntl(FD_CLOEXEC) after socket().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.io_sock/topic/socket_api.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.devctl/topic/fio/fionbio.html>

### QNX050: Linux GNU extensions

GNU/Linux libc extension: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** qsort_r -> qsort() with TLS context; getdents -> readdir(); syncfs -> fsync()/sync(); fallocate -> posix_fallocate(); pthread_tryjoin_np -> pthread_timedjoin(); explicit_bzero -> volatile memset.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/posix_fallocate.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/p/pthread_timedjoin.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/q/qsort.html>

## Findings by file

### `docs/examples/ephiperfifo.c`

- L65:11 **QNX002** `sys/epoll.h` — `#include <sys/epoll.h>`
- L68:11 **QNX010** `sys/timerfd.h` — `#include <sys/timerfd.h>`
- L228:3 **QNX009** `timerfd_settime` — `timerfd_settime(g->tfd, /* flags= */ 0, &its, NULL);`
- L248:5 **QNX009** `timerfd_settime` — `timerfd_settime(g->tfd, 0, &its, NULL);`
- L257:10 **QNX003** `epoll_ctl` — `if(epoll_ctl(g->epfd, EPOLL_CTL_DEL, f->sockfd, NULL))`
- L274:8 **QNX003** `epoll_ctl` — `if(epoll_ctl(g->epfd, EPOLL_CTL_DEL, f->sockfd, NULL))`
- L285:6 **QNX003** `epoll_ctl` — `if(epoll_ctl(g->epfd, EPOLL_CTL_ADD, s, &ev))`
- L440:3 **QNX003** `epoll_ctl` — `epoll_ctl(g->epfd, EPOLL_CTL_ADD, sockfd, &epev);`
- L448:3 **QNX003** `epoll_ctl` — `epoll_ctl(g->epfd, EPOLL_CTL_DEL, g->fifofd, NULL);`
- L476:12 **QNX003** `epoll_create1` — `g.epfd = epoll_create1(EPOLL_CLOEXEC);`
- L483:11 **QNX009** `timerfd_create` — `g.tfd = timerfd_create(CLOCK_MONOTONIC, TFD_NONBLOCK | TFD_CLOEXEC);`
- L493:3 **QNX009** `timerfd_settime` — `timerfd_settime(g.tfd, 0, &its, NULL);`
- L497:3 **QNX003** `epoll_ctl` — `epoll_ctl(g.epfd, EPOLL_CTL_ADD, g.tfd, &ev);`
- L518:15 **QNX003** `epoll_wait` — `int err = epoll_wait(g.epfd, events,`

### `lib/cf-socket.c`

- L434:21 **QNX044** `SOCK_CLOEXEC` — `addr->socktype |= SOCK_CLOEXEC;`
- L1196:9 **QNX044** `SOCK_CLOEXEC` — `x &= ~SOCK_CLOEXEC;`
- L1199:9 **QNX044** `SOCK_NONBLOCK` — `x &= ~SOCK_NONBLOCK;`
- L1271:27 **QNX044** `SOCK_NONBLOCK` — `ctx->addr.socktype |= SOCK_NONBLOCK;`
- L1277:28 **QNX044** `SOCK_NONBLOCK` — `ctx->addr.socktype &= ~SOCK_NONBLOCK;`
- L2372:29 **QNX044** `SOCK_NONBLOCK` — `SOCK_NONBLOCK | SOCK_CLOEXEC);`
- L2372:45 **QNX044** `SOCK_CLOEXEC` — `SOCK_NONBLOCK | SOCK_CLOEXEC);`

### `lib/curl_setup.h`

- L1672:39 **QNX050** `explicit_bzero` — `#define curlx_memzero_low(buf, size)  explicit_bzero(buf, size)`

### `lib/rand.c`

- L59:21 **QNX030** `arc4random` — `r = (unsigned int)arc4random();`

### `lib/socketpair.c`

- L94:11 **QNX044** `SOCK_CLOEXEC` — `type |= SOCK_CLOEXEC;`
- L98:13 **QNX044** `SOCK_NONBLOCK` — `type |= SOCK_NONBLOCK;`

### `lib/vquic/vquic.c`

- L207:17 **QNX018** `syscall` — `while((rc = syscall(SYS_sendmsg_x, qctx->sockfd, &mmsg, batch, 0)) == -1 &&`
- L796:21 **QNX018** `syscall` — `while((mcount = syscall(SYS_recvmsg_x, qctx->sockfd, mmsg, n, 0)) == -1 &&`
