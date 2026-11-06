using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QnxPortScan.Core.Lexing;
using QnxPortScan.Core.Rules;

namespace QnxPortScan.Core.Scanning;

public sealed class Scanner
{
    private readonly RuleMatcher _matcher;
    private readonly ILogger<Scanner> _logger;

    public Scanner(RuleCatalog catalog, ILogger<Scanner>? logger = null)
    {
        _matcher = new RuleMatcher(catalog);
        _logger = logger ?? NullLogger<Scanner>.Instance;
    }

    /// <summary>Scans a single in-memory source. Path is used for reporting only.</summary>
    public IReadOnlyList<Finding> ScanText(string relativePath, string text, ScanOptions? options = null)
    {
        options ??= new ScanOptions();
        var (findings, defined) = ScanCore(relativePath, text, options);
        return Order(ApplyProjectDefinitions(findings, defined, options)).ToList();
    }

    private (List<Finding> Findings, HashSet<string> DefinedNames) ScanCore(string relativePath, string text, ScanOptions options)
    {
        var view = CppSourceView.Parse(text);
        var contexts = PreprocessorTracker.Analyze(view);
        var platformFile = PlatformFileDetector.Detect(relativePath);
        var findings = new List<Finding>();
        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hit in _matcher.Match(view))
        {
            if (hit.Kind == HitKind.Definition) defined.Add(hit.Match);
            if (hit.Kind != HitKind.Use) continue;
            var (line, col) = view.GetPosition(hit.Offset);
            var ctx = contexts[line];
            if (ctx == PreprocessorTracker.LineContext.Disabled) continue;
            var reason = ctx == PreprocessorTracker.LineContext.LinuxGuarded
                ? "inside a non-QNX platform #if branch"
                : platformFile is not null ? $"platform-specific file ({platformFile})" : null;
            if (reason is not null && !options.IncludeGuarded) continue;
            findings.Add(new Finding(
                hit.Rule, relativePath, line, col, hit.Match,
                view.GetLineText(line).Trim(),
                reason is not null ? Severity.Note : hit.Rule.Severity,
                reason));
        }
        return (findings, defined);
    }

    /// <summary>Calls to functions the project defines itself (own itoa, a wepoll shim) are not Linux API uses.</summary>
    private static IEnumerable<Finding> ApplyProjectDefinitions(IEnumerable<Finding> findings, HashSet<string> defined, ScanOptions options)
    {
        foreach (var f in findings)
        {
            if (f.Rule.Kind == RuleKind.Call && defined.Contains(f.Match))
            {
                if (!options.IncludeGuarded) continue;
                yield return f with { Severity = Severity.Note, GuardReason = f.GuardReason ?? $"project defines its own {f.Match}()" };
                continue;
            }
            yield return f;
        }
    }

    private static IEnumerable<Finding> Order(IEnumerable<Finding> findings) =>
        findings
            .DistinctBy(f => (f.Rule.Id, f.File, f.Line, f.Column))
            .OrderBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Line).ThenBy(f => f.Column)
            .ThenBy(f => f.Rule.Id, StringComparer.Ordinal);

    public async Task<ScanResult> ScanDirectoryAsync(string root, ScanOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ScanOptions();
        root = Path.GetFullPath(root);
        if (File.Exists(root))
        {
            var text = await File.ReadAllTextAsync(root, ct);
            var single = ScanText(Path.GetFileName(root), text, options);
            return new ScanResult(Path.GetDirectoryName(root)!, single, 1,
                PreprocessorTracker.QnxMacroRegex().IsMatch(text) ? [Path.GetFileName(root)] : []);
        }
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var files = EnumerateFiles(root, options).ToList();
        _logger.LogInformation("Scanning {Count} files under {Root}", files.Count, root);

        var all = new ConcurrentBag<Finding>();
        var defined = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
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
                if (PreprocessorTracker.QnxMacroRegex().IsMatch(text)) qnxGuarded.Add(rel);
                var (found, names) = ScanCore(rel, text, options);
                foreach (var f in found) all.Add(f);
                foreach (var n in names) defined.TryAdd(n, 0);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Cannot read {File}", rel);
            }
            catch (RegexMatchTimeoutException ex)
            {
                _logger.LogWarning("Rule pattern timed out on {File}: {Pattern}", rel, ex.Pattern);
            }
        });

        var definedNames = defined.Keys.ToHashSet(StringComparer.Ordinal);
        var ordered = Order(ApplyProjectDefinitions(all, definedNames, options)).ToList();
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
                if (new DirectoryInfo(sub).LinkTarget is not null) continue; // skip all symlinked dirs (avoids loops and escaping the root)
                if (IsExcluded(root, sub + "/", options)) continue;
                pending.Push(sub);
            }
            foreach (var f in files.Order(StringComparer.Ordinal))
            {
                if (exts.Contains(Path.GetExtension(f)) && !IsExcluded(root, f, options)) yield return f;
            }
        }
    }

    private static bool IsExcluded(string root, string path, ScanOptions options)
    {
        if (options.ExcludePaths.Count == 0) return false;
        var rel = Path.GetRelativePath(root, path.TrimEnd('/')).Replace('\\', '/');
        foreach (var raw in options.ExcludePaths)
        {
            var p = raw.Replace('\\', '/').Trim();
            if (p.StartsWith("./", StringComparison.Ordinal)) p = p[2..];
            p = p.Trim('/');
            if (p.Length == 0) continue;
            if (rel == p || rel.StartsWith(p + "/", StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
