#!/usr/bin/env python3
"""Generates src/QnxPortScan.Core/Rules/rules.json. Source of truth for the facts:
research notes checked against QNX SDP 8.0 docs (2026-11-06). Edit here, then re-run."""
import json, pathlib
LR = "https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.lib_ref/topic/"
PG = "https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.prog/topic/"
MIG = "https://www.qnx.com/developers/docs/8.0/com.qnx.doc.qnxsdp.migration/topic/"
IOS = "https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.io_sock/topic/socket_api.html"
def idx(c): return f"{LR}lib-{c}.html"
NA = "Not documented in the QNX SDP 8.0 C Library Reference."
rules = []
def r(id, title, kind, sev, cat, patterns, message, suggestion, docs):
    rules.append(dict(id=id, title=title, kind=kind, severity=sev, category=cat,
                      patterns=patterns, message=message, suggestion=suggestion, docs=docs))

# --- headers ---
r("QNX001","Linux kernel headers","Include","Error","headers",["linux/","asm/"],
  "<linux/...> and <asm/...> expose the Linux kernel ABI; QNX Neutrino has no such headers.",
  "Guard with #ifdef __linux__ and provide a QNX path: device control goes through devctl() or a resource manager.",
  [f"{LR}d/devctl.html"])
r("QNX002","epoll","Include","Error","io",["sys/epoll.h"], f"<sys/epoll.h>: {NA}",
  "Use poll()/ppoll()/select(), or ionotify() with pulses for an event-driven QNX design.", [f"{LR}i/ionotify.html", f"{LR}p/poll.html"])
r("QNX003","epoll","Call","Error","io",["epoll_create","epoll_create1","epoll_ctl","epoll_wait","epoll_pwait","epoll_pwait2"],
  f"epoll is Linux-only. {NA}",
  "Simple path: poll()/ppoll(). Native path: ionotify() arms a sigevent per fd; receive pulses on a channel (ChannelCreate + MsgReceivePulse). QNX docs describe ionotify as the epoll/kqueue counterpart.",
  [f"{LR}i/ionotify.html", f"{LR}m/msgreceivepulse.html", f"{LR}p/poll.html"])
r("QNX004","kqueue","Call","Error","io",["kqueue","kevent"], f"kqueue/kevent: {NA}",
  "Use ionotify() + pulses, or poll().", [f"{LR}i/ionotify.html", idx("k")])
r("QNX005","eventfd","Call","Error","io",["eventfd","eventfd_read","eventfd_write"], f"eventfd: {NA}",
  "For a poll()-based loop use a self-pipe via pipe2(O_NONBLOCK|O_CLOEXEC); in a channel-based design send a pulse with MsgSendPulse().",
  [f"{LR}p/pipe2.html", f"{LR}m/msgsendpulse.html", idx("e")])
r("QNX006","eventfd","Include","Error","io",["sys/eventfd.h"], f"<sys/eventfd.h>: {NA}",
  "Replace with a self-pipe (pipe2) or pulses (MsgSendPulse).", [f"{LR}p/pipe2.html"])
r("QNX007","signalfd","Call","Error","signals",["signalfd"], f"signalfd: {NA}",
  "Block the signals and handle them in a dedicated thread with sigwaitinfo()/sigtimedwait(); forward to the main loop via a pipe or pulse.",
  [f"{LR}s/sigwaitinfo.html", idx("s")])
r("QNX008","signalfd","Include","Error","signals",["sys/signalfd.h"], f"<sys/signalfd.h>: {NA}",
  "Use a sigwaitinfo() thread instead.", [f"{LR}s/sigwaitinfo.html"])
r("QNX009","timerfd","Call","Error","timers",["timerfd_create","timerfd_settime","timerfd_gettime"], f"timerfd: {NA}",
  "Create a POSIX timer with timer_create() and a SIGEV_PULSE sigevent, arm it with timer_settime(), receive expirations with MsgReceivePulse().",
  [f"{LR}t/timer_create.html", f"{LR}s/sigevent.html", f"{LR}t/timer_settime.html"])
