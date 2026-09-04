namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#concept-cd-substring">DOM §4.10's substring data</a> and
/// <a href="https://dom.spec.whatwg.org/#concept-cd-replace">replace data</a>: the WebIDL
/// <c>unsigned long</c> conversion happens first, and only then are the offset and the count tested against
/// the length.
/// </summary>
/// <remarks>
/// <c>-1</c> is not a small negative number to a DOM member; it is 4 294 967 295, which is past the end of
/// any string. Converted as a signed integer it is past neither test, and what a page saw was an
/// <c>ArgumentOutOfRangeException</c> from <c>String.Substring</c> crossing as a <c>TypeError</c>.
/// </remarks>
public sealed class CharacterDataOffsetTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="a">hello</div></body></html>
        """;

    /// <summary>The text node every row works on holds five code units.</summary>
    private const string Node = "var t = document.getElementById('a').firstChild;";

    /// <summary>
    /// <c>substringData</c>: an offset past the end refuses, and a count that runs off the end is what is
    /// left rather than a refusal.
    /// </summary>
    [TestCase("t.substringData(0, 5)", "hello")]
    [TestCase("t.substringData(1, 2)", "el")]
    [TestCase("t.substringData(0, 10)", "hello")]
    [TestCase("t.substringData(0, -1)", "hello")]
    [TestCase("t.substringData(5, 1)", "")]
    [TestCase("t.substringData(6, 0)", "IndexSizeError")]
    [TestCase("t.substringData(-1, 10)", "IndexSizeError")]
    [TestCase("t.substringData(4294967295, 0)", "IndexSizeError")]
    [TestCase("t.substringData(4294967296, 0)", "")]
    public void SubstringDataClampsItsCountAndRefusesItsOffset(string source, string expected)
    {
        Answer("return String(" + source + ");").Should().Be(expected);
    }

    /// <summary>
    /// The three that go through replace data. Each row reads the data back, so what is asserted is the run
    /// that moved rather than only the absence of a throw.
    /// </summary>
    [TestCase("t.deleteData(0, 1)", "ello")]
    [TestCase("t.deleteData(1, 10)", "h")]
    [TestCase("t.deleteData(0, -1)", "")]
    [TestCase("t.deleteData(-1, 10)", "IndexSizeError")]
    [TestCase("t.deleteData(6, 0)", "IndexSizeError")]
    [TestCase("t.deleteData(5, 0)", "hello")]
    [TestCase("t.insertData(0, 'X')", "Xhello")]
    [TestCase("t.insertData(5, 'X')", "helloX")]
    [TestCase("t.insertData(6, 'X')", "IndexSizeError")]
    [TestCase("t.insertData(-1, 'X')", "IndexSizeError")]
    [TestCase("t.replaceData(0, 1, 'X')", "Xello")]
    [TestCase("t.replaceData(1, 10, 'X')", "hX")]
    [TestCase("t.replaceData(1, -1, 'X')", "hX")]
    [TestCase("t.replaceData(-1, 1, 'X')", "IndexSizeError")]
    [TestCase("t.replaceData(6, 0, 'X')", "IndexSizeError")]
    public void ReplaceDataClampsItsCountAndRefusesItsOffset(string source, string expected)
    {
        Answer(source + "; return t.data;").Should().Be(expected);
    }

    private static string? Answer(string body)
    {
        using var fixture = DomTestFixture.Create(Page);

        return fixture.Text($$"""
            (function () {
              {{Node}}
              try { {{body}} }
              catch (e) { return e.name || String(e); }
            })()
            """);
    }
}
