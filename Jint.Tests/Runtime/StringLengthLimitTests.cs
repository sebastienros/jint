using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// A JavaScript string has a maximum length, and every way of building one past it has to report the
/// same catchable <c>RangeError: Invalid string length</c>. Jint had no such limit: the size caps that
/// existed were the CLR's <c>Array.MaxLength</c>, they covered only <c>repeat</c> and the two pad
/// methods, and past <c>2^31</c> characters <c>ValueStringBuilder.Grow</c> computed its required size
/// as an unchecked <c>int</c>, wrapped negative and handed <c>ArrayPool&lt;char&gt;.Shared.Rent</c> an
/// invalid length. The <c>ArgumentOutOfRangeException</c> that produced escaped <c>Evaluate</c> past
/// every <c>catch</c> in the script.
/// <para>
/// The limit is now V8's own, <c>(1 &lt;&lt; 29) - 24</c>, so a script that builds too large a string
/// fails identically on both engines. The suite's own coverage of the reported shape is
/// <c>staging/sm/String/replace-math.js</c> — a 2^20-character subject replaced with 2^15 <c>$1</c>
/// tokens, so the substitution targets 2^35 characters — and <c>staging/</c> is not part of the
/// generated test262 projection at all, so the port lives here.
/// </para>
/// <para>
/// Every row below is a path whose limit is decided from small inputs — a repeat count, a pad length,
/// a replacement pattern's expansion factor, an array's <c>length</c> — so the throw happens before
/// anything of that size is allocated and the test costs a few megabytes. The remaining guarded paths
/// (<c>+=</c>, template literals, <c>concat</c>, <c>join</c>, <c>toLocaleString</c>,
/// <c>String.raw</c>, the accumulators inside <c>replace</c>/<c>replaceAll</c> and the element-by-element
/// half of <c>JSON.stringify</c>) accumulate their result one piece at a time, so their guard can only
/// fire once roughly half a billion characters are already in hand; a test for those would have to
/// allocate the gigabyte the guard exists to prevent, and there is no cheaper input that reaches them.
/// </para>
/// <para>
/// <c>+</c> used to be in that second list and no longer is: it defers a long result into an immutable
/// node rather than materializing it, so <c>s = s + s</c> doubles the length by referencing the same
/// value twice and reaches the limit through 29 nodes and no characters at all. The guard is checked
/// on the summed lengths, before the node is built, which is why it still fires there.
/// </para>
/// </summary>
public class StringLengthLimitTests
{
    private readonly Engine _engine = new();

    private const string InvalidStringLength = "RangeError: Invalid string length";

    /// <summary>
    /// What <paramref name="source"/> throws, as the running script sees it. The <c>try</c>/<c>catch</c>
    /// is written in JavaScript rather than with <c>Assert.Throws</c> precisely because a CLR exception
    /// escaping the engine is the bug: it would fail the test as an escaping exception instead of being
    /// reported here as a handled JavaScript error.
    /// </summary>
    private string Caught(string source) => _engine
        .Evaluate($"(function () {{ try {{ {source}; return 'did not throw'; }} catch (e) {{ return (e instanceof RangeError ? 'RangeError' : e.constructor.name) + ': ' + e.message; }} }})()")
        .AsString();

    [Test]
    public void TheLimitIsTheOneV8Uses()
    {
        JsString.MaxLength.Should().Be(536_870_888);
        JsString.MaxLength.Should().Be((1 << 29) - 24);
    }

    [Test]
    public void RepeatPastTheLimitIsACatchableRangeError()
    {
        Caught($"'xx'.repeat({JsString.MaxLength / 2 + 1})").Should().Be(InvalidStringLength);
        _engine.Evaluate("'xx'.repeat(3)").AsString().Should().Be("xxxxxx");
    }

    [Test]
    public void PadStartPastTheLimitIsACatchableRangeError()
    {
        Caught($"'x'.padStart({JsString.MaxLength + 1L})").Should().Be(InvalidStringLength);
        Caught("'x'.padStart(2147483647)").Should().Be(InvalidStringLength);
        _engine.Evaluate("'x'.padStart(3, 'ab')").AsString().Should().Be("abx");
    }