r("QNX010","timerfd","Include","Error","timers",["sys/timerfd.h"], f"<sys/timerfd.h>: {NA}",
  "Use timer_create() with SIGEV_PULSE.", [f"{LR}t/timer_create.html"])
r("QNX011","inotify (libfsnotify)","Call","Warning","fs",["inotify_init","inotify_init1","inotify_add_watch","inotify_rm_watch"],
  "inotify exists on QNX 8.0 but lives in libfsnotify (link with -l fsnotify), needs the fsevmgr service running (otherwise ENOSYS) and only covers io-blk.so based filesystems and devf-ram.",
  "Link -l fsnotify, start fsevmgr in the target image, and test on the actual filesystem; behaviour differs from Linux in details.",
  [f"{LR}i/inotify_init.html", f"{LR}i/inotify_qnx_ext.html", f"{MIG}c_apps.html"])
r("QNX012","fanotify","Call","Error","fs",["fanotify_init","fanotify_mark"], f"fanotify: {NA}",
  "Use inotify via libfsnotify (with its limits).", [f"{LR}i/inotify_init.html"])
r("QNX013","prctl","Call","Error","process",["prctl"], f"prctl: {NA}",
  "PR_SET_NAME -> pthread_setname_np() or ThreadCtl(_NTO_TCTL_NAME). For privileges see procmgr_ability(). Other options have no direct equivalent.",
  [f"{LR}p/pthread_setname_np.html", f"{LR}t/threadctl.html", f"{LR}p/procmgr_ability.html"])
r("QNX014","prctl","Include","Error","process",["sys/prctl.h"], f"<sys/prctl.h>: {NA}",
  "See QNX013.", [f"{LR}t/threadctl.html"])
r("QNX015","sendfile/splice","Call","Error","io",["sendfile","sendfile64","splice","tee","vmsplice","copy_file_range"], f"Zero-copy Linux syscalls: {NA}",
  "Use a read()/write() loop (or mmap() + write()).", [idx("s"), IOS])
r("QNX016","sendfile","Include","Error","io",["sys/sendfile.h"], f"<sys/sendfile.h>: {NA}",
  "Use a read()/write() loop.", [idx("s")])
r("QNX017","clone()","Call","Error","process",["clone3","unshare","setns"], f"clone/namespaces: {NA}",
  "Use pthread_create(), fork() or posix_spawn(). Linux namespaces have no equivalent.",
  [f"{LR}p/posix_spawn.html", f"{LR}f/fork.html"])
r("QNX053","clone() flags","Identifier","Error","process",["CLONE_VM","CLONE_FS","CLONE_FILES","CLONE_SIGHAND","CLONE_THREAD","CLONE_NEWNS","CLONE_NEWPID","CLONE_NEWNET","CLONE_NEWUSER","CLONE_VFORK"],
  f"clone() and its CLONE_* flags: {NA}",
  "Use pthread_create(), fork() or posix_spawn(). Linux namespaces have no equivalent.",
  [f"{LR}p/posix_spawn.html", f"{LR}f/fork.html"])
r("QNX018","raw syscall()","Call","Error","process",["syscall"], f"syscall(): {NA} Linux syscall numbers do not apply to QNX.",
  "Call the libc wrapper, or the QNX kernel call (MsgSend, ThreadCtl, ...).", [idx("s")])
r("QNX019","raw syscall()","Include","Error","process",["sys/syscall.h"], f"<sys/syscall.h> (SYS_* numbers): {NA}",
  "Drop it; use libc wrappers. For thread id use gettid() from <process.h>.", [f"{LR}g/gettid.html"])
r("QNX020","gettid()","Call","Warning","process",["gettid"],
  "gettid() exists on QNX 8.0 but is declared in <process.h> (not <unistd.h>) and returns int.",
  "#include <process.h> on QNX.", [f"{LR}g/gettid.html"])
