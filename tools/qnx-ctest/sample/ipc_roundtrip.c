/* Exercises QNX native message passing, which only works on a real QNX kernel. */
#include <pthread.h>
#include <stdio.h>
#include <string.h>
#include <sys/neutrino.h>

static int chid;

static void *server(void *arg)
{
    char buf[16];
    (void)arg;
    int rcvid = MsgReceive(chid, buf, sizeof buf, NULL);
    if (rcvid > 0) {
        buf[0] = 'P';
        MsgReply(rcvid, 0, buf, sizeof buf);
    }
    return NULL;
}

int main(void)
{
    pthread_t thread;
    char reply[16] = "";
    chid = ChannelCreate(0);
    if (chid == -1 || pthread_create(&thread, NULL, server, NULL) != 0)
        return 2;
    int coid = ConnectAttach(0, 0, chid, _NTO_SIDE_CHANNEL, 0);
    if (coid == -1 || MsgSend(coid, "ping", 5, reply, sizeof reply) == -1)
        return 3;
    pthread_join(thread, NULL);
    printf("reply=%s\n", reply);
    return strcmp(reply, "Ping") == 0 ? 0 : 4;
}
