namespace QnxPortScan.Cli;

public sealed class CliOptions
{
    public required string Command { get; init; }
    public string? Path { get; private set; }
    public string Format { get; private set; } = "md";
    public string? Out { get; private set; }
    public string? RulesFile { get; private set; }
    public List<string> Excludes { get; } = [];
    public bool IncludeGuarded { get; private set; }
    public string FailOn { get; private set; } = "none";
    public bool Verbose { get; private set; }

    public static CliOptions Parse(string[] args)
    {
        var cmd = args[0];
        if (cmd is not ("scan" or "rules")) throw new ArgumentException($"unknown command '{cmd}'");
        var o = new CliOptions { Command = cmd };
        for (var i = 1; i < args.Length; i++)
        {
            var a = args[i];
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{a} needs a value");
            switch (a)
            {
                case "--format":
                    o.Format = Value();
                    if (o.Format is not ("md" or "sarif" or "both")) throw new ArgumentException($"bad --format '{o.Format}'");
                    break;
                case "--out": o.Out = Value(); break;
                case "--rules": o.RulesFile = Value(); break;
                case "--exclude": o.Excludes.Add(Value()); break;
                case "--include-guarded": o.IncludeGuarded = true; break;
                case "--fail-on":
                    o.FailOn = Value();
                    if (o.FailOn is not ("error" or "warning" or "none")) throw new ArgumentException($"bad --fail-on '{o.FailOn}'");
                    break;
                case "-v" or "--verbose": o.Verbose = true; break;
                default:
                    if (a.StartsWith('-')) throw new ArgumentException($"unknown option '{a}'");
                    if (cmd == "rules") throw new ArgumentException($"unexpected argument '{a}'");
                    if (o.Path is not null) throw new ArgumentException("only one path is allowed");
                    o.Path = a;
                    break;
            }
        }
        if (cmd == "scan" && o.Path is null) throw new ArgumentException("scan needs a <path>");
        return o;
    }
}
