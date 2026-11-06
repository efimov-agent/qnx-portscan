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
}
