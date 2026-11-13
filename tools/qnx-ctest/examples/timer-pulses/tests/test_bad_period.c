/* A zero or negative period is rejected with EINVAL. */
#include <errno.h>
#include <stdio.h>
#include "ticker.h"

static int rejected(long period_ms)
{
    errno = 0;
    ticker_t *t = ticker_start(period_ms);
    printf("period=%ld ticker=%p errno=%d\n", period_ms, (void *)t, errno);
    return t == NULL && errno == EINVAL;
}

int main(void)
{
    return rejected(0) && rejected(-5) ? 0 : 1;
}
