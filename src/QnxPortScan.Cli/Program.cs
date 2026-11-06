using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Console;
using QnxPortScan.Core.Reporting;
using QnxPortScan.Core.Rules;
using QnxPortScan.Core.Scanning;

namespace QnxPortScan.Cli;

public static class Program
{
    public const int ExitOk = 0;
    public const int ExitFindings = 1;
    public const int ExitUsage = 2;
    public const int ExitIoError = 3;

    private const string Usage = """
        qnx-portscan — find Linux-only APIs in C/C++ code and suggest QNX Neutrino replacements.

        Usage:
          qnx-portscan scan <path> [options]
          qnx-portscan rules [--rules <file>]

        Options:
          --format md|sarif|both   Output format (default: md)
          --out <path>             Output file (for 'both': base path, .md and .sarif are appended)
          --rules <file>           Use a custom rules JSON instead of the built-in catalog
          --exclude <prefix>       Skip a relative path prefix (repeatable), e.g. third_party/
          --include-guarded        Also report code inside Linux-only #ifdef branches (as notes)
          --fail-on error|warning|none   Exit 1 if findings at or above this level (default: none)
          -v, --verbose            Log progress to stderr

        Exit codes: 0 ok, 1 findings at --fail-on level, 2 usage error, 3 I/O or rules file error.
        """;

    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        try
        {
            return await RunAsync(args, Console.Out, Console.Error, cts.Token);
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("cancelled");
            return 130;
        }
    }

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            await stderr.WriteLineAsync(Usage);
            return ExitUsage;
        }
        if (args[0] is "-h" or "--help" or "help")
        {
            await stdout.WriteLineAsync(Usage);
            return ExitOk;
        }

        CliOptions opts;
        try
        {
            opts = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            await stderr.WriteLineAsync($"error: {ex.Message}");
            await stderr.WriteLineAsync("Run 'qnx-portscan --help' for usage.");
            return ExitUsage;
        }

        try
        {
            return await ExecuteAsync(opts, stdout, stderr, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                       or InvalidOperationException or ArgumentException)
        {
            // DirectoryNotFoundException/FileNotFoundException are IOExceptions.
            await stderr.WriteLineAsync($"error: {ex.Message}");
            return ExitIoError;
        }
    }

    private static async Task<int> ExecuteAsync(CliOptions opts, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        var catalog = opts.RulesFile is null
            ? RuleCatalog.LoadBuiltIn()
            : await RuleCatalog.FromFileAsync(opts.RulesFile, ct);

        if (opts.Command == "rules")
        {
            foreach (var r in catalog.Rules)
            {
                await stdout.WriteLineAsync($"{r.Id,-8} {r.Severity,-8} {r.Kind,-10} {r.Title}");
            }
            return ExitOk;
        }

        using var loggerFactory = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(opts.Verbose ? LogLevel.Information : LogLevel.Warning);
            b.AddSimpleConsole(o => o.SingleLine = true);
            b.Services.Configure<ConsoleLoggerOptions>(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        });
        var scanner = new Scanner(catalog, loggerFactory.CreateLogger<Scanner>());

        ScanResult result;
        try
        {
            result = await scanner.ScanDirectoryAsync(opts.Path!, new ScanOptions
            {
                IncludeGuarded = opts.IncludeGuarded,
                ExcludePaths = opts.Excludes,
            }, ct);
        }
        catch (DirectoryNotFoundException)
        {
            await stderr.WriteLineAsync($"error: path not found: {opts.Path}");
            return ExitUsage;
        }
        catch (RegexMatchTimeoutException ex)
        {
            await stderr.WriteLineAsync($"error: rule pattern timed out: {ex.Pattern}");
            return ExitIoError;
        }

        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? "0.0.0";
        var title = $"QNX portability report: {Path.GetFileName(result.Root.TrimEnd('/'))}";

        var summary = $"({result.Findings.Count} findings in {result.FilesScanned} files)";
        switch (opts.Format)
        {
            case "md":
                await Emit(MarkdownReporter.Render(result, title), opts.Out, summary, stdout, ct);
                break;
            case "sarif":
                await Emit(SarifReporter.Render(result, catalog, version), opts.Out, summary, stdout, ct);
                break;
            case "both":
                var basePath = opts.Out ?? "qnx-portscan";
                await File.WriteAllTextAsync(basePath + ".md", MarkdownReporter.Render(result, title), ct);
                await File.WriteAllTextAsync(basePath + ".sarif", SarifReporter.Render(result, catalog, version), ct);
                await stdout.WriteLineAsync($"Wrote {basePath}.md and {basePath}.sarif {summary}");
                break;
        }

        var threshold = opts.FailOn switch
        {
            "error" => Severity.Error,
            "warning" => Severity.Warning,
            _ => (Severity?)null,
        };
        return threshold is { } t && result.Findings.Any(f => f.Severity >= t) ? ExitFindings : ExitOk;
    }

    private static async Task Emit(string content, string? outPath, string summary, TextWriter stdout, CancellationToken ct)
    {
        if (outPath is null)
        {
            await stdout.WriteAsync(content);
            return;
        }
        await File.WriteAllTextAsync(outPath, content, ct);
        await stdout.WriteLineAsync($"Wrote {outPath} {summary}");
    }
}