r("QNX021","sysinfo()","Call","Error","system",["sysinfo"], f"sysinfo(): {NA}",
  "CPU count and memory: system page (_syspage_ptr, SYSPAGE_ENTRY) or devctl DCMD_PROC_SYSINFO; uptime: clock_gettime(CLOCK_MONOTONIC). Note: sysconf(_SC_NPROCESSORS_ONLN) is not documented in 8.0.",
  [f"{LR}s/_syspage_ptr.html", f"{LR}s/syspage_entry.html", f"{PG}process_DCMD_PROC_SYSINFO.html"])
r("QNX022","sysinfo()","Include","Error","system",["sys/sysinfo.h"], f"<sys/sysinfo.h>: {NA}", "See QNX021.", [f"{LR}s/_syspage_ptr.html"])
r("QNX023","CPU affinity","Call","Error","sched",["sched_setaffinity","sched_getaffinity","pthread_setaffinity_np","pthread_getaffinity_np"],
  f"Linux affinity API: {NA}",
  "Use ThreadCtl(_NTO_TCTL_RUNMASK...) from the thread itself (the mask must match a CPU cluster in 8.0), pthread_cluster_set(), or posix_spawnattr_setrunmask() for children.",
  [f"{LR}t/threadctl.html", f"{LR}p/pthread_cluster_set.html", f"{LR}p/posix_spawnattr_setrunmask.html"])
r("QNX024","cpu_set_t macros","Identifier","Error","sched",["cpu_set_t","CPU_SET","CPU_ZERO","CPU_ISSET","CPU_CLR","CPU_COUNT"],
  f"cpu_set_t / CPU_* macros: {NA}",
  "Use a uint64_t run mask with ThreadCtl(_NTO_TCTL_RUNMASK).", [f"{LR}t/threadctl.html"])
r("QNX025","sched_getcpu()","Call","Error","sched",["sched_getcpu"], f"sched_getcpu(): {NA}",
  "Use SchedGetCpuNum() from <sys/neutrino.h>.", [f"{LR}s/schedgetcpunum.html"])
r("QNX026","mremap()","Call","Error","memory",["mremap"], f"mremap(): {NA}",
  "mmap() a new region, memcpy(), munmap() the old one.", [idx("m")])
r("QNX027","memfd_create()","Call","Error","memory",["memfd_create"], f"memfd_create(): {NA}",
  "shm_open(SHM_ANON, ...) + ftruncate(); share across processes with shm_create_handle()/shm_open_handle().",
  [f"{LR}s/shm_open.html", f"{LR}s/shm_create_handle.html"])
r("QNX028","MAP_POPULATE","Identifier","Warning","memory",["MAP_POPULATE"], "MAP_POPULATE is not a documented mmap() flag on QNX 8.0.",
  "Drop it: QNX 8.0 allocates mapped memory eagerly (MAP_LAZY has no effect).", [f"{LR}m/mmap.html"])
r("QNX029","MAP_ANONYMOUS","Identifier","Note","memory",["MAP_ANONYMOUS"], "Only MAP_ANON is documented for QNX 8.0 mmap(); a MAP_ANONYMOUS alias is not confirmed.",
  "Prefer MAP_ANON (with fd = NOFD / -1) for portability.", [f"{LR}m/mmap.html"])
r("QNX030","getrandom()","Call","Error","crypto",["getrandom","getentropy","arc4random","arc4random_buf","arc4random_uniform"],
  f"getrandom/getentropy/arc4random: {NA}",
  "Read /dev/urandom (provided by the random service, which must be running in the image).",
  ["https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.utilities/topic/r/random.html", idx("g")])
r("QNX031","getrandom()","Include","Error","crypto",["sys/random.h"], f"<sys/random.h>: {NA}", "Read /dev/urandom.",
  ["https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.utilities/topic/r/random.html"])
