# QNX portability report: mosquitto

Files scanned: **605** · Findings: **7** (errors 2, warnings 0, notes 5)

Files already mentioning `__QNX__`/`__QNXNTO__`: 6

## Summary by rule

| Rule | Severity | Title | Hits | Files |
|---|---|---|---:|---:|
| QNX031 | Error | getrandom() | 1 | 1 |
| QNX030 | Error | getrandom() | 1 | 1 |
| QNX045 | Note | GNU strerror_r | 5 | 4 |

## How to port

### QNX030: getrandom()

getrandom/getentropy/arc4random: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Read /dev/urandom (provided by the random service, which must be running in the image).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.utilities/topic/r/random.html> · <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/lib-g.html>

### QNX031: getrandom()

<sys/random.h>: Not documented in the QNX SDP 8.0 C Library Reference.

**Suggestion:** Read /dev/urandom.

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.utilities/topic/r/random.html>

### QNX045: GNU strerror_r

_GNU_SOURCE suggests reliance on GNU extensions; e.g. QNX has only the POSIX int strerror_r(), not the GNU char* variant.

**Suggestion:** Review GNU-only calls in this file (strerror_r return value, qsort_r, program_invocation_name, ...).

Docs: <https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/s/strerror_r.html>

## Findings by file

### `config.h`

- L96:11 **QNX045** `_GNU_SOURCE` — `#  define _GNU_SOURCE`

### `lib/net_mosq.c`

- L19:9 **QNX045** `_GNU_SOURCE` — `#define _GNU_SOURCE`
- L28:9 **QNX045** `_GNU_SOURCE` — `#define _GNU_SOURCE`

### `libcommon/random_common.c`

- L33:12 **QNX031** `sys/random.h` — `# include <sys/random.h>`
- L53:5 **QNX030** `getrandom` — `if(getrandom(bytes, (size_t)count, 0) == count){`

### `src/loop.c`

- L23:11 **QNX045** `_GNU_SOURCE` — `#  define _GNU_SOURCE`

### `src/mux_poll.c`

- L24:11 **QNX045** `_GNU_SOURCE` — `#  define _GNU_SOURCE`
