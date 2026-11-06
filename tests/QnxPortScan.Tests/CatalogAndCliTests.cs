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
