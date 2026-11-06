using System.Text;
using QnxPortScan.Core.Rules;
using QnxPortScan.Core.Scanning;

namespace QnxPortScan.Core.Reporting;

public static class MarkdownReporter
{
    public static string Render(ScanResult result, string? title = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title ?? "QNX portability report"}");
        sb.AppendLine();
        var bySeverity = result.Findings.GroupBy(f => f.Severity).ToDictionary(g => g.Key, g => g.Count());
        sb.AppendLine($"Files scanned: **{result.FilesScanned}** · Findings: **{result.Findings.Count}** " +
                      $"(errors {bySeverity.GetValueOrDefault(Severity.Error)}, warnings {bySeverity.GetValueOrDefault(Severity.Warning)}, notes {bySeverity.GetValueOrDefault(Severity.Note)})");
        if (result.FilesWithQnxGuards.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Files already mentioning `__QNX__`/`__QNXNTO__`: {result.FilesWithQnxGuards.Count}");
        }
        sb.AppendLine();

        if (result.Findings.Count == 0)
        {
            sb.AppendLine("No Linux-only API usage found.");
            return sb.ToString();
        }

        sb.AppendLine("## Summary by rule");
        sb.AppendLine();
        sb.AppendLine("| Rule | Severity | Title | Hits | Files |");
        sb.AppendLine("|---|---|---|---:|---:|");
        foreach (var g in result.Findings.GroupBy(f => f.Rule).OrderByDescending(g => g.Key.Severity).ThenByDescending(g => g.Count()))
        {
            sb.AppendLine($"| {g.Key.Id} | {g.Key.Severity} | {Escape(g.Key.Title)} | {g.Count()} | {g.Select(f => f.File).Distinct().Count()} |");
        }
        sb.AppendLine();

        sb.AppendLine("## How to port");
        sb.AppendLine();
        foreach (var rule in result.Findings.Select(f => f.Rule).Distinct().OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            sb.AppendLine($"### {rule.Id}: {Escape(rule.Title)}");
            sb.AppendLine();
            sb.AppendLine(rule.Message);
            sb.AppendLine();
            sb.AppendLine($"**Suggestion:** {rule.Suggestion}");
            if (rule.Docs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Docs: " + string.Join(" · ", rule.Docs.Select(d => $"<{d}>")));
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Findings by file");
        foreach (var file in result.Findings.GroupBy(f => f.File))
        {
            sb.AppendLine();
            sb.AppendLine($"### `{file.Key}`");
            sb.AppendLine();
            foreach (var f in file)
            {
                var guard = f.LinuxGuarded ? " _(inside Linux-only #ifdef)_" : string.Empty;
                sb.AppendLine($"- L{f.Line}:{f.Column} **{f.Rule.Id}** `{f.Match}`{guard} — `{Truncate(f.Snippet, 120).Replace("`", "'")}`");
            }
        }
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("|", "\\|");

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
