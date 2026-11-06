namespace QnxPortScan.Core.Lexing;

/// <summary>
/// Two same-length projections of a C/C++ source file: <see cref="Code"/> has comments and literal
/// contents blanked out, <see cref="Literals"/> keeps only string literal contents.
/// Newlines are preserved in both so offsets map 1:1 to the original text.
/// </summary>
public sealed class CppSourceView
{
    private readonly int[] _lineStarts;

    private CppSourceView(string original, string code, string literals, IReadOnlyList<(int Start, int End)> literalSpans)
    {
        LiteralSpans = literalSpans;
        Original = original;
        Code = code;
        Literals = literals;
        var starts = new List<int> { 0 };
        for (var i = 0; i < original.Length; i++)
        {
            if (original[i] == '\n') starts.Add(i + 1);
        }
        _lineStarts = starts.ToArray();
    }

    public string Original { get; }
    public string Code { get; }
    public string Literals { get; }
    public int LineCount => _lineStarts.Length;

    /// <summary>Content ranges [Start, End) of string/char literals, without quotes.</summary>
    public IReadOnlyList<(int Start, int End)> LiteralSpans { get; }

    /// <summary>Returns 1-based line and column for an offset.</summary>
    public (int Line, int Column) GetPosition(int offset)
    {
        var idx = Array.BinarySearch(_lineStarts, offset);
        if (idx < 0) idx = ~idx - 1;
        return (idx + 1, offset - _lineStarts[idx] + 1);
    }

    public string GetLineText(int line)
    {
        var start = _lineStarts[line - 1];
        var end = line < _lineStarts.Length ? _lineStarts[line] : Original.Length;
        return Original[start..end].TrimEnd('\r', '\n');
    }

    public int GetLineStart(int line) => _lineStarts[line - 1];

    public static CppSourceView Parse(string text)
    {
        var code = text.ToCharArray();
        var lits = new char[text.Length];
        for (var i = 0; i < text.Length; i++) lits[i] = text[i] == '\n' ? '\n' : ' ';

        var spans = new List<(int, int)>();
        var i0 = 0;
        var n = text.Length;
        var numberRun = false; // inside an identifier/number run that started with a digit
        var inRun = false;
        while (i0 < n)
        {
            var c = text[i0];
            var next = i0 + 1 < n ? text[i0 + 1] : '\0';

            if (c == '/' && next == '/')
            {
                var end = i0;
                while (end < n && text[end] != '\n')
                {
                    // Backslash-newline (LF or CRLF) continues a // comment.
                    if (text[end] == '\\')
                    {
                        var after = end + 1 < n && text[end + 1] == '\r' ? end + 2 : end + 1;
                        if (after < n && text[after] == '\n') end = after;
                    }
                    end++;
                }
                Blank(code, i0, end);
                i0 = end;
                inRun = false;
                continue;
            }

            if (c == '/' && next == '*')
            {
                var close = text.IndexOf("*/", i0 + 2, StringComparison.Ordinal);
                var end = close < 0 ? n : close + 2;
                Blank(code, i0, end);
                i0 = end;
                inRun = false;
                continue;
            }

            if (c == '"' && IsRawStringPrefix(text, i0, out var delimiter)
                && text.IndexOf(")" + delimiter + "\"", i0 + 1 + delimiter.Length + 1, StringComparison.Ordinal) is var rawClose and >= 0)
            {
                // R"delim( ... )delim". Unterminated or misdetected raw strings fall back to normal lexing
                // instead of swallowing the rest of the file.
                var contentStart = i0 + 1 + delimiter.Length + 1;
                var terminator = ")" + delimiter + "\"";
                var contentEnd = rawClose;
                var end = rawClose + terminator.Length;
                CopyLiteral(text, code, lits, contentStart, contentEnd);
                spans.Add((contentStart, contentEnd));
                i0 = end;
                inRun = false;
                continue;
            }

            if (c == '"' || (c == '\'' && !(inRun && numberRun)))
            {
                var end = SkipQuoted(text, i0, c);
                var litEnd = end > i0 + 1 && text[end - 1] == c ? end - 1 : end;
                litEnd = Math.Max(i0 + 1, litEnd);
                CopyLiteral(text, code, lits, i0 + 1, litEnd);
                if (c == '"') spans.Add((i0 + 1, litEnd));
                i0 = end;
                inRun = false;
                continue;
            }

            if (char.IsLetterOrDigit(c) || c == '_' || (c == '\'' && inRun && numberRun))
            {
                if (!inRun)
                {
                    inRun = true;
                    numberRun = char.IsDigit(c);
                }
            }
            else
            {
                inRun = false;
            }

            i0++;
        }

        return new CppSourceView(text, new string(code), new string(lits), spans);
    }

    private static bool IsRawStringPrefix(string text, int quote, out string delimiter)
    {
        delimiter = string.Empty;
        if (quote == 0 || text[quote - 1] != 'R') return false;
        // Prefix must be R, LR, uR, UR or u8R and not part of a longer identifier.
        var p = quote - 2;
        if (p >= 0 && text[p] == '8' && p - 1 >= 0 && text[p - 1] == 'u') p -= 2;
        else if (p >= 0 && (text[p] == 'L' || text[p] == 'u' || text[p] == 'U')) p--;
        if (p >= 0 && (char.IsLetterOrDigit(text[p]) || text[p] == '_')) return false;

        var open = text.IndexOf('(', quote + 1);
        if (open < 0 || open - quote - 1 > 16) return false;
        var d = text.Substring(quote + 1, open - quote - 1);
        if (d.Any(ch => char.IsWhiteSpace(ch) || ch == '\\' || ch == ')' || ch == '"')) return false;
        delimiter = d;
        return true;
    }

    private static int SkipQuoted(string text, int start, char quote)
    {
        var i = start + 1;
        while (i < text.Length)
        {
            var ch = text[i];
            if (ch == '\\')
            {
                // Skip the escaped char; treat backslash-CRLF as a single line continuation.
                i += i + 2 < text.Length && text[i + 1] == '\r' && text[i + 2] == '\n' ? 3 : 2;
                continue;
            }
            if (ch == quote) return i + 1;
            if (ch == '\n') return i; // unterminated literal: stop at end of line
            i++;
        }
        return text.Length;
    }

    private static void Blank(char[] code, int start, int end)
    {
        for (var i = start; i < end && i < code.Length; i++)
        {
            if (code[i] != '\n') code[i] = ' ';
        }
    }

    private static void CopyLiteral(string text, char[] code, char[] lits, int start, int end)
    {
        for (var i = start; i < end && i < text.Length; i++)
        {
            if (text[i] == '\n') continue;
            lits[i] = text[i];
            code[i] = ' ';
        }
    }
}
