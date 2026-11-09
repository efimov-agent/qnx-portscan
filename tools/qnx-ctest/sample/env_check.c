#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int main(void)
{
    const char *value = getenv("QCT_VALUE");
    printf("QCT_VALUE=[%s]\n", value ? value : "(none)");
    return value && strcmp(value, "a b") == 0 ? 0 : 1;
}
