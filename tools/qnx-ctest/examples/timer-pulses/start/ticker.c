/* Starting point: a periodic ticker written for Linux. It does not build on QNX 8.0. */
#include <poll.h>
#include <stdint.h>
#include <stdlib.h>
#include <sys/timerfd.h>
#include <unistd.h>

typedef struct ticker { int fd; } ticker_t;

ticker_t *ticker_start(long period_ms)
{
    ticker_t *t = malloc(sizeof *t);
    if (t == NULL)
        return NULL;
    t->fd = timerfd_create(CLOCK_MONOTONIC, TFD_CLOEXEC);
    struct itimerspec its = {
        .it_interval = { period_ms / 1000, (period_ms % 1000) * 1000000L },
        .it_value = { period_ms / 1000, (period_ms % 1000) * 1000000L },
    };
    if (t->fd == -1 || timerfd_settime(t->fd, 0, &its, NULL) == -1) {
        free(t);
        return NULL;
    }
    return t;
}

/* 1 on a tick, 0 on timeout, -1 on error. */
int ticker_wait_for(ticker_t *t, long timeout_ms)
{
    struct pollfd p = { .fd = t->fd, .events = POLLIN };
    int n = poll(&p, 1, (int)timeout_ms);
    if (n <= 0)
        return n;
    uint64_t expirations;
    return read(t->fd, &expirations, sizeof expirations) == sizeof expirations ? 1 : -1;
}

void ticker_stop(ticker_t *t)
{
    close(t->fd);
    free(t);
}