    [Test]
    public void PadEndPastTheLimitIsACatchableRangeError()
    {
        Caught($"'x'.padEnd({JsString.MaxLength + 1L}, 'ab')").Should().Be(InvalidStringLength);
        _engine.Evaluate("'x'.padEnd(3, 'ab')").AsString().Should().Be("xab");
    }

    /// <summary>
    /// Repeated doubling with <c>+</c>: each step references the previous value twice, so the length
    /// doubles while the representation grows by one node. The limit is therefore reached with nothing
    /// of that size allocated, which is exactly where the guard has to fire — on the summed lengths,
    /// before the result is built.
    /// </summary>
    [Test]
    public void ConcatenationPastTheLimitIsACatchableRangeError()
    {
        _engine.Execute("var s = 'x'; for (var i = 0; i < 28; i++) { s = s + s; }");
        _engine.Evaluate("s.length").AsNumber().Should().Be(268_435_456);
        _engine.Evaluate($"s.length <= {JsString.MaxLength}").AsBoolean().Should().BeTrue();

        // one more character fits and is refused nothing; one more doubling does not
        _engine.Evaluate("(s + 'y').length").AsNumber().Should().Be(268_435_457);
        Caught("s + s").Should().Be(InvalidStringLength);
        Caught("s + s + s").Should().Be(InvalidStringLength);
        Caught("'y' + s + s").Should().Be(InvalidStringLength);

        // deliberately not `s += s`: the compound form coerces its right operand to text first, so it
        // would flatten the half-gigabyte this row exists to avoid allocating. Its guard is the one the
        // paragraph above describes, and it stays untested for the reason given there.
    }

    /// <summary>
    /// <c>String.prototype.replace</c> with a string search value: the expansion is decided by how many
    /// <c>$&amp;</c> tokens the replacement holds, so a 2 KB pattern against a 2 MB subject is enough.
    /// </summary>
    [Test]
    public void ReplaceWhoseSubstitutionPastTheLimitIsACatchableRangeError()
    {
        _engine.Execute("var subject = 'x'.repeat(1 << 20); var pattern = '$&'.repeat(1024);");

        Caught("subject.replace(subject, pattern)").Should().Be(InvalidStringLength);
    }

    [Test]
    public void ReplaceAllWhoseSubstitutionPastTheLimitIsACatchableRangeError()
    {
        _engine.Execute("var subject = 'x'.repeat(1 << 20); var pattern = '$&'.repeat(1024);");

        Caught("subject.replaceAll(subject, pattern)").Should().Be(InvalidStringLength);
    }

    [Test]
    public void RegExpReplaceWhoseSubstitutionPastTheLimitIsACatchableRangeError()
    {
        _engine.Execute("var subject = 'x'.repeat(1 << 20); var pattern = '$&'.repeat(1024);");

        Caught("subject.replace(/x+/g, pattern)").Should().Be(InvalidStringLength);
    }

    /// <summary>
    /// The port of <c>staging/sm/String/replace-math.js</c>. Upstream accepts an out-of-memory failure
    /// as well as a success, because the point there was the arithmetic; here the point is that whatever
    /// happens is something the script can catch.
    /// </summary>
    [Test]
    public void ReplaceMathTargetsMoreCharactersThanAStringCanHold()
    {
        _engine.Execute("""
            function puff(x, n)
            {
              while (x.length < n)
                x += x;
              return x.substring(0, n);
            }

            var x = puff("1", 1 << 20);
            var rep = puff("$1", 1 << 16);
            """);

        Caught("x.replace(/(.+)/g, rep)").Should().Be(InvalidStringLength);
    }