r("QNX032","getauxval()","Call","Error","system",["getauxval"], f"getauxval(): {NA}",
  "QNX exposes the aux vector as the global auxv_t *_auxv (declare it extern yourself).", [f"{LR}a/_auxv.html"])
r("QNX033","backtrace()","Call","Error","debug",["backtrace","backtrace_symbols","backtrace_symbols_fd"], f"glibc backtrace(): {NA}",
  "QNX ships libbacktrace (bt_* API) but marks it unsupported; prefer core dumps + gdb.",
  ["https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.technotes/topic/backtrace.html"])
r("QNX034","backtrace()","Include","Error","debug",["execinfo.h"], f"<execinfo.h>: {NA}", "See QNX033.",
  ["https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.technotes/topic/backtrace.html"])
r("QNX035","ptrace()","Call","Error","debug",["ptrace"], f"ptrace(): {NA}",
  "Debug control goes through /proc/<pid>/ctl with devctl() (DCMD_PROC_STOP, DCMD_PROC_RUN, DCMD_PROC_GETGREG, ...); memory through /proc/<pid>/as.",
  [f"{PG}process_proc_filesystem.html", f"{PG}process_DCMD_PROC_INFO.html"])
r("QNX036","io_uring","Call","Error","io",["io_uring_setup","io_uring_enter","io_uring_register","io_uring_queue_init","io_uring_submit","io_uring_get_sqe","io_uring_wait_cqe"],
  f"io_uring: {NA}", "Use POSIX AIO (aio_read/aio_write/aio_suspend) or a worker thread pool.", [f"{LR}a/aio_read.html"])
r("QNX037","io_uring","Include","Error","io",["liburing.h"], "liburing is Linux-only.", "See QNX036.", [f"{LR}a/aio_read.html"])
r("QNX038","mount table","Call","Error","fs",["setmntent","getmntent","getmntent_r","endmntent","addmntent"], f"<mntent.h> API: {NA}",
  "getfsent()/getfsfile() read /etc/fstab only; there is no documented equivalent for the live mount table.", [f"{LR}g/getfsent.html"])
r("QNX039","mount table","Include","Error","fs",["mntent.h"], f"<mntent.h>: {NA}", "See QNX038.", [f"{LR}g/getfsent.html"])
r("QNX040","statfs()","Call","Error","fs",["statfs","fstatfs","statfs64","fstatfs64"], f"statfs/fstatfs: {NA}",
  "Use POSIX statvfs()/fstatvfs().", [f"{LR}s/statvfs.html", f"{LR}f/fstatvfs.html"])
r("QNX041","statfs()","Include","Error","fs",["sys/vfs.h","sys/statfs.h"], f"<sys/vfs.h>: {NA}", "Use <sys/statvfs.h>.", [f"{LR}s/statvfs.html"])
r("QNX042","dup3()","Call","Error","io",["dup3"], f"dup3(): {NA}", "dup2() + fcntl(fd, F_SETFD, FD_CLOEXEC).", [f"{LR}d/dup2.html"])
r("QNX043","O_PATH","Identifier","Error","fs",["O_PATH"], "O_PATH is not among the documented open() flags on QNX 8.0.",
  "Open normally (O_RDONLY | O_DIRECTORY where applicable).", [f"{LR}o/open.html"])
r("QNX044","SOCK_NONBLOCK/SOCK_CLOEXEC","Identifier","Note","net",["SOCK_NONBLOCK","SOCK_CLOEXEC"],
  "io-sock follows FreeBSD 14.3 socket(2), which has these flags, but QNX does not confirm them explicitly.",
  "If the build fails, use fcntl(O_NONBLOCK)/ioctl(FIONBIO) and fcntl(FD_CLOEXEC) after socket().",
  [IOS, "https://www.qnx.com/developers/docs/8.0/com.qnx.doc.neutrino.devctl/topic/fio/fionbio.html"])
