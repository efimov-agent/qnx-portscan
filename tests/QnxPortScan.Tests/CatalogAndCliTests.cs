using QnxPortScan.Cli;
using QnxPortScan.Core.Rules;

namespace QnxPortScan.Tests;

public class CatalogAndCliTests
{
    [Fact]
    public void Built_in_catalog_loads_and_is_well_formed()
    {
        var c = RuleCatalog.LoadBuiltIn();
        Assert.NotEmpty(c.Rules);
        Assert.All(c.Rules, r =>
        {
            Assert.Matches(@"^QNX\d{3}$", r.Id);
            Assert.False(string.IsNullOrWhiteSpace(r.Message));
            Assert.False(string.IsNullOrWhiteSpace(r.Suggestion));
            Assert.All(r.Docs, d => Assert.StartsWith("https://", d));
        });
        // Every rule must compile.
        _ = new QnxPortScan.Core.Scanning.RuleMatcher(c);
    }

    [Fact]
    public void Qnx056_flags_sock_maxaddrlen_as_libuv_uses_it()
    {
        var scanner = new QnxPortScan.Core.Scanning.Scanner(RuleCatalog.LoadBuiltIn());
        // Shape of the libuv code that passed this size to bind()/connect() for AF_UNIX.
        const string src = "char buf[SOCK_MAXADDRLEN];\nsocklen_t len = sizeof(buf);\n";
        var f = Assert.Single(scanner.ScanText("pipe.c", src));
        Assert.Equal("QNX056", f.Rule.Id);
        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Equal(1, f.Line);
    }

    [Theory]
    [InlineData("// SOCK_MAXADDRLEN is 255 on QNX")]
    [InlineData("const char* s = \"SOCK_MAXADDRLEN\";")]
    [InlineData("int MY_SOCK_MAXADDRLEN = 1;")]
    [InlineData("#ifdef __linux__\nchar b[SOCK_MAXADDRLEN];\n#endif")]
    public void Qnx056_ignores_comments_strings_lookalikes_and_guarded_code(string src)
    {
        var scanner = new QnxPortScan.Core.Scanning.Scanner(RuleCatalog.LoadBuiltIn());
        Assert.DoesNotContain(scanner.ScanText("a.c", src), x => x.Rule.Id == "QNX056");
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => RuleCatalog.FromJson("""
            [ {"id":"A","title":"t","kind":"Call","patterns":["x"],"message":"m","suggestion":"s"},
              {"id":"A","title":"t","kind":"Call","patterns":["y"],"message":"m","suggestion":"s"} ]
            """));
    }

    [Fact]
    public async Task Cli_scan_writes_both_reports_and_fails_on_error()
    {
        var root = Directory.CreateTempSubdirectory("qps-cli-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "x.c"), "#include <sys/epoll.h>\nint main(){return epoll_wait(0,0,0,0);}");
            var outBase = Path.Combine(root, "report");
            var stdout = new StringWriter();
            var code = await Program.RunAsync(["scan", root, "--format", "both", "--out", outBase, "--fail-on", "error"], stdout, new StringWriter());
            Assert.Equal(Program.ExitFindings, code);
            Assert.True(File.Exists(outBase + ".md"));
            Assert.True(File.Exists(outBase + ".sarif"));
            Assert.Contains("x.c", await File.ReadAllTextAsync(outBase + ".md"));

            code = await Program.RunAsync(["scan", root], new StringWriter(), new StringWriter());
            Assert.Equal(Program.ExitOk, code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("frobnicate")]
    [InlineData("scan")]
    [InlineData("scan", ".", "--format", "xml")]
    [InlineData("scan", ".", "--bogus")]
    public async Task Cli_usage_errors_return_2(params string[] args)
    {
        var err = new StringWriter();
        Assert.Equal(Program.ExitUsage, await Program.RunAsync(args, new StringWriter(), err));
        Assert.Contains("error:", err.ToString());
    }
}
