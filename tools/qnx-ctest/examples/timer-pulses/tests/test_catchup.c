/* Expirations are queued as separate pulses: after a stall, each missed period is one immediate tick. */
#include <stdio.h>
#include <time.h>
#include "ticker.h"

static long elapsed_ms(const struct timespec *a, const struct timespec *b)
{
    return (b->tv_sec - a->tv_sec) * 1000 + (b->tv_nsec - a->tv_nsec) / 1000000;
}

int main(void)
{
    struct timespec start, end;
    clock_gettime(CLOCK_MONOTONIC, &start);
    ticker_t *t = ticker_start(20);
    if (t == NULL) {
        perror("ticker_start");
        return 1;
    }
    int before = ticker_wait_for(t, 0);          /* nothing queued yet */
    struct timespec stall = { 0, 110 * 1000000L };
    nanosleep(&stall, NULL);                     /* about 5 periods pass without a receive */
    clock_gettime(CLOCK_MONOTONIC, &end);
    int queued = 0;
    while (ticker_wait_for(t, 0) == 1)
        queued++;
    ticker_stop(t);
    long expected = elapsed_ms(&start, &end) / 20; /* periods that expired during the stall */
    printf("before=%d queued=%d expected=%ld\n", before, queued, expected);
    return before == 0 && expected >= 4 && queued >= expected - 1 && queued <= expected + 1 ? 0 : 1;
}
