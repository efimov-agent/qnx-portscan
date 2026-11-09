#include <stdio.h>
#include <stdlib.h>

/* Prints its first argument and exits with the status given by the second. */
int main(int argc, char **argv)
{
    if (argc != 3)
        return 99;
    printf("arg=[%s]\n", argv[1]);
    return atoi(argv[2]);
}
