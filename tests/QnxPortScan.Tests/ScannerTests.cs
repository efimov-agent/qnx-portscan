using QnxPortScan.Core.Rules;
using QnxPortScan.Core.Scanning;

namespace QnxPortScan.Tests;

public class ScannerTests
{
    private readonly Scanner _scanner = new(TestRules.Catalog);

    [Fact]
    public void Finds_all_rule_kinds_with_positions()
    {
        const string src = """
            #include <sys/epoll.h>
            #include "linux/futex.h"
            #include <stdio.h>
            int main() {
                int fd = epoll_create1(0);
                void* p = mmap(0, 4096, PROT_READ, MAP_PRIVATE | MAP_POPULATE, -1, 0);
                FILE* f = fopen("/proc/cpuinfo", "r");
                return epoll_wait (fd, 0, 0, 0);
            }
            """;
        var f = _scanner.ScanText("a.c", src);
        Assert.Collection(f,
            x => { Assert.Equal("T-INC", x.Rule.Id); Assert.Equal("sys/epoll.h", x.Match); Assert.Equal(1, x.Line); Assert.Equal(11, x.Column); },
            x => { Assert.Equal("T-INC", x.Rule.Id); Assert.Equal("linux/futex.h", x.Match); Assert.Equal(2, x.Line); },
            x => { Assert.Equal("T-CALL", x.Rule.Id); Assert.Equal(5, x.Line); Assert.Equal(14, x.Column); },
            x => { Assert.Equal("T-ID", x.Rule.Id); Assert.Equal(6, x.Line); },
            x => { Assert.Equal("T-LIT", x.Rule.Id); Assert.Equal(7, x.Line); },
            x => { Assert.Equal("T-CALL", x.Rule.Id); Assert.Equal(8, x.Line); Assert.Equal(Severity.Error, x.Severity); });
    }

    [Theory]
    [InlineData("obj.epoll_wait(1);")]
    [InlineData("p->epoll_wait(1);")]
    [InlineData("my_epoll_wait(1);")]
    [InlineData("// epoll_wait(1);")]
    [InlineData("const char* s = \"epoll_wait(1)\";")]
    [InlineData("int epoll_waiter;")]
    [InlineData("#include <stdio.h> // linux/x.h")]
    [InlineData("f(\"/proc/self/maps2\");")]
    public void Does_not_report_false_positives(string line)
    {
        Assert.Empty(_scanner.ScanText("a.c", line));
    }

    [Fact]
    public void Anchored_literal_pattern_matches_whole_literal()
    {
        Assert.Single(_scanner.ScanText("a.c", "f(\"/proc/self/maps\");"));
    }

    [Theory]
    [InlineData("#ifdef __linux__\nepoll_wait(1);\n#endif")]
    [InlineData("#if defined(__linux__) && !defined(FOO)\nepoll_wait(1);\n#endif")]
    [InlineData("#ifndef __linux__\nx();\n#else\nepoll_wait(1);\n#endif")]
    [InlineData("#if 0\nepoll_wait(1);\n#endif")]
    [InlineData("#ifdef __linux__\n#ifdef HAVE_X\nepoll_wait(1);\n#endif\n#endif")]
    [InlineData("#if defined(__APPLE__) || defined(__FreeBSD__)\nepoll_wait(1);\n#endif")]
    [InlineData("#if (defined(__linux__) || defined(__ANDROID__)) && HAVE_EPOLL\nepoll_wait(1);\n#endif")]
    [InlineData("#if !defined(_WIN32)\nx();\n#else\nepoll_wait(1);\n#endif")]
    [InlineData("#ifdef _WIN32\nepoll_wait(1);\n#elif defined(__QNX__)\nx();\n#endif")]
    public void Guarded_or_disabled_code_is_skipped_by_default(string src)
    {
        Assert.Empty(_scanner.ScanText("a.c", src));
    }

    [Theory]
    [InlineData("#ifndef __linux__\nepoll_wait(1);\n#endif")]
    [InlineData("#ifdef __linux__\nx();\n#else\nepoll_wait(1);\n#endif")]
    [InlineData("#if defined(__linux__) || defined(__QNX__)\nepoll_wait(1);\n#endif")]
    [InlineData("#ifdef __linux__\nx();\n#endif\nepoll_wait(1);")]
    [InlineData("#if 0\nx();\n#else\nepoll_wait(1);\n#endif")]
    [InlineData("#if defined(__linux__) || defined(__unix__)\nepoll_wait(1);\n#endif")]
    [InlineData("#if defined(__linux__) || HAVE_EPOLL\nepoll_wait(1);\n#endif")]
    [InlineData("#ifdef __APPLE__\nx();\n#elif defined(HAVE_EPOLL)\nepoll_wait(1);\n#endif")]
    public void Unguarded_code_is_reported(string src)
    {
        Assert.Single(_scanner.ScanText("a.c", src));
    }

    [Fact]
    public void Guarded_code_reported_as_note_when_requested()
    {
        var f = Assert.Single(_scanner.ScanText("a.c", "#ifdef __linux__\nepoll_wait(1);\n#endif", new ScanOptions { IncludeGuarded = true }));
        Assert.True(f.Guarded);
        Assert.Equal(Severity.Note, f.Severity);
    }

    [Fact]
    public async Task Directory_scan_respects_extensions_and_excludes()
    {
        var root = Directory.CreateTempSubdirectory("qps-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            Directory.CreateDirectory(Path.Combine(root, "third_party"));
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            await File.WriteAllTextAsync(Path.Combine(root, "src", "a.cpp"), "epoll_wait(1);\n#ifdef __QNX__\n#endif");
            await File.WriteAllTextAsync(Path.Combine(root, "src", "notes.txt"), "epoll_wait(1);");
            await File.WriteAllTextAsync(Path.Combine(root, "third_party", "b.c"), "epoll_wait(1);");
            await File.WriteAllTextAsync(Path.Combine(root, ".git", "c.c"), "epoll_wait(1);");

            var all = await _scanner.ScanDirectoryAsync(root);
            Assert.Equal(2, all.FilesScanned);
            Assert.Equal(["src/a.cpp", "third_party/b.c"], all.Findings.Select(f => f.File).ToArray());
            Assert.Equal(["src/a.cpp"], all.FilesWithQnxGuards);

            var some = await _scanner.ScanDirectoryAsync(root, new ScanOptions { ExcludePaths = ["third_party/"] });
            Assert.Equal("src/a.cpp", Assert.Single(some.Findings).File);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public class PlatformFileDetectorTests
{
    [Theory]
    [InlineData("src/unix/linux.c", "linux")]
    [InlineData("src/ae_epoll.c", "epoll")]
    [InlineData("src/kqueue.cpp", "kqueue")]
    [InlineData("os/darwin/proc.c", "darwin")]
    [InlineData("src/win/core.c", "win")]
    [InlineData("src/qnx/linux_compat.c", null)]
    [InlineData("src/window.c", null)]
    [InlineData("src/poller.cpp", null)]
    public void Detects_platform_files(string path, string? expected)
    {
        Assert.Equal(expected, QnxPortScan.Core.Scanning.PlatformFileDetector.Detect(path));
    }

    [Fact]
    public void Findings_in_platform_files_are_hidden_by_default()
    {
        var s = new Scanner(TestRules.Catalog);
        Assert.Empty(s.ScanText("src/ae_epoll.c", "epoll_wait(1);"));
        var f = Assert.Single(s.ScanText("src/ae_epoll.c", "epoll_wait(1);", new ScanOptions { IncludeGuarded = true }));
        Assert.Equal("platform-specific file (epoll)", f.GuardReason);
    }
}
