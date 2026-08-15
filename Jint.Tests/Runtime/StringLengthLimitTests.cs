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
/// a replacement pattern's expansion factor — so the throw happens before anything of that size is
/// allocated and the test costs a few megabytes. The remaining guarded paths (<c>+</c>/<c>+=</c>,
/// template literals, <c>concat</c>, <c>join</c>, <c>toLocaleString</c>, <c>String.raw</c> and the
/// accumulators inside <c>replace</c>/<c>replaceAll</c>) accumulate their result one piece at a time,
/// so their guard can only fire once roughly half a billion characters are already in hand; a test for
/// those would have to allocate the gigabyte the guard exists to prevent, and there is no cheaper
/// input that reaches them.
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

    [Fact]
    public void TheLimitIsTheOneV8Uses()
    {
        JsString.MaxLength.Should().Be(536_870_888);
        JsString.MaxLength.Should().Be((1 << 29) - 24);
    }

    [Fact]
    public void RepeatPastTheLimitIsACatchableRangeError()
    {
        Caught($"'xx'.repeat({JsString.MaxLength / 2 + 1})").Should().Be(InvalidStringLength);
        _engine.Evaluate("'xx'.repeat(3)").AsString().Should().Be("xxxxxx");
    }

    [Fact]
    public void PadStartPastTheLimitIsACatchableRangeError()
    {
        Caught($"'x'.padStart({JsString.MaxLength + 1L})").Should().Be(InvalidStringLength);
        Caught("'x'.padStart(2147483647)").Should().Be(InvalidStringLength);
        _engine.Evaluate("'x'.padStart(3, 'ab')").AsString().Should().Be("abx");
    }

    [Fact]
    public void PadEndPastTheLimitIsACatchableRangeError()
    {
        Caught($"'x'.padEnd({JsString.MaxLength + 1L}, 'ab')").Should().Be(InvalidStringLength);
        _engine.Evaluate("'x'.padEnd(3, 'ab')").AsString().Should().Be("xab");
    }

    /// <summary>
    /// <c>String.prototype.replace</c> with a string search value: the expansion is decided by how many
    /// <c>$&amp;</c> tokens the replacement holds, so a 2 KB pattern against a 2 MB subject is enough.
    /// </summary>
    [Fact]
    public void ReplaceWhoseSubstitutionPastTheLimitIsACatchableRangeError()
    {
        _engine.Execute("var subject = 'x'.repeat(1 << 20); var pattern = '$&'.repeat(1024);");

        Caught("subject.replace(subject, pattern)").Should().Be(InvalidStringLength);
    }

    [Fact]
    public void ReplaceAllWhoseSubstitutionPastTheLimitIsACatchableRangeError()
    {
        _engine.Execute("var subject = 'x'.repeat(1 << 20); var pattern = '$&'.repeat(1024);");

        Caught("subject.replaceAll(subject, pattern)").Should().Be(InvalidStringLength);
    }

    [Fact]
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
    [Fact]
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
    /// The up-front expansion estimate that makes the rows above cheap counts only the tokens whose
    /// contribution is knowable without running user code, so it can never over-count and can never
    /// refuse a substitution that fits. These are the shapes where over-counting would show up first.
    /// </summary>
    [Theory]
    [InlineData(@"'abcabc'.replace(/b/g, ""[$`|$&|$']"")", "a[a|b|cabc]ca[abca|b|c]c")]
    [InlineData(@"'abc'.replace(/(a)(b)(c)/, '$3$2$1$12$0$')", "cbaa2$0$")]
    [InlineData(@"'John Smith'.replace(/(?<first>\w+) (?<last>\w+)/, '$<last>, $<first>')", "Smith, John")]
    [InlineData(@"'abc'.replace(/b/, '$<x>')", "a$<x>c")]
    [InlineData(@"'abc'.replace('b', '$<x>')", "a$<x>c")]
    [InlineData(@"'abc'.replace(/(b)/, '$<a$&b>')", "a$<abb>c")]
    [InlineData(@"'John'.replace(/(?<n>\w+)/, '$<a$&b>')", "")]
    [InlineData(@"'aaa'.replaceAll('a', '$&$&')", "aaaaaa")]
    [InlineData(@"'abc'.replace('b', '$$')", "a$c")]
    [InlineData(@"'xyz'.replace('y', ""$`-$'-$&"")", "xx-z-yz")]
    public void SubstitutionPatternsThatFitAreUnaffected(string source, string expected)
    {
        _engine.Evaluate(source).AsString().Should().Be(expected);
    }
}
