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
