using System.Text.Json.Serialization;

namespace QnxPortScan.Core.Rules;

[JsonConverter(typeof(JsonStringEnumConverter<RuleKind>))]
public enum RuleKind
{
    /// <summary>Header include; a pattern ending with '/' matches a directory prefix.</summary>
    Include,
    /// <summary>Function call: identifier followed by '('.</summary>
    Call,
    /// <summary>Any use of an identifier (macro, constant, type).</summary>
    Identifier,
    /// <summary>Regex applied to string literal contents (e.g. "/proc/self/maps").</summary>
    Literal,
}

[JsonConverter(typeof(JsonStringEnumConverter<Severity>))]
public enum Severity
{
    Note,
    Warning,
    Error,
}

public sealed record Rule
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required RuleKind Kind { get; init; }
    public required IReadOnlyList<string> Patterns { get; init; }
    public Severity Severity { get; init; } = Severity.Warning;
    public required string Message { get; init; }
    public required string Suggestion { get; init; }
    public IReadOnlyList<string> Docs { get; init; } = [];
    public string Category { get; init; } = "api";
}
