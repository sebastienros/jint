#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Text;

namespace Jint.Tests.Wpt;

/// <summary>
/// The driver's server, held to what the <c>.py</c> handlers it stands in for actually do.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these exist.</b> <see cref="WptServer"/> is the one part of the corpus that is <i>not</i> vendored
/// bytes — it is a C# reimplementation of upstream's Python, and a reimplementation that quietly answered
/// something else would make a suite assert against the driver rather than against the engine. Every
/// assertion below is written from the upstream handler's source at the pinned commit, named in the test, so
/// a corpus bump that changes one has something to fail.
/// </para>
/// <para>
/// They also make the server's own failures legible. A suite failing because a handler answered wrongly and a
/// suite failing because the engine is wrong look identical in the exclusion table; these separate the two.
/// </para>
/// </remarks>
public class WptServerTests
{
    private static readonly HttpClient _client = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private static Task<HttpResponseMessage> GetAsync(string path)
        => _client.GetAsync(WptServer.Instance.Origin + path);

    /// <summary>
    /// <c>fetch/api/resources/inspect-headers.py</c> echoes each named request header back as
    /// <c>x-request-&lt;name&gt;</c>, and names it does not find are simply absent.
    /// </summary>
    [Test]
    public async Task InspectHeadersEchoesTheHeadersItWasAskedFor()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            WptServer.Instance.Origin + "/fetch/api/resources/inspect-headers.py?headers=x-foo|x-absent");
        request.Headers.TryAddWithoutValidation("x-foo", "bar");

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("x-request-x-foo").Should().ContainSingle().Which.Should().Be("bar");
        response.Headers.Contains("x-request-x-absent").Should().BeFalse();
    }

    /// <summary>
    /// <c>fetch/api/resources/status.py</c> takes its code, its reason phrase, its content type and its body
    /// from the query, and echoes the request method.
    /// </summary>
    [Test]
    public async Task StatusAnswersWithWhatTheQueryAsksFor()
    {
        using var response = await GetAsync("/fetch/api/resources/status.py?code=418&text=Nope&type=text/plain&content=hi");

        ((int) response.StatusCode).Should().Be(418);
        response.ReasonPhrase.Should().Be("Nope");
        response.Content.Headers.ContentType!.ToString().Should().Be("text/plain");
        response.Headers.GetValues("x-request-method").Should().ContainSingle().Which.Should().Be("GET");
        (await response.Content.ReadAsStringAsync()).Should().Be("hi");
    }

    /// <summary>
    /// The query is percent-decoded to <b>bytes</b>, which is what <c>fetch/api/basic/text-utf8.any.js</c>
    /// depends on: it asks for a UTF-16BE body and reads it back through a UTF-8 decoder, so a server that
    /// decoded the query as text would have replaced the invalid sequences before the engine saw them.
    /// </summary>
    [Test]
    public async Task TheQueryIsDecodedToBytesRatherThanToText()
    {
        using var response = await GetAsync("/fetch/api/resources/status.py?code=200&content=%fe%ff%4e%09");

        var body = await response.Content.ReadAsByteArrayAsync();
        body.Should().Equal(0xFE, 0xFF, 0x4E, 0x09);
    }

    /// <summary>
    /// <c>fetch/api/resources/method.py</c> echoes the method, the four content headers (or <c>NO</c>) and
    /// the request body — which is how <c>redirect-method.any.js</c> sees what a redirect kept.
    /// </summary>
    [Test]
    public async Task MethodEchoesTheRequestBackToTheCaller()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, WptServer.Instance.Origin + "/fetch/api/resources/method.py")
        {
            Content = new StringContent("this is my body", Encoding.UTF8, "text/plain"),
        };

        using var response = await _client.SendAsync(request);

        response.Headers.GetValues("x-request-method").Should().ContainSingle().Which.Should().Be("POST");
        response.Headers.GetValues("x-request-content-type").Should().ContainSingle().Which.Should().Be("text/plain; charset=utf-8");
        response.Headers.GetValues("x-request-content-language").Should().ContainSingle().Which.Should().Be("NO");
        (await response.Content.ReadAsStringAsync()).Should().Be("this is my body");
    }

    /// <summary>
    /// <c>fetch/api/resources/redirect.py</c> answers the status the query names, and — because
    /// <c>simple</c> was not passed — rewrites the location to carry the original query plus a
    /// <c>count=</c> that changes on every hop.
    /// </summary>
    [Test]
    public async Task RedirectAnswersTheStatusAndRewritesTheLocation()
    {
        using var response = await GetAsync("/fetch/api/resources/redirect.py?redirect_status=307&location=top.txt");

        ((int) response.StatusCode).Should().Be(307);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith("top.txt?");
        location.Should().Contain("redirect_status=307");
        location.Should().EndWith("&count=1");
    }

    /// <summary>
    /// <c>simple</c> suppresses that rewrite, which is the branch a location with a non-http scheme also
    /// takes.
    /// </summary>
    [Test]
    public async Task RedirectLeavesASimpleLocationAlone()
    {
        using var response = await GetAsync("/fetch/api/resources/redirect.py?simple&location=top.txt");

        ((int) response.StatusCode).Should().Be(302);
        response.Headers.Location!.ToString().Should().Be("top.txt");
    }

    /// <summary>
    /// The token stash counts hops across requests, and past <c>max_count</c> the handler stops redirecting
    /// and answers 200 with the count instead — one less than the hops it served, because the reporting hop
    /// is not itself a redirection. <c>redirect-count.any.js</c> asserts exactly that number.
    /// </summary>
    [Test]
    public async Task RedirectCountsHopsInTheStashAndReportsThemAtMaxCount()
    {
        var token = "jint-" + Guid.NewGuid().ToString("N");
        var url = $"/fetch/api/resources/redirect.py?token={token}&redirect_status=302&max_count=3&location=x";

        for (var i = 0; i < 3; i++)
        {
            using var hop = await GetAsync(url);
            ((int) hop.StatusCode).Should().Be(302, $"hop {i + 1} of 3 is still under max_count");
        }

        using var last = await GetAsync(url);
        ((int) last.StatusCode).Should().Be(200);
        (await last.Content.ReadAsStringAsync()).Should().Be("3");

        // And clean-stash.py puts it back, which is what every one of that file's tests starts with.
        using var cleaned = await GetAsync($"/fetch/api/resources/clean-stash.py?token={token}");
        ((int) cleaned.StatusCode).Should().Be(200);

        using var restarted = await GetAsync(url);
        ((int) restarted.StatusCode).Should().Be(302);
    }

    /// <summary><c>fetch/api/resources/redirect-empty-location.py</c>: a 302 with an empty <c>Location</c>.</summary>
    [Test]
    public async Task RedirectEmptyLocationSendsAnEmptyLocationHeader()
    {
        using var response = await GetAsync("/fetch/api/resources/redirect-empty-location.py");

        ((int) response.StatusCode).Should().Be(302);
        response.Headers.TryGetValues("location", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().BeEmpty();
    }

    /// <summary>
    /// <c>fetch/api/resources/trickle.py</c> writes its lines one at a time with a delay between them, which
    /// is what <c>stream-response.any.js</c> and <c>response-cancel-stream.any.js</c> are about. The
    /// assertion is on the bytes rather than on the timing: a wall-clock assertion is the kind of thing that
    /// makes a suite flaky on a loaded machine.
    /// </summary>
    [Test]
    public async Task TrickleWritesTheLinesItWasAskedFor()
    {
        using var response = await GetAsync("/fetch/api/resources/trickle.py?ms=1&count=4");

        response.Content.Headers.ContentType!.ToString().Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be("TEST_TRICKLE\nTEST_TRICKLE\nTEST_TRICKLE\nTEST_TRICKLE\n");
    }

    /// <summary><c>notype</c> is what makes it send no <c>Content-Type</c> at all.</summary>
    [Test]
    public async Task TrickleSendsNoContentTypeWhenAskedNotTo()
    {
        using var response = await GetAsync("/fetch/api/resources/trickle.py?ms=1&count=1&notype=true");

        response.Content.Headers.ContentType.Should().BeNull();
    }

    /// <summary>
    /// Anything that is not a handler is a file out of the vendored corpus — the same bytes every other suite
    /// reads, which is what keeps the vendored-and-byte-verified model intact.
    /// </summary>
    [Test]
    public async Task StaticFilesComeOutOfTheVendoredCorpus()
    {
        using var response = await GetAsync("/fetch/api/resources/top.txt");

        ((int) response.StatusCode).Should().Be(200);
        (await response.Content.ReadAsStringAsync()).Should().Be(WptCorpus.Read("fetch/api/resources/top.txt"));
    }

    /// <summary>A path the corpus does not hold is a 404, not a CLR exception.</summary>
    [Test]
    public async Task APathTheCorpusDoesNotHoldIsA404()
    {
        using var response = await GetAsync("/fetch/api/resources/there-is-no-such-file.txt");

        ((int) response.StatusCode).Should().Be(404);
    }

    /// <summary>
    /// The one thing the server cannot make true however faithfully it echoes: <b>a header value carrying a
    /// byte above ASCII does not survive the .NET HTTP stack</b>, in either direction. Recorded here rather
    /// than left to be discovered from a suite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Fetch Standard's <i>header value</i> is any byte sequence without NUL, LF or CR
    /// (https://fetch.spec.whatwg.org/#header-value), and two vendored files send values a browser carries
    /// unchanged: <c>fetch/api/headers/header-values.any.js</c> sends every byte from 0x01 to 0xFF, and
    /// <c>fetch/api/basic/request-headers-nonascii.any.js</c> sends <c>before-æøå-after</c>. Their rows are
    /// excluded under <c>WptDivergence.NeedsPermissiveHeaderTransport</c>, and this is the evidence that
    /// puts them there rather than in <c>NeedsTriage</c>: the bytes leave <see cref="WptServer"/> intact and
    /// there is no engine anywhere in this test.
    /// </para>
    /// <para>
    /// The measured line is exactly ASCII. Every byte from 0x01 to 0x7F round-trips — control characters
    /// included, which is worth knowing because they are the ones that <i>look</i> most likely to be refused
    /// — and every byte from 0x80 up does not. So the divergence is narrower than "the transport is strict":
    /// it is the request and response header encodings both defaulting to ASCII, which
    /// <c>SocketsHttpHandler</c> exposes as <c>RequestHeaderEncodingSelector</c> and
    /// <c>ResponseHeaderEncodingSelector</c>.
    /// </para>
    /// </remarks>
    [Test]
    public async Task AHeaderValueAboveAsciiDoesNotSurviveTheHttpStack()
    {
        var lost = new List<int>();
        var echoed = new List<int>();

        for (var code = 1; code < 0x100; code++)
        {
            // NUL, LF and CR are the three the standard itself calls invalid; every other byte is a valid
            // header value and is what the two files above send.
            if (code is 0x0A or 0x0D)
            {
                continue;
            }

            var value = "x" + (char) code + "x";
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                WptServer.Instance.Origin + "/fetch/api/resources/inspect-headers.py?headers=x-probe");
            request.Headers.TryAddWithoutValidation("x-probe", value);

            try
            {
                using var response = await _client.SendAsync(request);
                var survived = response.Headers.TryGetValues("x-request-x-probe", out var values)
                    && string.Equals(string.Concat(values), value, StringComparison.Ordinal);
                (survived ? echoed : lost).Add(code);
            }
            catch (HttpRequestException)
            {
                lost.Add(code);
            }
        }

        echoed.Should().BeEquivalentTo(Ascii(), "every byte up to 0x7F survives, control characters included");
        lost.Should().BeEquivalentTo(AboveAscii(), "and every byte from 0x80 up is lost");

        static IEnumerable<int> Ascii()
        {
            for (var code = 1; code < 0x80; code++)
            {
                if (code is not (0x0A or 0x0D))
                {
                    yield return code;
                }
            }
        }

        static IEnumerable<int> AboveAscii()
        {
            for (var code = 0x80; code < 0x100; code++)
            {
                yield return code;
            }
        }
    }

    // ---- the xhr corpus's own handlers ----

    /// <summary>
    /// <c>xhr/resources/content.py</c> answers with the request body, and reports four facts about the
    /// request as headers — each falling back to the literal string <c>NO</c>, which is what the suites
    /// assert against.
    /// </summary>
    [Test]
    public async Task ContentEchoesTheBodyAndReportsTheRequest()
    {
        using var post = new HttpRequestMessage(HttpMethod.Post, WptServer.Instance.Origin + "/xhr/resources/content.py?a=1")
        {
            Content = new StringContent("hello", Encoding.UTF8, "text/x-probe"),
        };

        using var response = await _client.SendAsync(post);

        (await response.Content.ReadAsStringAsync()).Should().Be("hello");
        response.Headers.GetValues("x-request-method").Should().ContainSingle().Which.Should().Be("POST");
        response.Headers.GetValues("x-request-query").Should().ContainSingle().Which.Should().Be("a=1");
        response.Headers.GetValues("x-request-content-length").Should().ContainSingle().Which.Should().Be("5");
        response.Headers.GetValues("x-request-content-type").Should().ContainSingle().Which.Should().Be("text/x-probe; charset=utf-8");

        // A GET with no body, no query and no content type: three of the four report the literal NO rather
        // than being absent, which is upstream's `request.headers.get(name, b"NO")`.
        using var bare = await GetAsync("/xhr/resources/content.py");

        (await bare.Content.ReadAsStringAsync()).Should().BeEmpty();
        bare.Headers.GetValues("x-request-query").Should().ContainSingle().Which.Should().Be("NO");
        bare.Headers.GetValues("x-request-content-length").Should().ContainSingle().Which.Should().Be("NO");
        bare.Headers.GetValues("x-request-content-type").Should().ContainSingle().Which.Should().Be("NO");
    }

    /// <summary>
    /// The <c>content</c> parameter wins over the body, and <c>response_charset_label</c> names the charset
    /// the answer is typed with.
    /// </summary>
    [Test]
    public async Task ContentPrefersTheQueryAndCarriesTheCharsetItWasGiven()
    {
        using var response = await GetAsync("/xhr/resources/content.py?content=hi&response_charset_label=shift_jis");

        // Read as bytes: the charset in that header is one no .NET encoding provider is registered for,
        // which is the whole point of the parameter — the suite that passes it is asking the engine to
        // decode it, not the test client.
        Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()).Should().Be("hi");
        response.Content.Headers.NonValidated.TryGetValues("Content-Type", out var contentType).Should().BeTrue();
        contentType.ToString().Should().Be("text/plain;charset=shift_jis");
    }

    /// <summary>
    /// <c>xhr/resources/delay.py</c> sleeps for <c>ms</c> milliseconds and then answers <c>TEST_DELAY</c>.
    /// </summary>
    [Test]
    public async Task DelayWaitsAndThenAnswers()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        using var response = await GetAsync("/xhr/resources/delay.py?ms=120");

        (await response.Content.ReadAsStringAsync()).Should().Be("TEST_DELAY");

        // A floor rather than a window: what is being asserted is that the handler waits at all.
        System.Diagnostics.Stopwatch.GetElapsedTime(started).Should().BeGreaterThan(TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// <c>xhr/resources/echo-content-type.py</c> answers with the request's own <c>Content-Type</c>, and
    /// <c>echo-headers.py</c> with the header block it read.
    /// </summary>
    [Test]
    public async Task TheTwoEchoHandlersAnswerWithWhatTheRequestCarried()
    {
        using var typed = new HttpRequestMessage(HttpMethod.Post, WptServer.Instance.Origin + "/xhr/resources/echo-content-type.py")
        {
            Content = new StringContent("x", Encoding.UTF8, "application/x-probe"),
        };

        using var typedResponse = await _client.SendAsync(typed);
        (await typedResponse.Content.ReadAsStringAsync()).Should().Be("application/x-probe; charset=utf-8");

        using var headed = new HttpRequestMessage(HttpMethod.Post, WptServer.Instance.Origin + "/xhr/resources/echo-headers.py")
        {
            Content = new StringContent("22 bytes worth of body"),
        };

        using var headedResponse = await _client.SendAsync(headed);
        var block = await headedResponse.Content.ReadAsStringAsync();

        // One `Name: value` per line, which is the shape xhr/request-content-length.any.js reads: it asks
        // whether `Content-Length: 22` is in there.
        block.Should().Contain("Content-Length: 22");
        block.Should().Contain("Host: ");
    }

    /// <summary>
    /// <c>xhr/resources/form.py</c> answers <c>id:&lt;id&gt;;value:&lt;value&gt;;</c> out of the posted form,
    /// whether it arrived urlencoded or as multipart.
    /// </summary>
    [Test]
    public async Task FormReadsBothPostEncodings()
    {
        using var urlencoded = new HttpRequestMessage(HttpMethod.Post, WptServer.Instance.Origin + "/xhr/resources/form.py")
        {
            Content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("id", "1"),
                new KeyValuePair<string, string>("value", "a b"),
            ]),
        };

        using var first = await _client.SendAsync(urlencoded);
        (await first.Content.ReadAsStringAsync()).Should().Be("id:1;value:a b;");

        var multipart = new MultipartFormDataContent("BOUNDARY");
        multipart.Add(new StringContent("2"), "id");
        multipart.Add(new StringContent("b"), "value");

        using var second = new HttpRequestMessage(HttpMethod.Post, WptServer.Instance.Origin + "/xhr/resources/form.py") { Content = multipart };
        using var secondResponse = await _client.SendAsync(second);

        (await secondResponse.Content.ReadAsStringAsync()).Should().Be("id:2;value:b;");
    }

    /// <summary>
    /// <c>fetch/api/resources/bad-chunk-encoding.py</c> writes <c>count</c> well-formed chunks and then the
    /// literal bytes <c>garbage</c>, so a client has to report the body as failed rather than as finished.
    /// </summary>
    [Test]
    public async Task BadChunkEncodingBreaksTheBodyRatherThanEndingIt()
    {
        using var response = await _client.GetAsync(
            WptServer.Instance.Origin + "/fetch/api/resources/bad-chunk-encoding.py?ms=1&count=2",
            HttpCompletionOption.ResponseHeadersRead);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var read = async () => await response.Content.ReadAsStringAsync();
        await read.Should().ThrowAsync<Exception>("the chunk framing stops being valid part-way through");
    }

    /// <summary>
    /// An <c>.asis</c> file is the whole response: the status line and reason phrase are the file's, and so
    /// are the repeated headers a composed response would have combined.
    /// </summary>
    [Test]
    public async Task AnAsisFileIsServedAsTheWholeResponse()
    {
        using var basic = await GetAsync("/xhr/resources/headers-basic.asis");

        ((int) basic.StatusCode).Should().Be(280);
        basic.ReasonPhrase.Should().Be("HELLO");
        basic.Headers.GetValues("foo-test").Should().BeEquivalentTo(["1", "2", "3"]);

        // HTTP/1.0, and a header value holding a vertical tab and a form feed — the two things the line-ending
        // normalization must not touch.
        using var empty = await GetAsync("/xhr/resources/headers-some-are-empty.asis");

        empty.Version.Should().Be(new Version(1, 0));
        empty.Headers.GetValues("heya").Should().Contain("\u000b\u000c");
    }

    /// <summary>
    /// The root answers a non-empty 200, which is what xhr/responsetype.any.js reads before it gets to the
    /// rules it is really about.
    /// </summary>
    [Test]
    public async Task TheRootAnswersSomething()
    {
        using var response = await GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotBeEmpty();
    }
}
#endif
