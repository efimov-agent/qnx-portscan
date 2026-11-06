# QNX portability report: sqlite

Files scanned: **359** · Findings: **9** (errors 7, warnings 0, notes 2)

Files already mentioning `__QNX__`/`__QNXNTO__`: 2

## Summary by rule

| Rule | Severity | Title | Hits | Files |
|---|---|---|---:|---:|
| QNX033 | Error | backtrace() | 4 | 2 |
| QNX034 | Error | backtrace() | 1 | 1 |
| QNX049 | Error | /proc/<pid> Linux files | 1 | 1 |
| QNX040 | Error | statfs() | 1 | 1 |
| QNX045 | Note | GNU strerror_r | 2 | 2 |

## How to port

### QNX033: backtrace()

glibc backtrace(): Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** QNX ships libbacktrace (bt_* API) but marks it unsupported; prefer core dumps + gdb.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.technotes/topic/backtrace.html>

### QNX034: backtrace()

<execinfo.h>: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** See QNX033.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.technotes/topic/backtrace.html>

### QNX040: statfs()

statfs/fstatfs: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Use POSIX statvfs()/fstatvfs().

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/statvfs.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/f/fstatvfs.html>

### QNX045: GNU strerror_r

_GNU_SOURCE suggests reliance on GNU extensions; e.g. QNX has only the POSIX int strerror_r(), not the GNU char* variant.

**Suggestion:** Review GNU-only calls in this file (strerror_r return value, qsort_r, program_invocation_name, ...).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/strerror_r.html>

### QNX049: /proc/<pid> Linux files

Per-process Linux procfs files are not available; QNX offers as, cmdline, ctl, exefile, mappings, pmap and vmstat.

**Suggestion:** Memory map: /proc/<pid>/mappings or pmap, or devctl DCMD_PROC_MAPINFO; process info: devctl DCMD_PROC_INFO.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.prog/topic/process_proc_filesystem.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.prog/topic/process_DCMD_PROC_MAPINFO.html>

## Findings by file

### `autosetup/jimsh0.c`

- L51:9 **QNX045** `_GNU_SOURCE` — `#define _GNU_SOURCE`
- L7467:11 **QNX034** `execinfo.h` — `#include <execinfo.h>`
- L7991:16 **QNX033** `backtrace` — `size = backtrace(array, 40);`
- L7992:19 **QNX033** `backtrace_symbols` — `strings = backtrace_symbols(array, size);`

### `src/mem2.c`

- L274:26 **QNX033** `backtrace` — `pHdr->nBacktrace = backtrace(aAddr, mem.nBacktrace+1)-1;`
- L496:7 **QNX033** `backtrace_symbols_fd` — `backtrace_symbols_fd(pBt, pHdr->nBacktrace, fileno(out));`

### `src/os_unix.c`

- L650:35 **QNX049** `/proc/%d/fd` — `sqlite3_snprintf(sizeof(z), z, "/proc/%d/fdinfo/%d", getpid(), fd);`
- L6772:7 **QNX040** `fstatfs` — `if( fstatfs(fd, &fsInfo) == -1 ){`

### `src/sqliteInt.h`

- L136:10 **QNX045** `_GNU_SOURCE` — `# define _GNU_SOURCE`
