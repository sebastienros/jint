#if NET8_0_OR_GREATER
#nullable enable

using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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

    // ================================================================ the static-file half

    /// <summary>
    /// <c>handlers.py</c>'s <c>guess_content_type</c> over <c>constants.py</c>'s <c>content_types</c>, one
    /// case per extension the table names.
    /// </summary>
    /// <remarks>
    /// The whole table, not the part the corpus exercises. Only a handful of these extensions are vendored —
    /// nothing binary is, because <c>WptCorpus</c> hands every file back as a string — so the rows that
    /// cannot be served are here precisely so that a corpus bump which changes one of them fails against
    /// upstream's source rather than going unnoticed until a lane needs it.
    /// </remarks>
    [TestCase("x.json", "application/json")]
    [TestCase("x.wasm", "application/wasm")]
    [TestCase("x.xht", "application/xhtml+xml")]
    [TestCase("x.xhtm", "application/xhtml+xml")]
    [TestCase("x.xhtml", "application/xhtml+xml")]
    [TestCase("x.xml", "application/xml")]
    [TestCase("x.xpi", "application/x-xpinstall")]
    [TestCase("x.m4a", "audio/mp4")]
    [TestCase("x.mp3", "audio/mpeg")]
    [TestCase("x.oga", "audio/ogg")]
    [TestCase("x.weba", "audio/webm")]
    [TestCase("x.wav", "audio/x-wav")]
    [TestCase("x.avif", "image/avif")]
    [TestCase("x.bmp", "image/bmp")]
    [TestCase("x.gif", "image/gif")]
    [TestCase("x.jpg", "image/jpeg")]
    [TestCase("x.jpeg", "image/jpeg")]
    [TestCase("x.jxl", "image/jxl")]
    [TestCase("x.png", "image/png")]
    [TestCase("x.svg", "image/svg+xml")]
    [TestCase("x.manifest", "text/cache-manifest")]
    [TestCase("x.css", "text/css")]
    [TestCase("x.event_stream", "text/event-stream")]
    [TestCase("x.htm", "text/html")]
    [TestCase("x.html", "text/html")]
    [TestCase("x.js", "text/javascript")]
    [TestCase("x.mjs", "text/javascript")]
    [TestCase("x.txt", "text/plain")]
    [TestCase("x.md", "text/plain")]
    [TestCase("x.vtt", "text/vtt")]
    [TestCase("x.mp4", "video/mp4")]
    [TestCase("x.m4v", "video/mp4")]
    [TestCase("x.webm", "video/webm")]
    // The fallback, and the two rules `os.path.splitext` and a dictionary lookup impose on top of the table:
    // the last extension only, and no case folding.
    [TestCase("x.asis", "application/octet-stream")]
    [TestCase("x.headers", "application/octet-stream")]
    [TestCase("x", "application/octet-stream")]
    [TestCase("a/b.c/x", "application/octet-stream")]
    [TestCase("x.HTML", "application/octet-stream")]
    [TestCase("get-host-info.sub.js", "text/javascript")]
    [TestCase("x.tar.gz", "application/octet-stream")]
    public void TheContentTypeIsTheOneWptservesTableNames(string path, string expected)
        => WptServerFiles.GuessContentType(path).Should().Be(expected);

    /// <summary>
    /// And a real file really is served with it. wptserve adds <b>no</b> charset here: nothing in
    /// <c>tools/wptserve/</c> appends one to a guessed type, so a <c>.js</c> is <c>text/javascript</c> flat
    /// and the <c>charset=utf-8</c> a browser sees on the harness comes from a <c>.headers</c> sidecar
    /// instead — see <see cref="AHeadersSidecarReplacesTheGuessedContentType"/>.
    /// </summary>
    [TestCase("/common/utils.js", "text/javascript")]
    [TestCase("/common/blank.html", "text/html")]
    [TestCase("/common/dummy.xml", "application/xml")]
    [TestCase("/common/dummy.xhtml", "application/xhtml+xml")]
    [TestCase("/fetch/api/resources/top.txt", "text/plain")]
    [TestCase("/url/resources/urltestdata.json", "application/json")]
    public async Task AStaticFileCarriesTheTypeItsExtensionNames(string path, string expected)
    {
        using var response = await GetAsync(path);

        ((int) response.StatusCode).Should().Be(200);
        response.Content.Headers.ContentType!.ToString().Should().Be(expected);
    }

    /// <summary>
    /// <c>handlers.py</c>'s <c>load_headers</c>: a file's <c>.headers</c> sidecar is applied to the response,
    /// and a <c>Content-Type</c> in it replaces the guess rather than joining it.
    /// </summary>
    /// <remarks>
    /// <c>resources/testharness.js.headers</c> is upstream's own, vendored beside the file it belongs to, and
    /// it is two lines: the content type <i>with</i> a charset, and a cache directive. It is the answer to
    /// "where does wptserve add <c>charset=utf-8</c>" — in a sidecar, for six files, and nowhere else.
    /// </remarks>
    [Test]
    public async Task AHeadersSidecarReplacesTheGuessedContentType()
    {
        using var response = await GetAsync("/resources/testharness.js");

        ((int) response.StatusCode).Should().Be(200);
        response.Content.Headers.ContentType!.ToString().Should().Be("text/javascript; charset=utf-8");
        response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromHours(1));
        (await response.Content.ReadAsStringAsync()).Should().Be(WptCorpus.Read("resources/testharness.js"));
    }

    /// <summary>
    /// The four rules <c>load_headers</c> and <c>get_headers</c> impose between them, checked against a
    /// synthetic tree because the vendored one has one sidecar shape and there are four.
    /// </summary>
    /// <remarks>
    /// In order: the directory's <c>__dir__.headers</c> comes before the file's own; a repeated name is kept
    /// rather than overwritten, and takes the position of its first appearance; a <c>Content-Type</c> in
    /// either sidecar suppresses the guess, which is otherwise inserted <i>first</i>; and a
    /// <c>.sub.headers</c> wins over a plain <c>.headers</c> of the same base name and is substituted with
    /// escaping off.
    /// </remarks>
    [Test]
    public void SidecarHeadersAreAppliedInWptservesOrder()
    {
        var context = new WptSubstitutionContext(8080, null);

        // The guess goes in front of whatever the sidecars said, and __dir__ comes before the file.
        Load("a/b.txt", context, new Dictionary<string, string>
        {
            ["a/__dir__.headers"] = "X-Dir: one\n",
            ["a/b.txt.headers"] = "X-File: two\n",
        }).Should().Equal(("Content-Type", "text/plain"), ("X-Dir", "one"), ("X-File", "two"));

        // A content type anywhere in the sidecars suppresses the guess entirely — even in __dir__, and even
        // spelled in another case, because `get_headers` compares lowered.
        Load("a/b.txt", context, new Dictionary<string, string>
        {
            ["a/__dir__.headers"] = "content-type: text/x-dir\n",
            ["a/b.txt.headers"] = "X-File: two\n",
        }).Should().Equal(("content-type", "text/x-dir"), ("X-File", "two"));

        // A repeated name is two headers, and they sit where the first one did.
        Load("a/b.txt", context, new Dictionary<string, string>
        {
            ["a/b.txt.headers"] = "Set-Cookie: a=1\nX-Other: x\nSet-Cookie: b=2\n",
        }).Should().Equal(
            ("Content-Type", "text/plain"),
            ("Set-Cookie", "a=1"),
            ("Set-Cookie", "b=2"),
            ("X-Other", "x"));

        // The .sub. spelling wins outright over the plain one, and its values are substituted.
        Load("a/b.txt", context, new Dictionary<string, string>
        {
            ["a/b.txt.headers"] = "X-Which: plain\n",
            ["a/b.txt.sub.headers"] = "X-Which: {{host}}:{{ports[http][0]}}\n",
        }).Should().Equal(("Content-Type", "text/plain"), ("X-Which", "127.0.0.1:8080"));

        // Content-Length, Connection and Transfer-Encoding are this server's framing rather than the file's,
        // so a sidecar naming one is dropped: two Content-Lengths on the wire is a client reading the wrong
        // body length. A file whose subject *is* a wrong framing uses .asis, which bypasses all of this.
        Load("a/b.txt", context, new Dictionary<string, string>
        {
            ["a/b.txt.headers"] = "Content-Length: 99\nConnection: keep-alive\nTransfer-Encoding: chunked\nX-Kept: yes\n",
        }).Should().Equal(("Content-Type", "text/plain"), ("X-Kept", "yes"));

        // Blank lines are skipped, values are stripped, and CRLF is a line ending like any other.
        Load("a/b.txt", context, new Dictionary<string, string>
        {
            ["a/b.txt.headers"] = "\r\nX-Space:   padded  \r\n\r\n",
        }).Should().Equal(("Content-Type", "text/plain"), ("X-Space", "padded"));

        static List<(string Name, string Value)> Load(
            string path,
            in WptSubstitutionContext context,
            Dictionary<string, string> tree)
            => WptServerFiles.LoadHeaders(path, context, name => tree.GetValueOrDefault(name));
    }

    /// <summary>
    /// <c>pipes.py</c>'s <c>ReplacementTokenizer</c>: four patterns tried in order at each position, and a
    /// <b>stop</b> — not a skip — at the first character none of them match.
    /// </summary>
    /// <remarks>
    /// The stop is the half a reimplementation gets wrong. <c>{{ host }}</c> tokenizes to nothing upstream
    /// and is a 500; a tokenizer that skipped the spaces would accept a spelling wptserve rejects, and a
    /// corpus file written against it would then only work here.
    /// </remarks>
    [Test]
    public void TheTemplateGrammarIsTheOneUpstreamsScannerAccepts()
    {
        Kinds("host").Should().Equal("Identifier:host");
        Kinds("ports[http][0]").Should().Equal("Identifier:ports", "Index:http", "Index:0");
        Kinds("domains[]").Should().Equal("Identifier:domains", "Index:");
        Kinds("uuid()").Should().Equal("Identifier:uuid", "Arguments:");
        Kinds("header_or_default(X-Test, fallback)")
            .Should().Equal("Identifier:header_or_default", "Arguments:X-Test, fallback");

        // The $ stays in both the assignment's name and a later reference's, which is the only reason the
        // two ever meet.
        Kinds("$id:uuid()").Should().Equal("Variable:$id", "Identifier:uuid", "Arguments:");
        Kinds("$id").Should().Equal("Identifier:$id");

        // And the scanner stops rather than skipping: a space, a stray brace, an unterminated index.
        Kinds(" host").Should().BeEmpty();
        Kinds("host ports").Should().Equal("Identifier:host");
        Kinds("ports[http").Should().Equal("Identifier:ports");

        // `re.split(r",\s*", …)`: the whitespace after a comma is consumed and the whitespace before it is
        // not, which is a difference a Trim() would erase.
        Arguments("(a, b)").Should().Equal("a", "b");
        Arguments("(a ,b)").Should().Equal("a ", "b");
        Arguments("()").Should().BeEmpty();

        static List<string> Kinds(string body)
        {
            var kinds = new List<string>();
            foreach (var token in WptServerFiles.Tokenize(body))
            {
                kinds.Add(token.Kind + ":" + token.Text);
            }

            return kinds;
        }

        static string[] Arguments(string body) => WptServerFiles.Tokenize(body)[0].Arguments!;
    }

    /// <summary>
    /// Every substitution the corpus spells, as a <c>.sub.html</c> gets it — one case per token, which is
    /// what the browser lane's files are made of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The values say what this server is: one loopback host and one port, so <c>{{host}}</c>,
    /// <c>{{domains[www]}}</c> and <c>{{hosts[alt][www2]}}</c> all answer <c>127.0.0.1</c>, and every
    /// protocol and index of <c>{{ports[…][…]}}</c> answers the one port <see cref="WptServer"/> bound.
    /// <c>{{ports[https][0]}}</c> included: there is no TLS here, so a file that composes an
    /// <c>https://</c> origin out of it composes one that cannot be reached — which is the honest answer,
    /// and a reason such a file is un-runnable rather than merely different.
    /// </para>
    /// <para>
    /// <c>{{GET[…]}}</c> is the one lookup that answers the empty string for something absent instead of
    /// failing; <c>FirstWrapper.__getitem__</c> catches its own <c>KeyError</c>, and nothing else here does.
    /// </para>
    /// </remarks>
    [TestCase("{{host}}", "127.0.0.1")]
    [TestCase("{{domains[www]}}", "127.0.0.1")]
    [TestCase("{{domains[]}}", "127.0.0.1")]
    [TestCase("{{domains[www2]}}", "127.0.0.1")]
    [TestCase("{{hosts[alt][]}}", "127.0.0.1")]
    [TestCase("{{hosts[alt][www2]}}", "127.0.0.1")]
    [TestCase("{{ports[http][0]}}", "8080")]
    [TestCase("{{ports[http][1]}}", "8080")]
    [TestCase("{{ports[https][0]}}", "8080")]
    [TestCase("{{ports[ws][0]}}", "8080")]
    [TestCase("{{url_base}}", "/")]
    [TestCase("{{location[server]}}", "http://127.0.0.1:8080")]
    [TestCase("{{location[scheme]}}", "http")]
    [TestCase("{{location[host]}}", "127.0.0.1:8080")]
    [TestCase("{{location[hostname]}}", "127.0.0.1")]
    [TestCase("{{location[port]}}", "8080")]
    [TestCase("{{location[path]}}", "/a/page.sub.html")]
    [TestCase("{{location[pathname]}}", "/a/page.sub.html")]
    // Escaped, because these cases are a .sub.html's: the "&" between two query parameters is a "&"
    // in the source and "&amp;" in the markup, and upstream escapes it for exactly the same reason.
    [TestCase("{{location[query]}}", "?name=v%20alue&amp;empty=")]
    [TestCase("{{GET[name]}}", "v alue")]
    [TestCase("{{GET[empty]}}", "")]
    [TestCase("{{GET[absent]}}", "")]
    [TestCase("{{headers[x-probe]}}", "probed")]
    [TestCase("{{header_or_default(x-probe, fallback)}}", "probed")]
    [TestCase("{{header_or_default(x-absent, fallback)}}", "fallback")]
    // Text either side of a token is untouched, and two tokens in one line both resolve.
    [TestCase("http://{{host}}:{{ports[http][0]}}/x", "http://127.0.0.1:8080/x")]
    public async Task EverySubstitutionTokenTheCorpusUsesResolves(string template, string expected)
    {
        var request = await RequestAsync("GET /a/page.sub.html?name=v%20alue&empty= HTTP/1.1\r\nx-probe: probed\r\n\r\n");

        WptServerFiles.Substitute(new WptSubstitutionContext(8080, request), template, escapeAsHtml: true)
            .Should().Be(expected);
    }

    /// <summary>
    /// <c>wrap_pipeline</c> picks the escaping from the extension: markup gets
    /// <c>html.escape(…, quote=True)</c> and everything else — a <c>.sub.js</c>, a <c>.sub.headers</c> — gets
    /// none.
    /// </summary>
    /// <remarks>
    /// Python's escape is five characters and stops there. <c>WebUtility.HtmlEncode</c> would also turn every
    /// non-ASCII character into a numeric entity, so a substituted value carrying one would differ from what
    /// a browser running the real server sees — which is why this is written by hand.
    /// </remarks>
    [Test]
    public async Task AMarkupFileEscapesWhatItSubstitutesAndAScriptDoesNot()
    {
        var request = await RequestAsync("GET /a/page.sub.html?v=%3Cb%26%22%27%C3%A5 HTTP/1.1\r\n\r\n");
        var context = new WptSubstitutionContext(8080, request);

        WptServerFiles.Substitute(context, "{{GET[v]}}", escapeAsHtml: true)
            .Should().Be("&lt;b&amp;&quot;&#x27;Ã¥", "the five characters and no more");
        WptServerFiles.Substitute(context, "{{GET[v]}}", escapeAsHtml: false)
            .Should().Be("<b&\"'Ã¥");

        WptServerFiles.WantsSubstitution("a/page.sub.html").Should().BeTrue();
        WptServerFiles.WantsSubstitution("a/page.html").Should().BeFalse();
        WptServerFiles.EscapesAsHtml("a/page.sub.html").Should().BeTrue();
        WptServerFiles.EscapesAsHtml("a/page.sub.xhtml").Should().BeTrue();
        WptServerFiles.EscapesAsHtml("a/page.sub.svg").Should().BeTrue();
        WptServerFiles.EscapesAsHtml("a/page.sub.xml").Should().BeTrue();
        WptServerFiles.EscapesAsHtml("a/page.sub.js").Should().BeFalse();
    }

    /// <summary>
    /// <c>{{uuid()}}</c> is fresh per call, and <c>{{$name:…}}</c> is how a file uses the same one twice.
    /// </summary>
    [Test]
    public async Task AUuidIsFreshAndAVariableRemembersOne()
    {
        var request = await RequestAsync("GET /a/page.sub.js HTTP/1.1\r\n\r\n");
        var context = new WptSubstitutionContext(8080, request);

        var pair = WptServerFiles.Substitute(context, "{{uuid()}}|{{uuid()}}", escapeAsHtml: false).Split('|');
        pair[0].Should().NotBe(pair[1]);
        Guid.TryParse(pair[0], out _).Should().BeTrue();

        var remembered = WptServerFiles.Substitute(context, "{{$id:uuid()}}|{{$id}}", escapeAsHtml: false).Split('|');
        remembered[0].Should().Be(remembered[1]);
    }

    /// <summary>
    /// A substitution this server cannot make is a failure and never a placeholder left in place.
    /// </summary>
    /// <remarks>
    /// Upstream raises and wptserve answers 500. The alternative — serving <c>{{file_hash(md5, x)}}</c>
    /// verbatim — puts the failure in the test's assertions, several layers from the cause, which is exactly
    /// how the corpus's own unrunnable files used to be discovered.
    /// </remarks>
    [TestCase("{{nonesuch}}", "undefined template variable")]
    [TestCase("{{file_hash(md5, dom/interfaces.html)}}", "undefined template variable")]
    [TestCase("{{fs_path(x)}}", "undefined template variable")]
    [TestCase("{{ host }}", "nothing to substitute")]
    [TestCase("{{host[0]}}", "unexpected trailing token")]
    [TestCase("{{ports[http]}}", "expected an index")]
    [TestCase("{{location[nonesuch]}}", "not a part of the request URL")]
    [TestCase("{{headers[x-absent]}}", "has no \"x-absent\" header")]
    [TestCase("{{uuid}}", "expected a call")]
    [TestCase("{{header_or_default(only-one)}}", "expected 2 arguments")]
    public async Task AnUnresolvableSubstitutionFailsLoudly(string template, string expected)
    {
        var request = await RequestAsync("GET /a/page.sub.html HTTP/1.1\r\n\r\n");

        Invoking(() => WptServerFiles.Substitute(new WptSubstitutionContext(8080, request), template, escapeAsHtml: true))
            .Should().Throw<WptServerFileException>()
            .WithMessage("*" + expected + "*");
    }

    /// <summary>
    /// And the vendored <c>.sub.</c> file really is substituted on its way out: 68 files of the browser
    /// lane's suites include <c>common/get-host-info.sub.js</c>, and it is the reason the template language
    /// is here at all.
    /// </summary>
    [Test]
    public async Task TheVendoredSubstitutionFileIsServedSubstituted()
    {
        using var response = await GetAsync("/common/get-host-info.sub.js");

        ((int) response.StatusCode).Should().Be(200);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("{{", "every placeholder is resolved before the file is written");
        body.Should().Contain("var ORIGINAL_HOST = '127.0.0.1';");
        body.Should().Contain(
            "var HTTP_PORT = '" + WptServer.Instance.Port.ToString(CultureInfo.InvariantCulture) + "';");

        // Escaping is off for a .sub.js, so the quotes the file writes around each value stay quotes.
        body.Should().NotContain("&#x27;");
    }

    /// <summary>
    /// A <c>?pipe=</c> this server does not implement is a 500 that names itself, not a file served as if
    /// the query were not there.
    /// </summary>
    /// <remarks>
    /// wptserve has fourteen pipes. Only <c>sub</c> is ported, because only <c>sub</c> is needed to load an
    /// <c>.html</c> test — but a file asking for <c>?pipe=status(404)</c> and getting a 200 would fail an
    /// assertion about the engine, and one asking for <c>?pipe=trickle(d1)</c> is why
    /// <c>xhr/abort-after-timeout.any.js</c> is a not-vendored row. Both should say so.
    /// </remarks>
    [Test]
    public async Task AnUnimplementedPipeIsA500ThatNamesIt()
    {
        using var response = await GetAsync("/common/blank.html?pipe=trickle(d1)");

        ((int) response.StatusCode).Should().Be(500);
        (await response.Content.ReadAsStringAsync()).Should().Contain("trickle");

        using var supported = await GetAsync("/common/utils.js?pipe=sub(none)");
        ((int) supported.StatusCode).Should().Be(200);
    }

    /// <summary>
    /// The query and the fragment take no part in finding the file, and a percent-escape in the path is
    /// decoded before the lookup — all three of which the browser lane needs and none of which the
    /// <c>.any.js</c> lane ever asked for.
    /// </summary>
    /// <remarks>
    /// A fragment never reaches a real server, so the one here is written straight onto the socket:
    /// <c>HttpClient</c> strips it, which is the correct behaviour and the reason it cannot be the thing
    /// under test.
    /// </remarks>
    [Test]
    public async Task AQueryAFragmentAndAPercentEscapeAllResolveToTheSameFile()
    {
        var expected = WptCorpus.Read("common/utils.js");

        using var withQuery = await GetAsync("/common/utils.js?some=thing");
        (await withQuery.Content.ReadAsStringAsync()).Should().Be(expected);

        using var withEscape = await GetAsync("/common/utils%2Ejs");
        (await withEscape.Content.ReadAsStringAsync()).Should().Be(expected);

        var raw = await RawAsync("GET /common/utils.js?some=thing#fragment HTTP/1.1\r\nhost: x\r\n\r\n");
        raw.Should().StartWith("HTTP/1.1 200 OK");
        raw.Should().Contain(expected);
    }

    /// <summary>
    /// <c>HEAD</c> of a static file is the headers a <c>GET</c> would have carried, <c>Content-Length</c>
    /// included, and no body.
    /// </summary>
    [Test]
    public async Task HeadOfAStaticFileIsItsHeadersWithNoBody()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, WptServer.Instance.Origin + "/resources/testharness.js");
        using var response = await _client.SendAsync(request);

        ((int) response.StatusCode).Should().Be(200);
        response.Content.Headers.ContentType!.ToString().Should().Be("text/javascript; charset=utf-8");
        response.Content.Headers.ContentLength
            .Should().Be(Encoding.UTF8.GetByteCount(WptCorpus.Read("resources/testharness.js")));
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// A path the corpus does not hold is a 404 whatever it looks like, with the status line and the content
    /// type every other answer here has.
    /// </summary>
    [TestCase("/dom/nodes/there-is-no-such-file.html")]
    [TestCase("/resources/testharness.css")]
    [TestCase("/common/nothing.sub.html")]
    public async Task APathTheCorpusDoesNotHoldIsA404WhateverItsExtension(string path)
    {
        using var response = await GetAsync(path);

        ((int) response.StatusCode).Should().Be(404);
        response.ReasonPhrase.Should().Be("Not Found");
        response.Content.Headers.ContentType!.ToString().Should().Be("text/plain");
        response.Content.Headers.ContentLength.Should().Be(0);
    }

    /// <summary>
    /// <c>/resources/testharnessreport.js</c> comes from <c>Prelude/</c> rather than from <c>Vendor/</c>, and
    /// a server can be given an overlay to answer with instead.
    /// </summary>
    /// <remarks>
    /// Upstream's file is a stub that exists to be replaced — "intended for vendors to implement code needed
    /// to integrate testharness.js tests with their own test systems" — so vendoring it would put bytes in
    /// the tree the server never sends. The overlay is the slot the browser lane fills with the script that
    /// posts a page's results back to the driver; nothing supplies one yet, which is why this test starts
    /// its own server.
    /// </remarks>
    [Test]
    public async Task TheHarnessReportComesFromThePreludeAndCanBeOverlaid()
    {
        using var response = await GetAsync("/resources/testharnessreport.js");

        ((int) response.StatusCode).Should().Be(200);
        response.Content.Headers.ContentType!.ToString().Should().Be("text/javascript; charset=utf-8");
        (await response.Content.ReadAsStringAsync()).Should().Be(WptCorpus.HarnessReport);

        // It is Jint's file, and it says so; and it is not in the vendored tree at all.
        WptCorpus.HarnessReport.Should().Contain("Jint's `resources/testharnessreport.js`");
        WptCorpus.Contains("resources/testharnessreport.js").Should().BeFalse();

        using var overlaid = new WptServer("window.__jintReport = true;");
        using var fromOverlay = await _client.GetAsync(overlaid.Origin + "/resources/testharnessreport.js");
        (await fromOverlay.Content.ReadAsStringAsync()).Should().Be("window.__jintReport = true;");

        // And the overlay is per server: the shared one is untouched.
        using var again = await GetAsync("/resources/testharnessreport.js");
        (await again.Content.ReadAsStringAsync()).Should().Be(WptCorpus.HarnessReport);
    }

    /// <summary>
    /// Parses one request off a raw byte stream, which is how the substitution cases get a
    /// <see cref="WptServerRequest"/> to resolve <c>{{GET[…]}}</c> and <c>{{location[…]}}</c> against.
    /// </summary>
    private static async Task<WptServerRequest> RequestAsync(string raw)
    {
        using var stream = new MemoryStream(Encoding.Latin1.GetBytes(raw));
        return (await WptServerRequest.ReadAsync(stream, CancellationToken.None))!;
    }

    /// <summary>
    /// Writes a request onto the socket verbatim and reads the whole answer back, for the shapes an
    /// <see cref="HttpClient"/> corrects on the way out.
    /// </summary>
    private static async Task<string> RawAsync(string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, WptServer.Instance.Port);
        await using var stream = client.GetStream();

        var bytes = Encoding.Latin1.GetBytes(request);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();

        using var answer = new MemoryStream();
        await stream.CopyToAsync(answer);
        return Encoding.UTF8.GetString(answer.ToArray());
    }
}
#endif
