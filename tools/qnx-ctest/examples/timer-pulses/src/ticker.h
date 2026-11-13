#ifndef TICKER_H
#define TICKER_H

typedef struct ticker ticker_t;

/* Starts a periodic ticker; the first tick comes after one period. NULL on error (errno set). */
ticker_t *ticker_start(long period_ms);

/* Waits for the next tick: 1 on a tick, 0 if timeout_ms passed first, -1 on error (errno set).
 * timeout_ms == 0 only takes a tick that is already queued; timeout_ms < 0 waits without a limit.
 * Timeouts above about 30 days are capped. Every expiration is queued as its own pulse,
 * so after a stall the caller gets one quick tick per missed period. */
int ticker_wait_for(ticker_t *t, long timeout_ms);

void ticker_stop(ticker_t *t);

#endif
