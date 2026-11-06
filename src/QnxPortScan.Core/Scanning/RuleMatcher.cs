using System.Text.RegularExpressions;
using QnxPortScan.Core.Lexing;
using QnxPortScan.Core.Rules;

namespace QnxPortScan.Core.Scanning;

/// <summary>Compiles rules to regexes and matches them against a parsed source view.</summary>
public sealed class RuleMatcher
{
    /// <summary>
    /// Upper bound (chars) when searching for the ')' closing a function signature. Bounds the cost of
    /// pathological input (e.g. an unbalanced '(' in a huge file) while covering any realistic declaration.
    /// </summary>
    private const int MaxSignatureScan = 8000;

    private static readonly Regex IncludeRegex =
        new(@"^[ \t]*#[ \t]*include[ \t]*[<""](?<h>[^>""]+)[>""]", RegexOptions.Multiline | RegexOptions.Compiled);

    // Includes backslash-continued physical lines of the directive.
    private static readonly Regex ConditionRegex =
        new(@"^[ \t]*#[ \t]*(if|ifdef|ifndef|elif|elifdef|elifndef|undef)\b(?:[^\n]*\\\r?\n)*[^\n]*", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    private static readonly HashSet<string> CallKeywords = new(StringComparer.Ordinal)
    {
        "return", "case", "else", "do", "sizeof", "throw", "new", "delete", "co_return", "co_await", "co_yield", "goto",
    };

    private static readonly HashSet<string> DeclaratorSuffixKeywords = new(StringComparer.Ordinal)
    {
        "const", "noexcept", "override", "final", "volatile", "throw",
    };

    private readonly List<(Rule Rule, Regex Regex)> _codeRules = [];
    private readonly List<(Rule Rule, Regex Regex)> _literalRules = [];
    private readonly List<(Rule Rule, string[] Patterns)> _includeRules = [];

    public RuleMatcher(RuleCatalog catalog)
    {
        foreach (var rule in catalog.Rules)
        {
            switch (rule.Kind)
            {
                case RuleKind.Include:
                    _includeRules.Add((rule, rule.Patterns.ToArray()));
                    break;
                case RuleKind.Call:
                    _codeRules.Add((rule, new Regex($@"(?<![\w.>]|\w::)\b(?<m>{Alternation(rule)})\s*\(", RegexOptions.Compiled, MatchTimeout)));
                    break;
                case RuleKind.Identifier:
                    _codeRules.Add((rule, new Regex($@"\b(?<m>{Alternation(rule)})\b", RegexOptions.Compiled, MatchTimeout)));
                    break;
                case RuleKind.Literal:
                    _literalRules.Add((rule, new Regex($"(?<m>{string.Join('|', rule.Patterns)})", RegexOptions.Compiled, MatchTimeout)));
                    break;
            }
        }
    }

    private static string Alternation(Rule rule) => string.Join('|', rule.Patterns.Select(Regex.Escape));

    public IEnumerable<MatchHit> Match(CppSourceView view)
    {
        foreach (Match m in IncludeRegex.Matches(view.Code))
        {
            // Header names in quotes are blanked in Code; read them from the original text.
            var lineStart = m.Index;
            var origMatch = IncludeRegex.Match(view.Original, lineStart);
            if (!origMatch.Success || origMatch.Index != lineStart) continue;
            var header = origMatch.Groups["h"].Value.Trim();
            foreach (var (rule, patterns) in _includeRules)
            {
                if (patterns.Any(p => p.EndsWith('/') ? header.StartsWith(p, StringComparison.Ordinal) : header == p))
                {
                    yield return new MatchHit(rule, origMatch.Groups["h"].Index, header, HitKind.Use);
                }
            }
        }

        // Include lines with quoted headers were blanked; angle-bracket headers survive in Code
        // and would otherwise also hit identifier rules (e.g. <sys/epoll.h>), so skip include lines.
        var codeNoIncludes = IncludeRegex.Replace(view.Code, m => new string(' ', m.Length));
        // Feature tests like "#if defined(SOCK_CLOEXEC)" are portable by construction.
        var codeNoConditions = ConditionRegex.Replace(codeNoIncludes, m => new string(' ', m.Length));
        foreach (var (rule, regex) in _codeRules)
        {
            var target = rule.Kind == RuleKind.Identifier ? codeNoConditions : codeNoIncludes;
            foreach (Match m in regex.Matches(target))
            {
                var g = m.Groups["m"];
                var kind = rule.Kind == RuleKind.Call ? ClassifyCall(target, g.Index, m.Index + m.Length - 1) : HitKind.Use;
                yield return new MatchHit(rule, g.Index, g.Value, kind);
            }
        }

        foreach (var (rule, regex) in _literalRules)
        {
            foreach (var (start, end) in view.LiteralSpans)
            {
                var literal = view.Original[start..end];
                foreach (Match m in regex.Matches(literal))
                {
                    var g = m.Groups["m"];
                    yield return new MatchHit(rule, start + g.Index, g.Value, HitKind.Use);
                }
            }
        }
    }

    /// <summary>
    /// Tells a call from a declaration/definition of a function with the same name
    /// ("int splice(int);", "static char *itoa(int v) {").
    /// </summary>
    internal static HitKind ClassifyCall(string code, int nameStart, int openParen)
    {
        var p = nameStart - 1;
        while (p >= 0 && code[p] is ' ' or '\t') p--;
        if (p < 0) return HitKind.Use;
        var prev = code[p];
        var looksDeclared = false;
        if (char.IsLetterOrDigit(prev) || prev == '_')
        {
            var end = p + 1;
            while (p >= 0 && (char.IsLetterOrDigit(code[p]) || code[p] == '_')) p--;
            looksDeclared = !CallKeywords.Contains(code[(p + 1)..end]);
        }
        else if (prev is '*' or '&')
        {
            // "char *itoa(" at statement start: only identifiers, '*', '&' and spaces since the line start.
            var lineStart = code.LastIndexOf('\n', p) + 1;
            var head = code[lineStart..(p + 1)];
            looksDeclared = head.Trim().Length > 1 && head.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or ' ' or '\t' or '*' or '&' or ':' or '<' or '>' or ',');
        }
        if (!looksDeclared) return HitKind.Use;

        // Find the matching ')' and look at what follows: '{' means a definition.
        var depth = 0;
        var i = openParen;
        for (; i < code.Length && i < openParen + MaxSignatureScan; i++)
        {
            if (code[i] == '(') depth++;
            else if (code[i] == ')' && --depth == 0) break;
        }
        if (depth != 0) return HitKind.Declaration;
        i++;
        while (true)
        {
            while (i < code.Length && char.IsWhiteSpace(code[i])) i++;
            var wordStart = i;
            while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i++;
            if (i == wordStart) break;
            if (!DeclaratorSuffixKeywords.Contains(code[wordStart..i])) break;
        }
        return i < code.Length && code[i] == '{' ? HitKind.Definition : HitKind.Declaration;
    }
}

public enum HitKind
{
    Use,
    /// <summary>Prototype of a function that shares the rule's name.</summary>
    Declaration,
    /// <summary>The project defines its own function with this name.</summary>
    Definition,
}

public readonly record struct MatchHit(Rule Rule, int Offset, string Match, HitKind Kind);
