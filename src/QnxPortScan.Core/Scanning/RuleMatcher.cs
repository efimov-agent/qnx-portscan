using System.Text.RegularExpressions;
using QnxPortScan.Core.Lexing;
using QnxPortScan.Core.Rules;

namespace QnxPortScan.Core.Scanning;

/// <summary>Compiles rules to regexes and matches them against a parsed source view.</summary>
public sealed class RuleMatcher
{
    private static readonly Regex IncludeRegex =
        new(@"^[ \t]*#[ \t]*include[ \t]*[<""](?<h>[^>""]+)[>""]", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ConditionRegex =
        new(@"^[ \t]*#[ \t]*(if|ifdef|ifndef|elif|elifdef|elifndef|undef)\b[^\n]*", RegexOptions.Multiline | RegexOptions.Compiled);

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
                    _codeRules.Add((rule, new Regex($@"(?<![\w.>]|->)\b(?<m>{Alternation(rule)})\s*\(", RegexOptions.Compiled)));
                    break;
                case RuleKind.Identifier:
                    _codeRules.Add((rule, new Regex($@"\b(?<m>{Alternation(rule)})\b", RegexOptions.Compiled)));
                    break;
                case RuleKind.Literal:
                    _literalRules.Add((rule, new Regex($"(?<m>{string.Join('|', rule.Patterns)})", RegexOptions.Compiled)));
                    break;
            }
        }
    }

    private static string Alternation(Rule rule) => string.Join('|', rule.Patterns.Select(Regex.Escape));

    public IEnumerable<(Rule Rule, int Offset, string Match)> Match(CppSourceView view)
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
                    yield return (rule, origMatch.Groups["h"].Index, header);
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
                yield return (rule, g.Index, g.Value);
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
                    yield return (rule, start + g.Index, g.Value);
                }
            }
        }
    }
}
