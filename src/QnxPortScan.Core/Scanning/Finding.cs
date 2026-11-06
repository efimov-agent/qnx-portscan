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
    bool LinuxGuarded);

public sealed record ScanResult(string Root, IReadOnlyList<Finding> Findings, int FilesScanned, IReadOnlyList<string> FilesWithQnxGuards);
