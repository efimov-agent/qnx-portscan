/* A wait shorter than the period times out; a longer one gets the tick. */
#include <stdio.h>
#include <time.h>
#include "ticker.h"

static long elapsed_ms(const struct timespec *a, const struct timespec *b)
{
    return (b->tv_sec - a->tv_sec) * 1000 + (b->tv_nsec - a->tv_nsec) / 1000000;
}

int main(void)
{
    ticker_t *t = ticker_start(300);
    if (t == NULL) {
        perror("ticker_start");
        return 1;
    }
    struct timespec a, b;
    clock_gettime(CLOCK_MONOTONIC, &a);
    int first = ticker_wait_for(t, 50);
    clock_gettime(CLOCK_MONOTONIC, &b);
    int second = ticker_wait_for(t, 1000);
    ticker_stop(t);
    long waited = elapsed_ms(&a, &b);
    printf("first=%d after %ld ms, second=%d\n", first, waited, second);
    return first == 0 && waited >= 45 && second == 1 ? 0 : 1;
}
