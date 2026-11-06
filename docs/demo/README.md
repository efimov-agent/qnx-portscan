# Demo reports

Output of `qnx-portscan` run against seven well-known open-source C/C++ projects
(shallow `git clone --depth 1`, default settings). Reproduce:

```
for p in libuv redis mosquitto libzmq nginx sqlite curl; do
  dotnet run -c Release --project ../../src/QnxPortScan.Cli -- \
    scan /path/to/$p --format both --out $p
done
```

| Project | Files | Findings | Errors | Notes |
|---|---:|---:|---:|---:|
| [libuv](libuv.md) | 378 | 25 | 19 | 6 |
| [redis](redis.md) | 856 | 59 | 44 | 8 |
| [mosquitto](mosquitto.md) | 605 | 7 | 2 | 5 |
| [libzmq](libzmq.md) | 465 | 18 | 13 | 5 |
| [nginx](nginx.md) | 411 | 27 | 25 | 2 |
| [sqlite](sqlite.md) | 359 | 9 | 7 | 2 |
| [curl](curl.md) | 1067 | 27 | 18 | 9 |

Reports are generated, not hand-edited. The findings are the real Linux-only
call sites a QNX port must revisit: cpu_set_t affinity, raw `syscall()`,
`statfs()`, `prctl()`, `O_PATH` (nginx); epoll, timerfd, `getrandom()` (curl);
glibc `backtrace()`, `/proc/<pid>` reads (sqlite).

Two behaviours worth noting from these runs:

- **Guarded code is trusted by default.** libuv and mosquitto already carry some
  QNX support (`__QNX__` guards); nginx keeps its epoll/eventfd/timerfd use
  inside `#if (NGX_HAVE_EPOLL)`-style branches that a QNX build never compiles.
  The scanner counts those files in the header but reports only the call sites a
  port actually has to change. Pass `--include-guarded` to also list the guarded
  branches as notes (e.g. nginx's `ngx_epoll_module.c` then surfaces 17 notes).
- **Portable code stays quiet.** sqlite — written to be portable — yields just 9
  findings over 359 files, almost all optional diagnostics (`backtrace()`,
  GNU `strerror_r`), which is the low-false-positive signal a porting team wants.
