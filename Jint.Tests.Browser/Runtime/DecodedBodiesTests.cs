using System.Net.Http;
using System.Text;
using Jint.Browser.Runtime;
using Jint.Browser.Runtime.Parsing;
using Jint.WebApi.Fetch;

namespace Jint.Tests.Browser.Runtime;

/// <summary>
/// A body the HTTP cache serves is its stored array, and the pages decoding that array share its text.
/// </summary>
public sealed class DecodedBodiesTests
{
    [Test]
    public async Task ACachedBodyIsReadAsTheStoredArray()
    {
        var stored = Encoding.UTF8.GetBytes("window.ran = true;");
        using var cached = new HttpResponseMessage { Content = new HttpResponseCache.StoredBodyContent(stored) };
        (await SubresourceFetch.ReadBoundedAsync(cached, long.MaxValue, default)).Should().BeSameAs(stored);

        using var network = new HttpResponseMessage { Content = new ByteArrayContent(stored) };
        var read = await SubresourceFetch.ReadBoundedAsync(network, long.MaxValue, default);
        read.Should().NotBeSameAs(stored).And.Equal(stored);
    }

    [Test]
    public void AScriptBodyIsDecodedOncePerEncoding()
    {
        var bytes = Encoding.UTF8.GetBytes("﻿var café = 1;");
        var body = new FetchedSubresource(bytes, "text/javascript", "https://example.test/a.js", null, 200);
        var text = body.Text();
        text.Should().Be("var café = 1;");
        body.Text().Should().BeSameAs(text);
        new FetchedSubresource(bytes, "text/javascript", "https://example.test/a.js", null, 200).Text().Should().BeSameAs(text);

        // Another encoding is another text.
        var utf16 = new FetchedSubresource(bytes, "text/javascript; charset=utf-16", "https://example.test/a.js", null, 200).Text();
        utf16.Should().NotBe(text);
    }

    [TestCase(new byte[] { 0xEF, 0xBB, 0xBF, (byte) 'a', (byte) '{', (byte) '}' }, null, null)]
    [TestCase(new byte[] { 0xFF, 0xFE, (byte) 'a', 0, (byte) '{', 0, (byte) '}', 0 }, null, null)]
    [TestCase(new byte[] { (byte) 'a', (byte) '{', 0xE9, (byte) '}' }, "text/css; charset=windows-1252", null)]
    [TestCase(new byte[] { (byte) 'a', (byte) '{', 0xE9, (byte) '}' }, "text/css", "windows-1252")]
    [TestCase(new byte[] { (byte) 'a', (byte) '{', 0xE9, (byte) '}' }, "text/css", null)]
    public void AStyleSheetBodyDecodesAsItsBytesDoAndSharesItsText(byte[] bytes, string? contentType, string? environment)
    {
        var expected = CssStyleSheetDecoding.Decode(bytes.AsSpan(), contentType, environment);
        var first = CssStyleSheetDecoding.Decode(bytes, contentType, environment);
        first.Should().Be(expected);
        CssStyleSheetDecoding.Decode(bytes, contentType, environment).Text.Should().BeSameAs(first.Text);
    }

    [Test]
    public void AStyleSheetBodyDecodedForAnotherDocumentEncodingIsDecodedAgain()
    {
        var bytes = new byte[] { (byte) 'a', (byte) '{', 0xE9, (byte) '}' };
        var utf8 = CssStyleSheetDecoding.Decode(bytes, "text/css", "utf-8");
        var latin = CssStyleSheetDecoding.Decode(bytes, "text/css", "windows-1252");
        utf8.Text.Should().Be("a{�}");
        latin.Text.Should().Be("a{é}");
        latin.Encoding.Should().Be("windows-1252");
        CssStyleSheetDecoding.Decode(bytes, "text/css", "utf-8").Should().Be(utf8);
    }
}
