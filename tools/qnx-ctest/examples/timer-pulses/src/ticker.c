/* The ticker ported to QNX 8.0: the timer delivers pulses to a private channel. */
#include "ticker.h"

#include <errno.h>
#include <stdint.h>
#include <stdlib.h>
#include <sys/neutrino.h>
#include <time.h>

#define TICK_CODE (_PULSE_CODE_MINAVAIL + 1)

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

fail_timer:
    timer_delete(t->timer);
fail_connection:
    ConnectDetach(t->coid);
fail_channel:
    ChannelDestroy(t->chid);
fail_alloc:
    free(t);
    return NULL;
}

int ticker_wait_for(ticker_t *t, long timeout_ms)
{
    for (;;) {
        /* Arm a kernel timeout for the next receive only; the kernel clears it afterwards. */
        uint64_t ns = (uint64_t)timeout_ms * 1000000u;
        if (TimerTimeout(CLOCK_MONOTONIC, _NTO_TIMEOUT_RECEIVE, NULL, &ns, NULL) == -1)
            return -1;

        struct _pulse pulse;
        if (MsgReceivePulse(t->chid, &pulse, sizeof pulse, NULL) == -1)
            return errno == ETIMEDOUT ? 0 : -1;
        if (pulse.code == TICK_CODE)
            return 1;
        /* Any other pulse (none are expected on a private channel): keep waiting. */
    }
}

void ticker_stop(ticker_t *t)
{
    timer_delete(t->timer);
    ConnectDetach(t->coid);
    ChannelDestroy(t->chid);
    free(t);
}