    /// <summary>
    /// <c>JSON.stringify</c> builds its document one element at a time, but an array announces up front
    /// how many elements there are, and every index contributes at least two characters — a separator
    /// plus at least one character of value text, an absent element being written as <c>null</c>. So an
    /// array whose <c>length</c> alone puts the document past the limit is refused before the first
    /// element is written, which is what makes this row cost nothing: the array itself is empty.
    /// </summary>
    [Test]
    public void JsonStringifyOfAnArrayTooLongToSerializeIsACatchableRangeError()
    {
        Caught("JSON.stringify(Object.assign([], { length: 4294967295 }))").Should().Be(InvalidStringLength);
        Caught($"JSON.stringify(new Array({JsString.MaxLength / 2 + 1}))").Should().Be(InvalidStringLength);
    }

    /// <summary>
    /// The estimate above is a lower bound on the finished document, so it can never refuse one that
    /// would have fit. These are the shapes where over-counting would show up first: an element is
    /// written as <c>null</c> however it came to have no representation, and indentation only ever
    /// adds to the count.
    /// </summary>
    [TestCase("JSON.stringify(new Array(3))", "[null,null,null]")]
    [TestCase("JSON.stringify([1, undefined, function () {}, Symbol()])", "[1,null,null,null]")]
    [TestCase("JSON.stringify([1, 2], null, 4).replace(/\\n/g, '|')", "[|    1,|    2|]")]
    [TestCase("JSON.stringify({ a: undefined, b: 1 })", "{\"b\":1}")]
    [TestCase("JSON.stringify(['x'.repeat(1 << 20)]).length", "1048580")]
    public void JsonDocumentsThatFitAreUnaffected(string source, string expected)
    {
        _engine.Evaluate(source).ToString().Should().Be(expected);
    }

    /// <summary>
    /// <c>JSON.parse</c> needs no guard of its own — a reviver that builds too long a string does it
    /// through the ordinary string-building paths, which have one — but the error has to reach the
    /// script's <c>catch</c> through the parse machinery rather than being wrapped or swallowed on the
    /// way out. Verified with <c>+</c> as well as <c>repeat</c>, in a run that could afford the half
    /// gigabyte the concatenating shape costs.
    /// </summary>
    [Test]
    public void JsonParseReviverThatBuildsTooLongAStringIsACatchableRangeError()
    {
        Caught($"JSON.parse('{{\"a\":1}}', function (k, v) {{ return typeof v === 'number' ? 'x'.repeat({JsString.MaxLength + 1L}) : v; }})")
            .Should().Be(InvalidStringLength);

        _engine.Evaluate("JSON.stringify(JSON.parse('{\"a\":1}', function (k, v) { return typeof v === 'number' ? v + 1 : v; }))")
            .AsString().Should().Be("{\"a\":2}");
    }

    /// <summary>
    /// The up-front expansion estimate that makes the rows above cheap counts only the tokens whose
    /// contribution is knowable without running user code, so it can never over-count and can never
    /// refuse a substitution that fits. These are the shapes where over-counting would show up first.
    /// </summary>
    [TestCase(@"'abcabc'.replace(/b/g, ""[$`|$&|$']"")", "a[a|b|cabc]ca[abca|b|c]c")]
    [TestCase(@"'abc'.replace(/(a)(b)(c)/, '$3$2$1$12$0$')", "cbaa2$0$")]
    [TestCase(@"'John Smith'.replace(/(?<first>\w+) (?<last>\w+)/, '$<last>, $<first>')", "Smith, John")]
    [TestCase(@"'abc'.replace(/b/, '$<x>')", "a$<x>c")]
    [TestCase(@"'abc'.replace('b', '$<x>')", "a$<x>c")]
    [TestCase(@"'abc'.replace(/(b)/, '$<a$&b>')", "a$<abb>c")]
    [TestCase(@"'John'.replace(/(?<n>\w+)/, '$<a$&b>')", "")]
    [TestCase(@"'aaa'.replaceAll('a', '$&$&')", "aaaaaa")]
    [TestCase(@"'abc'.replace('b', '$$')", "a$c")]
    [TestCase(@"'xyz'.replace('y', ""$`-$'-$&"")", "xx-z-yz")]
    public void SubstitutionPatternsThatFitAreUnaffected(string source, string expected)
    {
        _engine.Evaluate(source).AsString().Should().Be(expected);
    }
}
