using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QnxPortScan.Core.Lexing;
using QnxPortScan.Core.Rules;

namespace QnxPortScan.Core.Scanning;

public sealed partial class Scanner
{
    private readonly RuleMatcher _matcher;
    private readonly ILogger<Scanner> _logger;

    public Scanner(RuleCatalog catalog, ILogger<Scanner>? logger = null)
    {
        _matcher = new RuleMatcher(catalog);
        _logger = logger ?? NullLogger<Scanner>.Instance;
    }

    [GeneratedRegex(@"\b(__QNX__|__QNXNTO__)\b")]
    private static partial Regex QnxGuardRegex();

    /// <summary>Scans a single in-memory source. Path is used for reporting only.</summary>
    public IReadOnlyList<Finding> ScanText(string relativePath, string text, ScanOptions? options = null)
    {
        options ??= new ScanOptions();
        var view = CppSourceView.Parse(text);
        var contexts = PreprocessorTracker.Analyze(view);
        var findings = new List<Finding>();
        foreach (var (rule, offset, match) in _matcher.Match(view))
        {
            var (line, col) = view.GetPosition(offset);
            var ctx = contexts[line];
            if (ctx == PreprocessorTracker.LineContext.Disabled) continue;
            var guarded = ctx == PreprocessorTracker.LineContext.LinuxGuarded;
            if (guarded && !options.IncludeGuarded) continue;
            findings.Add(new Finding(
                rule, relativePath, line, col, match,
                view.GetLineText(line).Trim(),
                guarded ? Severity.Note : rule.Severity,
                guarded));
        }
        return findings
            .DistinctBy(f => (f.Rule.Id, f.Line, f.Column))
            .OrderBy(f => f.Line).ThenBy(f => f.Column)
            .ToList();
    }

    public async Task<ScanResult> ScanDirectoryAsync(string root, ScanOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ScanOptions();
        root = Path.GetFullPath(root);
        if (File.Exists(root))
        {
            var single = ScanText(Path.GetFileName(root), await File.ReadAllTextAsync(root, ct), options);
            return new ScanResult(Path.GetDirectoryName(root)!, single, 1, []);
        }
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var files = EnumerateFiles(root, options).ToList();
        _logger.LogInformation("Scanning {Count} files under {Root}", files.Count, root);

        var all = new ConcurrentBag<Finding>();
        var qnxGuarded = new ConcurrentBag<string>();
        await Parallel.ForEachAsync(files, ct, async (file, token) =>
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            try
            {
                var info = new FileInfo(file);
                if (info.Length > options.MaxFileBytes)
                {
                    _logger.LogWarning("Skipping large file {File} ({Bytes} bytes)", rel, info.Length);
                    return;
                }
                var text = await File.ReadAllTextAsync(file, token);
                if (QnxGuardRegex().IsMatch(text)) qnxGuarded.Add(rel);
                foreach (var f in ScanText(rel, text, options)) all.Add(f);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Cannot read {File}", rel);
            }
        });

        var ordered = all
            .OrderBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Line).ThenBy(f => f.Column)
            .ToList();
        return new ScanResult(root, ordered, files.Count, qnxGuarded.Order(StringComparer.Ordinal).ToList());
    }

    private static IEnumerable<string> EnumerateFiles(string root, ScanOptions options)
    {
        var exts = new HashSet<string>(options.Extensions, StringComparer.OrdinalIgnoreCase);
        var excludedDirs = new HashSet<string>(options.ExcludedDirectories, StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            IEnumerable<string> subdirs, files;
            try
            {
                subdirs = Directory.EnumerateDirectories(dir);
                files = Directory.EnumerateFiles(dir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var sub in subdirs)
            {
                var name = Path.GetFileName(sub);
                if (excludedDirs.Contains(name)) continue;
                if (new DirectoryInfo(sub).LinkTarget is not null) continue; // no symlink loops
                if (IsExcluded(root, sub + "/", options)) continue;
                pending.Push(sub);
            }
            foreach (var f in files)
            {
                if (exts.Contains(Path.GetExtension(f)) && !IsExcluded(root, f, options)) yield return f;
            }
        }
    }

    private static bool IsExcluded(string root, string path, ScanOptions options)
    {
        if (options.ExcludePaths.Count == 0) return false;
        var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (path.EndsWith('/') && !rel.EndsWith('/')) rel += "/";
        return options.ExcludePaths.Any(p => rel.StartsWith(p.TrimStart('/'), StringComparison.Ordinal));
    }
}
