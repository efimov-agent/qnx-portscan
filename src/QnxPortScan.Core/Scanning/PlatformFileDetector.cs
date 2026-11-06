namespace QnxPortScan.Core.Scanning;

/// <summary>
/// Recognises files that the build system compiles only for another platform
/// (e.g. <c>src/unix/linux.c</c>, <c>ae_epoll.c</c>, <c>kqueue.cpp</c>, <c>os/darwin/</c>).
/// Findings there are not portability blockers: the QNX port needs its own backend file instead.
/// </summary>
public static class PlatformFileDetector
{
    private static readonly HashSet<string> Tokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "linux", "epoll", "kqueue", "darwin", "macos", "osx", "apple", "freebsd", "netbsd", "openbsd",
        "dragonfly", "bsd", "android", "sunos", "solaris", "illumos", "aix", "win32", "win", "windows",
        "cygwin", "mingw", "haiku", "os390", "zos", "hurd", "iouring", "uring", "eventport", "evport", "devpoll", "wepoll",
    };

    private static readonly char[] Separators = ['/', '\\', '_', '-', '.', ' '];

    /// <summary>Returns the platform token that marks the path, or null.</summary>
    public static string? Detect(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        var withoutExt = Path.ChangeExtension(path, null) ?? path;
        // Respect explicit QNX files: "qnx_linux_compat.c" is meant for QNX.
        var parts = withoutExt.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => p.Equals("qnx", StringComparison.OrdinalIgnoreCase) || p.Equals("nto", StringComparison.OrdinalIgnoreCase)))
            return null;
        return parts.FirstOrDefault(Tokens.Contains);
    }
}
