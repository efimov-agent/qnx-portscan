using QnxPortScan.Core.Rules;

namespace QnxPortScan.Core.Scanning;

public sealed record Finding(
    Rule Rule,
    string File,
    int Line,
    int Column,
    string Match,
    string Snippet,
    Severity Severity,
    string? GuardReason)
{
    /// <summary>True when the match is in code that is not compiled for QNX anyway.</summary>
    public bool Guarded => GuardReason is not null;
}

public sealed record ScanResult(string Root, IReadOnlyList<Finding> Findings, int FilesScanned, IReadOnlyList<string> FilesWithQnxGuards);
