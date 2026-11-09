/* Which sockaddr_un shapes does QNX 8.0 accept for AF_UNIX bind() and connect()?
 * Prints one line per combination of address length and sun_len. Each case uses a fresh path. */
#include <errno.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <unistd.h>

enum { LEN_SIZEOF, LEN_SIZEOF_MINUS_1, LEN_PATH_NUL, LEN_PATH, LEN_COUNT };
static const char *const len_names[LEN_COUNT] = {"sizeof", "sizeof-1", "path+nul", "path"};

static socklen_t addr_len(int kind, size_t path_len) {
  size_t base = offsetof(struct sockaddr_un, sun_path);
  switch (kind) {
  case LEN_SIZEOF: return sizeof(struct sockaddr_un);
  case LEN_SIZEOF_MINUS_1: return sizeof(struct sockaddr_un) - 1;
  case LEN_PATH_NUL: return (socklen_t)(base + path_len + 1);
  default: return (socklen_t)(base + path_len);
  }
}

static void fill(struct sockaddr_un *a, const char *path, unsigned sun_len) {
  memset(a, 0, sizeof *a);
  a->sun_len = (unsigned char)sun_len;
  a->sun_family = AF_UNIX;
  strcpy(a->sun_path, path);
}

static void report(const char *op, int len_kind, socklen_t len, const char *sun_len, int rc, int err) {
  printf("%-8s addrlen=%-8s(%3u) sun_len=%-7s -> %s\n", op, len_names[len_kind], (unsigned)len, sun_len,
         rc == 0 ? "ok" : strerror(err));
}

int main(void) {
  static const char *const sun_names[] = {"0", "addrlen"};
  int case_no = 0;
  printf("sizeof(sockaddr_un)=%zu offsetof(sun_path)=%zu\n", sizeof(struct sockaddr_un),
         offsetof(struct sockaddr_un, sun_path));
  for (int kind = 0; kind < LEN_COUNT; kind++) {
    for (int s = 0; s < 2; s++) {
      char path[32];
      struct sockaddr_un a;
      snprintf(path, sizeof path, "probe-sock-%d", case_no++);
      unlink(path);
      socklen_t len = addr_len(kind, strlen(path));
      int fd = socket(AF_UNIX, SOCK_STREAM, 0);
      fill(&a, path, s ? len : 0);
      int rc = bind(fd, (struct sockaddr *)&a, len);
      report("bind", kind, len, sun_names[s], rc, errno);
      if (rc == 0) {
        listen(fd, 1);
        for (int t = 0; t < 2; t++) {
          int c = socket(AF_UNIX, SOCK_STREAM, 0);
          socklen_t clen = addr_len(LEN_SIZEOF, strlen(path));
          fill(&a, path, t ? clen : 0);
          rc = connect(c, (struct sockaddr *)&a, clen);
          report("connect", LEN_SIZEOF, clen, sun_names[t], rc, errno);
          close(c);
        }
      }
      close(fd);
      unlink(path);
    }
  }
  /* Same call with no socket file behind it: what errno does a missing path give? */
  for (int kind = 0; kind < LEN_COUNT; kind++) {
    struct sockaddr_un a;
    const char *path = "probe-no-such-sock";
    socklen_t len = addr_len(kind, strlen(path));
    int c = socket(AF_UNIX, SOCK_STREAM, 0);
    fill(&a, path, 0);
    int rc = connect(c, (struct sockaddr *)&a, len);
    report("conn-404", kind, len, "0", rc, errno);
    close(c);
  }
  /* Oversized address lengths, with a buffer big enough for each. */
  static const unsigned lens[] = {106, 107, 108, 109, 110, 128, 200, 254, 255, 256};
  for (size_t i = 0; i < sizeof lens / sizeof lens[0]; i++) {
    union { struct sockaddr sa; char raw[512]; } u;
    char path[32];
    snprintf(path, sizeof path, "probe-big-%u", lens[i]);
    unlink(path);
    int fd = socket(AF_UNIX, SOCK_STREAM, 0);
    memset(&u, 0, sizeof u);
    u.sa.sa_family = AF_UNIX;
    strcpy(u.raw + offsetof(struct sockaddr_un, sun_path), path);
    int rc = bind(fd, &u.sa, lens[i]);
    printf("bind     addrlen=%u -> %s\n", lens[i], rc == 0 ? "ok" : strerror(errno));
    close(fd);
    unlink(path);
    int c = socket(AF_UNIX, SOCK_STREAM, 0);
    rc = connect(c, &u.sa, lens[i]);
    printf("connect  addrlen=%u (no such path) -> %s\n", lens[i], rc == 0 ? "ok" : strerror(errno));
    close(c);
  }
#ifdef SOCK_MAXADDRLEN
  printf("SOCK_MAXADDRLEN=%d\n", (int)SOCK_MAXADDRLEN);
#else
  puts("SOCK_MAXADDRLEN undefined");
#endif
  return 0;
}
