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

    [GeneratedRegex(@"\b(__linux__|__linux|linux|__gnu_linux__|__ANDROID__)\b")]
    private static partial Regex LinuxMacroRegex();

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

    private static Polarity ClassifyDefined(string rest, bool negate)
    {
        var name = rest.Trim().Split(' ', '\t').FirstOrDefault() ?? string.Empty;
        if (LinuxMacroRegex().IsMatch(name)) return negate ? Polarity.NotLinux : Polarity.Linux;
        return Polarity.None;
    }

    private static Polarity ClassifyExpression(string expr)
    {
        var e = expr.Trim();
        if (e == "0") return Polarity.Zero;
        var hasLinux = LinuxMacroRegex().IsMatch(e);
        if (!hasLinux) return Polarity.None;
        // Mixed conditions like "defined(__linux__) || defined(__QNX__)" are not Linux-only.
        if (QnxMacroRegex().IsMatch(e) || e.Contains("||", StringComparison.Ordinal)) return Polarity.None;
        var negated = Regex.IsMatch(e, @"!\s*(defined\s*\(?\s*)?(__linux__|__linux|linux|__gnu_linux__|__ANDROID__)");
        return negated ? Polarity.NotLinux : Polarity.Linux;
    }

    private static string GetCodeLine(CppSourceView view, int line)
    {
        var start = view.GetLineStart(line);
        var end = line < view.LineCount ? view.GetLineStart(line + 1) : view.Code.Length;
        return view.Code[start..end].TrimEnd('\r', '\n');
    }
}
