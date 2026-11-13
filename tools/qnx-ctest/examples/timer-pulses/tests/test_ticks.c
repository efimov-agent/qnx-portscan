/* Ten ticks of 20 ms should take about 200 ms. */
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
    for (int i = 0; i < 10; i++) {
        if (ticker_wait_for(t, 1000) != 1) {
            fprintf(stderr, "tick %d did not arrive\n", i);
            return 1;
        }
    }
    clock_gettime(CLOCK_MONOTONIC, &end);
    ticker_stop(t);
    long ms = elapsed_ms(&start, &end);
    printf("10 ticks in %ld ms\n", ms);
    return ms >= 195 && ms < 1000 ? 0 : 1;
}
