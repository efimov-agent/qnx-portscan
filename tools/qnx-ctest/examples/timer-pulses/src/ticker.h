#ifndef TICKER_H
#define TICKER_H

typedef struct ticker ticker_t;

/* Starts a periodic ticker; the first tick comes after one period. NULL on error (errno set). */
ticker_t *ticker_start(long period_ms);

/* Waits for the next tick: 1 on a tick, 0 if timeout_ms passed first, -1 on error (errno set). */
int ticker_wait_for(ticker_t *t, long timeout_ms);

void ticker_stop(ticker_t *t);

#endif
