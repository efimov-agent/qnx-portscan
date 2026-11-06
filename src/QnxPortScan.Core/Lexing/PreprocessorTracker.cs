using System.Text.RegularExpressions;

namespace QnxPortScan.Core.Lexing;

/// <summary>
/// Tracks, per line, whether code sits inside a Linux-only preprocessor branch
/// (e.g. <c>#ifdef __linux__</c> or the <c>#else</c> of <c>#ifndef __linux__</c>), or inside <c>#if 0</c>.
/// Heuristic: conditions are classified by text, not evaluated.
/// </summary>
public static partial class PreprocessorTracker
{
    public enum LineContext { Normal, LinuxGuarded, Disabled }

    private enum Polarity { None, Linux, NotLinux, Zero }

    private sealed class Frame
    {
        public Polarity Current;
        public bool LinuxSeen; // some earlier branch of this chain was Linux-positive
    }

    [GeneratedRegex(@"^\s*#\s*(?<dir>ifdef|ifndef|if|elif|elifdef|elifndef|else|endif)\b(?<rest>.*)$")]
    private static partial Regex DirectiveRegex();

    [GeneratedRegex(@"\b(__QNX__|__QNXNTO__)\b")]
    private static partial Regex QnxMacroRegex();

    public static LineContext[] Analyze(CppSourceView view)
    {
        var result = new LineContext[view.LineCount + 1];
        var stack = new Stack<Frame>();
        for (var line = 1; line <= view.LineCount; line++)
        {
            var codeLine = GetCodeLine(view, line);
            var m = DirectiveRegex().Match(codeLine);
            if (m.Success)
            {
                var dir = m.Groups["dir"].Value;
                var rest = m.Groups["rest"].Value;
                switch (dir)
                {
                    case "ifdef":
                        stack.Push(new Frame { Current = ClassifyDefined(rest, negate: false) });
                        break;
                    case "ifndef":
                        stack.Push(new Frame { Current = ClassifyDefined(rest, negate: true) });
                        break;
                    case "if":
                        stack.Push(new Frame { Current = ClassifyExpression(rest) });
                        break;
                    case "elif" or "elifdef" or "elifndef":
                        if (stack.TryPeek(out var f1))
                        {
                            f1.LinuxSeen |= f1.Current == Polarity.Linux;
                            f1.Current = dir switch
                            {
                                "elifdef" => ClassifyDefined(rest, false),
                                "elifndef" => ClassifyDefined(rest, true),
                                _ => ClassifyExpression(rest),
                            };
                        }
                        break;
                    case "else":
                        if (stack.TryPeek(out var f2))
                        {
                            f2.Current = f2.Current switch
                            {
                                Polarity.NotLinux => Polarity.Linux,
                                Polarity.Zero => Polarity.None,
                                _ => Polarity.None,
                            };
                        }
                        break;
                    case "endif":
                        if (stack.Count > 0) stack.Pop();
                        break;
                }
                result[line] = Current(stack);
                continue;
            }
            result[line] = Current(stack);
        }
        return result;
    }

    private static LineContext Current(Stack<Frame> stack)
    {
        var ctx = LineContext.Normal;
        foreach (var f in stack)
        {
            if (f.Current == Polarity.Zero) return LineContext.Disabled;
            if (f.Current == Polarity.Linux) ctx = LineContext.LinuxGuarded;
        }
        return ctx;
    }

    private static readonly HashSet<string> ForeignPlatformMacros = new(StringComparer.Ordinal)
    {
        "__linux__", "__linux", "linux", "__gnu_linux__", "__ANDROID__", "__GLIBC__",
        "__APPLE__", "__MACH__", "__FreeBSD__", "__NetBSD__", "__OpenBSD__", "__DragonFly__",
        "_WIN32", "_WIN64", "WIN32", "_MSC_VER", "__CYGWIN__", "__MINGW32__", "__MINGW64__",
        "__sun", "__sun__", "sun", "_AIX", "__MVS__", "__HAIKU__", "__GNU__", "__EMSCRIPTEN__",
    };

    [GeneratedRegex(@"^\(*\s*(?<neg>!\s*)?(defined\s*\(?\s*)?(?<m>[A-Za-z_]\w*)\s*\)?\s*\)*$")]
    private static partial Regex TermRegex();

    private static Polarity ClassifyDefined(string rest, bool negate)
    {
        var name = rest.Trim().Split(' ', '\t').FirstOrDefault() ?? string.Empty;
        if (ForeignPlatformMacros.Contains(name)) return negate ? Polarity.NotLinux : Polarity.Linux;
        return Polarity.None;
    }

    /// <summary>
    /// Classifies an #if expression. A conjunct (split on &amp;&amp;) that is a disjunction of
    /// positive non-QNX platform macros makes the branch foreign-only. A lone negated platform
    /// macro makes the #else branch foreign-only. Everything else is treated as neutral.
    /// </summary>
    private static Polarity ClassifyExpression(string expr)
    {
        var e = expr.Trim();
        if (e == "0") return Polarity.Zero;
        if (QnxMacroRegex().IsMatch(e)) return Polarity.None;
        var conjuncts = e.Split("&&");
        foreach (var c in conjuncts)
        {
            var terms = Unwrap(c).Split("||");
            var all = terms.Select(t => TermRegex().Match(t.Trim())).ToList();
            if (all.All(m => m.Success && m.Groups["neg"].Length == 0 && ForeignPlatformMacros.Contains(m.Groups["m"].Value)))
                return Polarity.Linux;
        }
        if (conjuncts.Length == 1)
        {
            var m = TermRegex().Match(e);
            if (m.Success && m.Groups["neg"].Length > 0 && ForeignPlatformMacros.Contains(m.Groups["m"].Value))
                return Polarity.NotLinux;
        }
        return Polarity.None;
    }

    private static string Unwrap(string s)
    {
        s = s.Trim();
        while (s.Length >= 2 && s[0] == '(' && s[^1] == ')' && Balanced(s[1..^1])) s = s[1..^1].Trim();
        return s;
    }

    private static bool Balanced(string s)
    {
        var depth = 0;
        foreach (var ch in s)
        {
            if (ch == '(') depth++;
            else if (ch == ')' && --depth < 0) return false;
        }
        return depth == 0;
    }

    private static string GetCodeLine(CppSourceView view, int line)
    {
        var start = view.GetLineStart(line);
        var end = line < view.LineCount ? view.GetLineStart(line + 1) : view.Code.Length;
        return view.Code[start..end].TrimEnd('\r', '\n');
    }
}
