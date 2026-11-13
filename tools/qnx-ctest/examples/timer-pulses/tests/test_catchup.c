/* Expirations are queued as separate pulses: after a stall, each missed period is one immediate tick. */
#include <stdio.h>
#include <time.h>
#include "ticker.h"

int main(void)
{
    ticker_t *t = ticker_start(20);
    if (t == NULL) {
        perror("ticker_start");
        return 1;
    }
    int before = ticker_wait_for(t, 0);          /* nothing queued yet */
    struct timespec stall = { 0, 110 * 1000000L };
    nanosleep(&stall, NULL);                     /* about 5 periods pass without a receive */
    int queued = 0;
    while (ticker_wait_for(t, 0) == 1)
        queued++;
    ticker_stop(t);
    printf("before=%d queued=%d\n", before, queued);
    return before == 0 && queued >= 4 && queued <= 6 ? 0 : 1;
}
