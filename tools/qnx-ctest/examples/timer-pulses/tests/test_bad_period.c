/* A zero period is rejected with EINVAL. */
#include <errno.h>
#include <stdio.h>
#include "ticker.h"

int main(void)
{
    errno = 0;
    ticker_t *t = ticker_start(0);
    printf("ticker=%p errno=%d\n", (void *)t, errno);
    return t == NULL && errno == EINVAL ? 0 : 1;
}
