#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>atob</c> and <c>btoa</c> as HTML specifies them —
/// https://html.spec.whatwg.org/multipage/webappapis.html#atob — over Infra's forgiving-base64
/// algorithms, https://infra.spec.whatwg.org/#forgiving-base64.
/// </summary>
public class Base64Tests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Base64));

    [TestCase("", "")]
    [TestCase("f", "Zg==")]
    [TestCase("fo", "Zm8=")]
    [TestCase("foo", "Zm9v")]
    [TestCase("foob", "Zm9vYg==")]
    [TestCase("fooba", "Zm9vYmE=")]
    [TestCase("foobar", "Zm9vYmFy")]
    [TestCase("hello world", "aGVsbG8gd29ybGQ=")]
    public void BtoaEncodesTheRfc4648Vectors(string input, string expected)
    {
        var engine = WebEngine();

        engine.SetValue("input", input);
        engine.Evaluate("btoa(input)").AsString().Should().Be(expected);
    }

    [Test]
    public void BtoaTakesOneByteFromEachCodeUnit()
    {
        var engine = WebEngine();

        // U+00FF is the highest code unit it accepts, and it becomes the byte 0xFF.
        engine.Evaluate("btoa('\\u00ff')").AsString().Should().Be("/w==");
        engine.Evaluate("btoa('\\u0000\\u0001\\u00fe\\u00ff')").AsString().Should().Be("AAH+/w==");
    }

    [TestCase("\\u0100")]
    [TestCase("a\\u0100b")]
    [TestCase("€")]
    // btoa takes a DOMString, not a USVString, so a lone surrogate is not replaced by U+FFFD first — it
    // is simply a code unit above U+00FF.
    [TestCase("\\ud800")]
    [TestCase("😀")]
    public void BtoaRefusesACodeUnitAboveLatin1(string input)
    {
        var engine = WebEngine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate($"btoa('{input}')"))!;

        exception.Error.Get("name").AsString().Should().Be("InvalidCharacterError");
        exception.Error.Get("code").AsNumber().Should().Be(5);
        engine.SetValue("thrown", exception.Error);
        engine.Evaluate("thrown instanceof DOMException").AsBoolean().Should().BeTrue();
    }

    [TestCase("", "")]
    [TestCase("Zg==", "f")]
    [TestCase("Zm8=", "fo")]
    [TestCase("Zm9v", "foo")]
    [TestCase("aGVsbG8gd29ybGQ=", "hello world")]
    // Unpadded input is accepted: this is where "forgiving" starts.
    [TestCase("Zg", "f")]
    [TestCase("Zm8", "fo")]
    [TestCase("Zm9vYg", "foob")]
    public void AtobDecodes(string input, string expected)
    {
        var engine = WebEngine();

        engine.SetValue("input", input);
        engine.Evaluate("atob(input)").AsString().Should().Be(expected);
    }

    // Step 1 removes ASCII whitespace from anywhere, not just the ends.
    [TestCase(" Zm9v ")]
    [TestCase("Zm\n9v")]
    [TestCase("Z m 9 v")]
    [TestCase("\tZ\rm\f9v")]
    [TestCase("Zm9v\n")]
    public void AtobStripsAsciiWhitespaceAnywhere(string input)
    {
        var engine = WebEngine();

        engine.SetValue("input", input);
        engine.Evaluate("atob(input)").AsString().Should().Be("foo");
    }

    // A code point outside the alphabet.
    [TestCase("a*bc")]
    [TestCase("ab-c")]
    [TestCase("ab_c")]
    [TestCase("Zm9v!")]
    // U+000B VT is not ASCII whitespace, so it is a stray code point rather than something to strip.
    [TestCase("Zm\u000B9v")]
    // Step 3: a length that leaves a remainder of one.
    [TestCase("a")]
    [TestCase("abcde")]
    // Padding is only removed when the length already divides by four, so these keep their '=' and then
    // fail the alphabet check.
    [TestCase("Zg=")]
    [TestCase("Zg===")]
    [TestCase("=")]
    [TestCase("==")]
    [TestCase("====")]
    [TestCase("Zm9vYg====")]
    // '=' in the middle is never removed.
    [TestCase("Z=9v")]
    public void AtobRefusesWhatForgivingBase64CallsFailure(string input)
    {
        var engine = WebEngine();

        engine.SetValue("input", input);
        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate("atob(input)"))!;

        exception.Error.Get("name").AsString().Should().Be("InvalidCharacterError");
        exception.Error.Get("code").AsNumber().Should().Be(5);
    }

    [Test]
    public void AtobRemovesAtMostTwoPaddingCodePoints()
    {
        var engine = WebEngine();

        // Four '=' is a length of four, so two are removed and the remaining two are stray code points.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("atob('====')"));

        // Eight characters ending in two '=' is fine ...
        engine.Evaluate("atob('Zm9vYg==')").AsString().Should().Be("foob");

        // ... and the padding may itself be split by whitespace, since step 1 runs first.
        engine.Evaluate("atob('Zm9vYg= =')").AsString().Should().Be("foob");
    }

    [Test]
    public void AtobProducesAByteString()
    {
        var engine = WebEngine();

        // Every code unit of the result is a byte, U+0000 to U+00FF.
        engine.Evaluate("""
            const decoded = atob('AAH+/w==');
            [decoded.length, decoded.charCodeAt(0), decoded.charCodeAt(1), decoded.charCodeAt(2), decoded.charCodeAt(3)].join(',');
            """).AsString().Should().Be("4,0,1,254,255");
    }

    [Test]
    public void RoundTripsEveryByteValue()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            let all = '';
            for (let i = 0; i < 256; i++) { all += String.fromCharCode(i); }
            atob(btoa(all)) === all;
            """).AsBoolean().Should().BeTrue();
    }

    [Test]
    public void CoercesItsArgumentToAString()
    {
        var engine = WebEngine();

        engine.Evaluate("btoa(123)").AsString().Should().Be("MTIz");
        engine.Evaluate("btoa(null)").AsString().Should().Be("bnVsbA==");
        engine.Evaluate("btoa({ toString() { return 'foo'; } })").AsString().Should().Be("Zm9v");
        engine.Evaluate("atob({ toString() { return 'Zm9v'; } })").AsString().Should().Be("foo");

        // "undefined" is nine code points, and step 3 refuses a length that leaves a remainder of one.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("atob(undefined)"));
    }

    [Test]
    public void AreOrdinaryGlobalFunctions()
    {
        var engine = WebEngine();

        engine.Evaluate("typeof atob").AsString().Should().Be("function");
        engine.Evaluate("typeof btoa").AsString().Should().Be("function");
        engine.Evaluate("atob.length").AsNumber().Should().Be(1);
        engine.Evaluate("btoa.length").AsNumber().Should().Be(1);
        engine.Evaluate("atob.name").AsString().Should().Be("atob");
        engine.Evaluate("btoa.name").AsString().Should().Be("btoa");

        // They are not constructors.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new btoa('a')"));
    }
}
#endif