r("QNX045","GNU strerror_r","Identifier","Note","libc",["_GNU_SOURCE"],
  "_GNU_SOURCE suggests reliance on GNU extensions; e.g. QNX has only the POSIX int strerror_r(), not the GNU char* variant.",
  "Review GNU-only calls in this file (strerror_r return value, qsort_r, program_invocation_name, ...).", [f"{LR}s/strerror_r.html"])
r("QNX046","sysfs","Literal","Error","procfs",[r"^/sys/"], "QNX has no /sys filesystem.",
  "Query the information through the system page, devctl() or io-sock sysctl() (network).", [f"{PG}process_proc_filesystem.html"])
r("QNX047","Linux /proc files","Literal","Error","procfs",[r"^/proc/(cpuinfo|meminfo|stat|uptime|loadavg|version|mounts|net/|sys/|filesystems|interrupts|modules|partitions|diskstats)"],
  "QNX /proc only has per-process directories (as, cmdline, ctl, exefile, mappings, pmap, vmstat); Linux system files like /proc/cpuinfo do not exist.",
  "CPU/memory info: system page (_syspage_ptr, SYSPAGE_CPU_ENTRY), SchedGetCpuNum(), devctl DCMD_PROC_SYSINFO.",
  [f"{PG}process_proc_filesystem.html", f"{LR}s/syspage_cpu_entry.html"])
r("QNX048","/proc/self/exe","Literal","Error","procfs",[r"^/proc/(self|%d|\d+)/exe$"],
  "QNX has /proc/<pid>/exefile instead of the exe symlink; exefile holds the path as content, so readlink() does not work on it.",
  "Read /proc/<pid>/exefile as a file, or use _cmdname().", [f"{PG}process_proc_filesystem.html", f"{LR}c/_cmdname.html"])
r("QNX049","/proc/<pid> Linux files","Literal","Error","procfs",[r"^/proc/(self|%d|\d+)/(maps|smaps|status|stat|statm|fd/?|task/?|environ|limits|mountinfo|cgroup|oom_score_adj|comm)"],
  "Per-process Linux procfs files are not available; QNX offers as, cmdline, ctl, exefile, mappings, pmap and vmstat.",
  "Memory map: /proc/<pid>/mappings or pmap, or devctl DCMD_PROC_MAPINFO; process info: devctl DCMD_PROC_INFO.",
  [f"{PG}process_proc_filesystem.html", f"{PG}process_DCMD_PROC_MAPINFO.html"])
r("QNX050","Linux GNU extensions","Call","Error","libc",["qsort_r","getdents","getdents64","syncfs","fallocate","pthread_tryjoin_np","pthread_getattr_np","getloadavg","explicit_bzero","setproctitle"],
  f"GNU/Linux libc extension: {NA}",
  "qsort_r -> qsort() with TLS context; getdents -> readdir(); syncfs -> fsync()/sync(); fallocate -> posix_fallocate(); pthread_tryjoin_np -> pthread_timedjoin(); explicit_bzero -> volatile memset.",
  [f"{LR}p/posix_fallocate.html", f"{LR}p/pthread_timedjoin.html", f"{LR}q/qsort.html"])
r("QNX051","program_invocation_name","Identifier","Error","libc",["program_invocation_name","program_invocation_short_name"], f"{NA}",
  "Use getprogname() or __progname.", [f"{LR}g/getprogname.html", f"{LR}p/__progname.html"])
r("QNX052","itoa/ltoa removed","Call","Error","libc",["itoa","ltoa","utoa","ultoa","lltoa","ulltoa"],
  "itoa()/ltoa() and friends were removed in QNX SDP 8.0 (they existed in older QNX releases).", "Use snprintf().", [f"{MIG}c_apps.html"])

out = pathlib.Path(__file__).resolve().parent.parent / "src/QnxPortScan.Core/Rules/rules.json"
out.write_text("// Generated by tools/gen_rules.py — do not edit by hand.\n" + json.dumps(rules, indent=2, ensure_ascii=False) + "\n")
print(f"{len(rules)} rules -> {out}")
