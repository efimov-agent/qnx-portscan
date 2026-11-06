using System.Text.Json;
using QnxPortScan.Core.Reporting;
using QnxPortScan.Core.Rules;
using QnxPortScan.Core.Scanning;

namespace QnxPortScan.Tests;

public class ReporterTests
{
    private static ScanResult Sample()
    {
        var scanner = new Scanner(TestRules.Catalog);
        var findings = scanner.ScanText("src/a.c", "#include <sys/epoll.h>\nint x = epoll_wait(1);");
        return new ScanResult("/tmp/proj", findings, 1, []);
    }

    [Fact]
    public void Sarif_is_valid_2_1_0_shape()
    {
        var json = SarifReporter.Render(Sample(), TestRules.Catalog, "0.1.0");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        var run = root.GetProperty("runs")[0];
        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
        Assert.Equal(TestRules.Catalog.Rules.Count, rules.GetArrayLength());
        var results = run.GetProperty("results");
        Assert.Equal(2, results.GetArrayLength());
        var r1 = results[1];
        Assert.Equal("T-CALL", r1.GetProperty("ruleId").GetString());
        Assert.Equal("T-CALL", rules[r1.GetProperty("ruleIndex").GetInt32()].GetProperty("id").GetString());
        Assert.Equal("error", r1.GetProperty("level").GetString());
        var region = r1.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region");
        Assert.Equal(2, region.GetProperty("startLine").GetInt32());
        Assert.Equal(9, region.GetProperty("startColumn").GetInt32());
        Assert.Equal("file:///tmp/proj/", run.GetProperty("originalUriBaseIds").GetProperty("SRCROOT").GetProperty("uri").GetString());
    }

    [Fact]
    public void Markdown_contains_summary_and_locations()
    {
        var md = MarkdownReporter.Render(Sample());
        Assert.Contains("| T-CALL | Error | epoll | 1 | 1 |", md);
        Assert.Contains("L2:9 **T-CALL** `epoll_wait`", md);
        Assert.Contains("### `src/a.c`", md);
    }

    [Fact]
    public void Markdown_for_clean_project()
    {
        Assert.Contains("No Linux-only API usage found.", MarkdownReporter.Render(new ScanResult("/x", [], 3, [])));
    }
}
