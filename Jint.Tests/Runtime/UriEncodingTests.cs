using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>encodeURI</c>/<c>encodeURIComponent</c> answer an input that needs no escaping without building a
/// buffer, and copy whole clean runs between escapes rather than one character per turn. Both are pure
/// short-cuts through https://tc39.es/ecma262/#sec-encode, so these pin the boundaries the short-cuts
/// have to get right: an empty input, an input that is entirely clean, one that starts or ends with an
/// escape, adjacent escapes, and the surrogate handling — where the walk consumes two characters at once
/// and a malformed pair must still raise a URIError rather than being copied over as part of a clean run.
/// <para>
/// Inputs that contain characters with no UTF-8 encoding (lone surrogates) or no visible spelling (NUL) are
/// built with <c>String.fromCharCode</c> rather than written as C# literals: a source file cannot carry a
/// lone surrogate at all — it stores U+FFFD instead, and the test silently stops testing anything.
/// </para>
/// </summary>
public class UriEncodingTests
{
    private readonly Engine _engine = new();

    // nothing to escape - the whole input is in the allowed set for both functions
    [TestCase("''", "", "")]
    [TestCase("'abc'", "abc", "abc")]
    [TestCase("\"abcABC123-_.!~*'()\"", "abcABC123-_.!~*'()", "abcABC123-_.!~*'()")]
    // escape at the very start, at the very end, and both
    [TestCase("' abc'", "%20abc", "%20abc")]
    [TestCase("'abc '", "abc%20", "abc%20")]
    [TestCase("' abc '", "%20abc%20", "%20abc%20")]
    [TestCase("' '", "%20", "%20")]
    // adjacent escapes, and a clean run between two of them
    [TestCase("'a  b'", "a%20%20b", "a%20%20b")]
    [TestCase("'a b c'", "a%20b%20c", "a%20b%20c")]
    [TestCase("'  '", "%20%20", "%20%20")]
    // the two functions disagree: reserved characters are clean for encodeURI only
    [TestCase("';/?:@&=+$,'", ";/?:@&=+$,", "%3B%2F%3F%3A%40%26%3D%2B%24%2C")]
    [TestCase("'#'", "#", "%23")]
    [TestCase("'https://example.com/a?b=c&d=e#f'", "https://example.com/a?b=c&d=e#f", "https%3A%2F%2Fexample.com%2Fa%3Fb%3Dc%26d%3De%23f")]
    // multi-byte UTF-8, and a surrogate pair, each surrounded by clean runs
    [TestCase("'café'", "caf%C3%A9", "caf%C3%A9")]
    [TestCase("'中文'", "%E4%B8%AD%E6%96%87", "%E4%B8%AD%E6%96%87")]
    [TestCase("'a😀b'", "a%F0%9F%98%80b", "a%F0%9F%98%80b")]
    [TestCase("String.fromCharCode(0)", "%00", "%00")]
    [TestCase("'ok' + String.fromCharCode(0) + 'ok'", "ok%00ok", "ok%00ok")]
    public void EncodesTheSameWhateverTheShapeOfTheInput(string expression, string expectedUri, string expectedComponent)
    {
        _engine.Evaluate($"encodeURI({expression})").AsString().Should().Be(expectedUri);
        _engine.Evaluate($"encodeURIComponent({expression})").AsString().Should().Be(expectedComponent);
    }

    /// <summary>
    /// An unpaired surrogate is a URIError (step 2.c / 2.e.iii of the encode algorithm). A clean-run copy
    /// must never swallow one: no surrogate is in either allowed set, so a run always stops before it.
    /// </summary>
    [TestCase("String.fromCharCode(0xD800)")]
    [TestCase("'a' + String.fromCharCode(0xD800)")]
    [TestCase("String.fromCharCode(0xDFFF)")]
    [TestCase("'a' + String.fromCharCode(0xDFFF) + 'b'")]
    [TestCase("'clean text ' + String.fromCharCode(0xD800) + ' more'")]
    [TestCase("'clean text ' + String.fromCharCode(0xD800, 0xD800)")]
    [TestCase("'clean text ' + String.fromCharCode(0xDC00, 0xD800)")]
    public void AnUnpairedSurrogateIsAUriErrorHoweverMuchCleanTextSurroundsIt(string expression)
    {
        Invoking(() => _engine.Evaluate($"encodeURI({expression})")).Should().Throw<JavaScriptException>();
        Invoking(() => _engine.Evaluate($"encodeURIComponent({expression})")).Should().Throw<JavaScriptException>();
    }

    /// <summary>
    /// Round-tripping is the strongest single statement about the walk: whatever the escapes, decoding the
    /// result has to give the input back.
    /// </summary>
    [Test]
    public void EveryEncodableCharacterRoundTrips()
    {
        var result = _engine.Evaluate("""
            var bad = 0;
            for (var i = 0; i <= 0xFFFF; i++) {
                if (i >= 0xD800 && i <= 0xDFFF) continue;
                var s = String.fromCharCode(i);
                if (decodeURIComponent(encodeURIComponent(s)) !== s) bad++;
            }
            bad;
            """).AsNumber();

        result.Should().Be(0);
    }
}
