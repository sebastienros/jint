#if NET8_0_OR_GREATER
#nullable enable

using System.Text;
using Jint.WebApi.Fetch;
using Jint.WebApi.Url.Parsing;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>data:</c> URL processor, https://fetch.spec.whatwg.org/#data-url-processor.
/// </summary>
/// <remarks>
/// It has one implementation in the repository and two callers — a page's navigation and a page's
/// <c>&lt;script src="data:…"&gt;</c> — so the steps that are easy to get subtly wrong are measured here
/// rather than through either of them: where the base64 marker ends, what an absent MIME type defaults to,
/// and which inputs are the specification's failure.
/// </remarks>
public class DataUrlTests
{
    [TestCase("data:,hello", "hello", "text/plain;charset=US-ASCII")]
    [TestCase("data:text/plain,hello", "hello", "text/plain")]
    [TestCase("data:text/javascript,a%20b", "a b", "text/javascript")]
    [TestCase("data:text/plain;base64,aGVsbG8=", "hello", "text/plain")]
    // Step 11 leaves the MIME type empty here, and step 12 prepends nothing to an empty string --
    // so it is step 14's default rather than "text/plain", which is what a browser answers too.
    [TestCase("data:;base64,aGVsbG8=", "hello", "text/plain;charset=US-ASCII")]
    [TestCase("data:text/plain;charset=utf-8;base64,aGVsbG8=", "hello", "text/plain;charset=utf-8")]
    public void TheProcessorAnswersABodyAndAMimeType(string url, string expectedBody, string expectedMimeType)
    {
        Process(url, out var body, out var mimeType).Should().BeTrue();
        body.Should().Be(expectedBody);
        mimeType.Should().Be(expectedMimeType);
    }

    [Test]
    public void TheBase64MarkerIsRecognizedWithSpacesAndInAnyCase()
    {
        // Step 11 is ";" then zero or more U+0020 SPACE then an ASCII case-insensitive "base64", and steps
        // 11.4 to 11.6 remove exactly that much.
        Process("data:text/plain;   BaSe64,aGVsbG8=", out var body, out var mimeType).Should().BeTrue();
        body.Should().Be("hello");
        mimeType.Should().Be("text/plain");
    }

    [Test]
    public void AMimeTypeEndingInBase64WithNoSemicolonIsNotTheMarker()
    {
        // "x/base64" ends with the six code points and is still an ordinary MIME type, so the body is
        // percent-decoded and not base64-decoded.
        Process("data:x/base64,aGVsbG8=", out var body, out var mimeType).Should().BeTrue();
        body.Should().Be("aGVsbG8=");
        mimeType.Should().Be("x/base64");
    }

    [Test]
    public void AnUnparseableMimeTypeFallsBackToTextPlainUsAscii()
    {
        // Step 14: a MIME type that is not a MIME type at all does not fail the URL, it defaults.
        Process("data:not-a-mime-type,hello", out var body, out var mimeType).Should().BeTrue();
        body.Should().Be("hello");
        mimeType.Should().Be("text/plain;charset=US-ASCII");
    }

    [Test]
    public void ForgivingBase64AcceptsAnUnpaddedPayload()
    {
        // https://infra.spec.whatwg.org/#forgiving-base64-decode, which Convert.FromBase64String is not: a
        // twenty-six code point payload decodes here and throws there.
        Process("data:text/plain;base64,PHAgaWQ9eD5kZWNvZGVkPC9wPg", out var body, out _).Should().BeTrue();
        body.Should().Be("<p id=x>decoded</p>");
    }

    [Test]
    public void ALonePercentSignIsKeptRatherThanRefused()
    {
        // https://url.spec.whatwg.org/#percent-decode appends a "%" that is not followed by two hex digits
        // and moves on; Uri.UnescapeDataString throws on the same input.
        Process("data:text/plain,100%25%20of%20%zz", out var body, out _).Should().BeTrue();
        body.Should().Be("100% of %zz");
    }

    [TestCase("data:text/plain")]
    [TestCase("data:")]
    [TestCase("data:text/plain;base64,%%%")]
    public void TheSpecificationsFailuresAreFailures(string url)
        => Process(url, out _, out _).Should().BeFalse();

    [Test]
    public void TheFragmentIsNoPartOfTheBody()
    {
        // Step 2 serializes with the fragment excluded, and the URL parser has already split it off at the
        // first "#" — so a "#" in what looks like the payload ends the payload.
        Process("data:text/plain,before#after", out var body, out _).Should().BeTrue();
        body.Should().Be("before");
    }

    /// <summary>
    /// The ceiling a caller passes is held against the <i>decoded</i> length, inclusive, whichever way the
    /// payload is encoded — percent-encoding, base64, base64 with whitespace and without its padding.
    /// </summary>
    [TestCase("data:,hello", 5, true)]
    [TestCase("data:,hello", 4, false)]
    [TestCase("data:,%68%65%6C%6C%6F", 5, true)]
    [TestCase("data:,%68%65%6C%6C%6F", 4, false)]
    [TestCase("data:;base64,aGVsbG8=", 5, true)]
    [TestCase("data:;base64,aGVsbG8=", 4, false)]
    [TestCase("data:;base64,aGVs%20bG8", 5, true)]
    [TestCase("data:;base64,aGVs%20bG8", 4, false)]
    [TestCase("data:;base64,aGk=", 2, true)]
    [TestCase("data:;base64,aGk=", 1, false)]
    [TestCase("data:,", 0, true)]
    public void TheCeilingIsHeldAgainstTheDecodedLength(string url, long ceiling, bool fits)
    {
        DataUrl.TryProcess(UrlParser.Parse(url)!, ceiling, out var content, out var tooLarge).Should().Be(fits);
        tooLarge.Should().Be(!fits);

        if (fits)
        {
            content.Body.Length.Should().BeLessThanOrEqualTo((int) ceiling);
        }
    }

    /// <summary>
    /// A payload whose length already says it is over the ceiling is refused before anything decodes it.
    /// </summary>
    [Test]
    public void APayloadTooLongForTheCeilingIsRefusedBeforeItIsDecoded()
    {
        // Five significant code points are not base64 at all — a remainder of one is forgiving-base64's
        // failure — so decoding this payload fails. Against a ceiling of two it is refused as too large
        // instead, which it can only be if its length was measured before anything tried to decode it.
        var url = UrlParser.Parse("data:;base64,aaaaa")!;

        DataUrl.TryProcess(url, 2, out _, out var tooLarge).Should().BeFalse();
        tooLarge.Should().BeTrue();

        // With room for what its length says it decodes to, it is decoded — and fails as the processor's
        // own failure.
        DataUrl.TryProcess(url, 3, out _, out tooLarge).Should().BeFalse();
        tooLarge.Should().BeFalse();
    }

    private static bool Process(string url, out string body, out string mimeType)
    {
        var record = UrlParser.Parse(url);
        record.Should().NotBeNull();

        if (!DataUrl.TryProcess(record!, out var content))
        {
            body = "";
            mimeType = "";
            return false;
        }

        body = Encoding.UTF8.GetString(content.Body);
        mimeType = content.MimeType.Serialize();
        return true;
    }
}
#endif
