using QnxPortScan.Core.Lexing;

namespace QnxPortScan.Tests;

public class LexerTests
{
    [Fact]
    public void Comments_are_blanked_and_offsets_preserved()
    {
        const string src = "int a; // epoll_wait(x)\n/* epoll_wait(\n y) */ int b;";
        var v = CppSourceView.Parse(src);
        Assert.Equal(src.Length, v.Code.Length);
        Assert.DoesNotContain("epoll", v.Code);
        Assert.Contains("int b;", v.Code);
        Assert.Equal(src.Count(c => c == '\n'), v.Code.Count(c => c == '\n'));
    }

    [Fact]
    public void String_contents_move_to_literal_view()
    {
        var v = CppSourceView.Parse("puts(\"epoll_wait(\\\"q\\\")\"); f();");
        Assert.DoesNotContain("epoll_wait", v.Code);
        Assert.Contains("epoll_wait", v.Literals);
        Assert.Contains("f();", v.Code);
    }

    [Fact]
    public void Raw_strings_are_handled()
    {
        var v = CppSourceView.Parse("auto s = R\"x(epoll_wait(\" )\" )x\"; g();");
        Assert.DoesNotContain("epoll_wait", v.Code);
        Assert.Contains("g();", v.Code);
    }

    [Fact]
    public void Digit_separators_are_not_char_literals()
    {
        var v = CppSourceView.Parse("int n = 1'000'000; epoll_wait(1);");
        Assert.Contains("epoll_wait(1)", v.Code);
    }

    [Fact]
    public void Char_literal_with_quote_does_not_swallow_code()
    {
        var v = CppSourceView.Parse("char c = '\\''; char d = '\"'; epoll_wait(2);");
        Assert.Contains("epoll_wait(2)", v.Code);
    }

    [Fact]
    public void Position_is_one_based()
    {
        var v = CppSourceView.Parse("a\nbc\nd");
        Assert.Equal((2, 2), v.GetPosition(3));
        Assert.Equal("bc", v.GetLineText(2));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Line_comment_continuation_handles_lf_and_crlf(string eol)
    {
        var src = "int a; // note \\" + eol + "epoll_wait(x);" + eol + "int b;";
        var v = CppSourceView.Parse(src);
        Assert.DoesNotContain("epoll_wait", v.Code);
        Assert.Contains("int b;", v.Code);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void String_continuation_handles_lf_and_crlf(string eol)
    {
        var src = "puts(\"a \\" + eol + "epoll_wait(\"); int b;";
        var v = CppSourceView.Parse(src);
        Assert.DoesNotContain("epoll_wait", v.Code);
        Assert.Contains("epoll_wait", v.Literals);
        Assert.Contains("int b;", v.Code);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Positions_and_line_text_survive_continuations(string eol)
    {
        var src = "int a; // c \\" + eol + "more" + eol + "epoll_wait(x);";
        var v = CppSourceView.Parse(src);
        Assert.Equal(3, v.LineCount);
        var offset = src.IndexOf("epoll_wait", StringComparison.Ordinal);
        Assert.Equal((3, 1), v.GetPosition(offset));
        Assert.Equal("more", v.GetLineText(2));
        Assert.Equal("epoll_wait(x);", v.GetLineText(3));
        Assert.Equal(src.Length, v.Code.Length);
    }

    [Theory]
    [InlineData("int a; // c \\")]
    [InlineData("int a; // c \\\r")]
    [InlineData("puts(\"abc \\")]
    [InlineData("puts(\"abc \\\r")]
    public void Backslash_at_end_of_file_does_not_throw(string src)
    {
        var v = CppSourceView.Parse(src);
        Assert.Equal(src.Length, v.Code.Length);
        Assert.Equal(1, v.LineCount);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Double_backslash_before_newline_continues_comment_but_not_closed_string(string eol)
    {
        var src = "int a; // c \\\\" + eol + "epoll_wait(x);";
        var v = CppSourceView.Parse(src);
        // In a // comment every backslash right before a newline continues it, as in the C preprocessor.
        Assert.DoesNotContain("epoll_wait", v.Code);

        var str = "puts(\"a\\\\\"); " + eol + "epoll_wait(x);";
        var sv = CppSourceView.Parse(str);
        Assert.Contains("epoll_wait(x);", sv.Code);
    }
}
