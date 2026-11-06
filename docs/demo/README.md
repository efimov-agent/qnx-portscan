# Demo reports

Output of `qnx-portscan` run against four well-known open-source C/C++ projects
(shallow `git clone --depth 1`, default settings). Reproduce:

```
for p in libuv redis mosquitto libzmq; do
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

Reports are generated, not hand-edited. libuv and mosquitto already carry some
QNX support (`__QNX__` guards), which is why their counts are low — the scanner
counts those files in the header but still reports the Linux-only call sites a
port must revisit.
