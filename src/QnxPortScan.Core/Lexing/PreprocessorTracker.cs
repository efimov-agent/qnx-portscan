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
                // Join backslash-continued physical lines into one logical directive.
                var last = line;
                while (rest.TrimEnd().EndsWith('\\') && last < view.LineCount)
                {
                    rest = rest.TrimEnd()[..^1] + " " + GetCodeLine(view, ++last);
                }
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
                for (var l = line; l <= last; l++) result[l] = Current(stack);
                line = last;
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

    // Facility names: any macro mentioning them (HAVE_EPOLL, UV__KQUEUE_EVFILT_USER) is about a non-QNX facility.
    private static readonly HashSet<string> ForeignFacilityTokens = new(StringComparer.Ordinal)
    {
        "EPOLL", "KQUEUE", "EVENTFD", "SIGNALFD", "TIMERFD", "SENDFILE", "IOURING", "URING", "PRCTL",
    };

    // Platform names count only as the last segment after a feature prefix (ZMQ_HAVE_LINUX, USE_WINDOWS),
    // so CONFIG_HAS_WINDOWS_COUNT is not treated as a platform guard.
    private static readonly HashSet<string> ForeignPlatformTokens = new(StringComparer.Ordinal)
    {
        "LINUX", "DARWIN", "APPLE", "MACOS", "OSX", "FREEBSD", "NETBSD", "OPENBSD", "WIN32", "WIN64", "WINDOWS", "GLIBC",
    };

    private static readonly HashSet<string> FeaturePrefixes = new(StringComparer.Ordinal)
    {
        "HAVE", "USE", "ENABLE", "WITH", "HAS", "IS", "ON", "OS", "PLATFORM", "TARGET", "BUILD",
    };

    /// <summary>
    /// True for platform macros (__linux__, _WIN32, ...) and for feature macros named after a
    /// non-QNX facility (HAVE_EPOLL, UV__KQUEUE_EVFILT_USER, USE_LINUX_AIO): a QNX build will not define them.
    /// </summary>
    private static bool IsForeign(string macro)
    {
        if (ForeignPlatformMacros.Contains(macro)) return true;
        var parts = macro.ToUpperInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(ForeignFacilityTokens.Contains)) return true;
        return parts.Length >= 2 && ForeignPlatformTokens.Contains(parts[^1]) && FeaturePrefixes.Contains(parts[^2]);
    }

    [GeneratedRegex(@"^\(*\s*(?<neg>!\s*)?(defined\s*\(?\s*)?(?<m>[A-Za-z_]\w*)\s*\)?\s*\)*$")]
    private static partial Regex TermRegex();

    private static Polarity ClassifyDefined(string rest, bool negate)
    {
        var name = rest.Trim().Split(' ', '\t').FirstOrDefault() ?? string.Empty;
        if (IsForeign(name)) return negate ? Polarity.NotLinux : Polarity.Linux;
        return Polarity.None;
    }

    /// <summary>
    /// Classifies an #if expression. A conjunct (split on &amp;&amp;) that is a disjunction of
    /// positive non-QNX platform macros makes the branch foreign-only. A lone negated platform
    /// macro makes the #else branch foreign-only. Everything else is treated as neutral.
    /// </summary>
    private static Polarity ClassifyExpression(string expr)
    {
        var e = Unwrap(expr);
        if (e == "0" || e.Split("&&").Any(c => Unwrap(c) == "0")) return Polarity.Zero;
        if (QnxMacroRegex().IsMatch(e)) return Polarity.None;
        var conjuncts = e.Split("&&");
        foreach (var c in conjuncts)
        {
            var terms = Unwrap(c).Split("||");
            var all = terms.Select(t => TermRegex().Match(t.Trim())).ToList();
            if (all.All(m => m.Success && m.Groups["neg"].Length == 0 && IsForeign(m.Groups["m"].Value)))
                return Polarity.Linux;
        }
        if (conjuncts.Length == 1)
        {
            var m = TermRegex().Match(e);
            if (m.Success && m.Groups["neg"].Length > 0 && IsForeign(m.Groups["m"].Value))
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
