/* The ticker ported to QNX 8.0: the timer delivers pulses to a private channel. */
#include "ticker.h"

#include <errno.h>
#include <stdint.h>
#include <stdlib.h>
#include <sys/neutrino.h>
#include <time.h>

/* Application pulse codes are _PULSE_CODE_MINAVAIL..._PULSE_CODE_MAXAVAIL (0..127). */
#define TICK_CODE (_PULSE_CODE_MINAVAIL + 1)
/* Caps a timeout at about 30 days, far below where milliseconds in nanoseconds would overflow. */
#define MAX_TIMEOUT_MS (30ull * 24 * 3600 * 1000)

struct ticker {
    int chid;
    int coid;
    timer_t timer;
};

static struct timespec ms_to_timespec(long ms)
{
    struct timespec ts = { ms / 1000, (ms % 1000) * 1000000L };
    return ts;
}

ticker_t *ticker_start(long period_ms)
{
    if (period_ms <= 0) {
        errno = EINVAL;
        return NULL;
    }
    ticker_t *t = malloc(sizeof *t);
    if (t == NULL)
        return NULL;

    /* A private channel: only this process can connect to it. */
    t->chid = ChannelCreate(_NTO_CHF_PRIVATE);
    if (t->chid == -1)
        goto fail_alloc;
    /* _NTO_SIDE_CHANNEL keeps the connection id out of the file descriptor range. */
    t->coid = ConnectAttach(0, 0, t->chid, _NTO_SIDE_CHANNEL, 0);
    if (t->coid == -1)
        goto fail_channel;

    struct sigevent ev;
    SIGEV_PULSE_INIT(&ev, t->coid, SIGEV_PULSE_PRIO_INHERIT, TICK_CODE, 0);
    if (timer_create(CLOCK_MONOTONIC, &ev, &t->timer) == -1)
        goto fail_connection;

    struct itimerspec its = { .it_value = ms_to_timespec(period_ms), .it_interval = ms_to_timespec(period_ms) };
    if (timer_settime(t->timer, 0, &its, NULL) == -1)
        goto fail_timer;
    return t;

    int saved;
fail_timer:
    saved = errno;
    timer_delete(t->timer);
    errno = saved;
fail_connection:
    saved = errno;
    ConnectDetach(t->coid);
    errno = saved;
fail_channel:
    saved = errno;
    ChannelDestroy(t->chid);
    errno = saved;
fail_alloc:
    saved = errno;
    free(t);
    errno = saved;
    return NULL;
}

int ticker_wait_for(ticker_t *t, long timeout_ms)
{
    /* One absolute deadline, so a stray pulse cannot extend the total wait. */
    uint64_t deadline = 0;
    if (timeout_ms >= 0) {
        struct timespec now;
        if (clock_gettime(CLOCK_MONOTONIC, &now) == -1)
            return -1;
        uint64_t ms = (uint64_t)timeout_ms;
        if (ms > MAX_TIMEOUT_MS)
            ms = MAX_TIMEOUT_MS;
        deadline = timespec2nsec(&now) + ms * 1000000u;
    }
    for (;;) {
        /* The kernel timeout covers only the next blocking call, so it is armed on every pass. */
        if (timeout_ms >= 0 &&
            TimerTimeout(CLOCK_MONOTONIC, _NTO_TIMEOUT_RECEIVE | TIMER_ABSTIME, NULL, &deadline, NULL) == -1)
            return -1;

        struct _pulse pulse;
        if (MsgReceivePulse(t->chid, &pulse, sizeof pulse, NULL) == -1)
            return errno == ETIMEDOUT ? 0 : -1;
        if (pulse.code == TICK_CODE)
            return 1;
        /* Any other pulse (none are expected on a private channel): keep waiting until the deadline. */
    }
}

void ticker_stop(ticker_t *t)
{
    timer_delete(t->timer);
    ConnectDetach(t->coid);
    ChannelDestroy(t->chid);
    free(t);
}
