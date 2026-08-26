#if NET8_0_OR_GREATER
#nullable enable

using System.Text;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>multipart/form-data</c> bodies: the HTML Standard's
/// <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#multipart/form-data-encoding-algorithm">encoding
/// algorithm</see> that https://fetch.spec.whatwg.org/#concept-bodyinit-extract runs for a <c>FormData</c>
/// body, and the parser behind https://fetch.spec.whatwg.org/#dom-body-formdata.
/// </summary>
/// <remarks>
/// The serialization is asserted <b>byte for byte</b> against a hand-written expectation rather than only
/// through a round trip: a writer and a reader that agree with each other and with nothing else would pass
/// every round-trip test and still produce a body no server can read.
/// <para>
/// The sibling arm of the same algorithm, <c>application/x-www-form-urlencoded</c>, is in
/// <see cref="UrlEncodedBodyTests"/>.
/// </para>
/// </remarks>
public class MultipartTests
{
    private const string TypePrefix = "multipart/form-data; boundary=";

    private static Engine WebEngine() => new(options => options.UseFetch());

    /// <summary>
    /// The body of <paramref name="expression"/>, one character per byte, so an expectation can be written as
    /// text and compared exactly. Note that this consumes the body.
    /// </summary>
    private static string Payload(Engine engine, string expression)
    {
        return engine.Evaluate($"({expression}).arrayBuffer().then(b => Array.from(new Uint8Array(b), c => String.fromCharCode(c)).join(''))")
            .UnwrapIfPromise()
            .AsString();
    }

    /// <summary>The same one-character-per-byte view of a UTF-8 encoded expectation.</summary>
    private static string Bytes(string text) => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(text));

    private static string Boundary(Engine engine, string expression)
    {
        var contentType = engine.Evaluate($"({expression}).headers.get('content-type')").AsString();
        contentType.Should().StartWith(TypePrefix);
        return contentType.Substring(TypePrefix.Length);
    }

    /// <summary>
    /// A JavaScript expression evaluating to the <c>formData()</c> promise of a response carrying
    /// <paramref name="body"/> verbatim under a <c>multipart/form-data</c> type with boundary <c>b</c>.
    /// </summary>
    private static string ParseExpression(string body)
    {
        var escaped = body
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

        return $"new Response('{escaped}', {{ headers: {{ 'content-type': 'multipart/form-data; boundary=b' }} }}).formData()";
    }

    [Test]
    public void SerializesEveryEntryShapeExactly()
    {
        var engine = WebEngine();
        engine.Execute(@"
            var fd = new FormData();
            fd.append('plain', 'value');
            fd.append('file', new File(['hello'], 'note.txt', { type: 'text/plain' }));
            fd.append('blob', new Blob(['xy']));
            var r = new Request('https://example.org/', { method: 'POST', body: fd });");

        var boundary = Boundary(engine, "r");

        // A non-file field carries no Content-Type — the one thing the HTML algorithm says about a part's
        // headers beyond the Content-Disposition — while a file field carries the File's type, or
        // application/octet-stream when it claims none. A bare Blob became a File named "blob" when FormData
        // took it, so it has a filename like any other file part.
        Payload(engine, "r").Should().Be(Bytes(
            $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"plain\"\r\n"
            + "\r\n"
            + "value\r\n"
            + $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"file\"; filename=\"note.txt\"\r\n"
            + "Content-Type: text/plain\r\n"
            + "\r\n"
            + "hello\r\n"
            + $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"blob\"; filename=\"blob\"\r\n"
            + "Content-Type: application/octet-stream\r\n"
            + "\r\n"
            + "xy\r\n"
            + $"--{boundary}--\r\n"));
    }

    [Test]
    public void AnEmptyFormDataIsJustTheCloseDelimiter()
    {
        var engine = WebEngine();
        engine.Execute("var fd = new FormData(); var r = new Response(fd); var again = new Response(fd);");

        var boundary = Boundary(engine, "r");
        Payload(engine, "r").Should().Be($"--{boundary}--\r\n");

        // And it reads back as an entry list with nothing in it.
        engine.Evaluate("again.formData().then(fd => [...fd].length)").UnwrapIfPromise().AsNumber().Should().Be(0);
    }

    [Test]
    public void NeutralizesCarriageReturnsQuotesAndLineFeedsInNames()
    {
        // The security pin: a name and a filename are the only script-controlled text that reaches a header
        // line, so an unescaped CRLF there would let a script append headers — or a whole extra part — to the
        // body a server ends up parsing.
        var engine = WebEngine();
        engine.Execute(@"
            var fd = new FormData();
            fd.append('a""b\r\nX-Injected: 1', 'v');
            fd.append('f', new File([''], 'q"".txt\r\nContent-Type: text/html'));
            var r = new Response(fd);
            var again = new Response(fd);");

        var boundary = Boundary(engine, "r");
        var payload = Payload(engine, "r");

        payload.Should().Be(Bytes(
            $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"a%22b%0D%0AX-Injected: 1\"\r\n"
            + "\r\n"
            + "v\r\n"
            + $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"f\"; filename=\"q%22.txt%0D%0AContent-Type: text/html\"\r\n"
            + "Content-Type: application/octet-stream\r\n"
            + "\r\n"
            + "\r\n"
            + $"--{boundary}--\r\n"));

        // Said plainly: the injected text never begins a line.
        payload.Should().NotContain("\r\nX-Injected");
        payload.Should().NotContain("\r\nContent-Type: text/html");

        // And it survives the round trip as the text the script wrote.
        engine.Evaluate("again.formData().then(fd => [...fd.keys()].join('|'))").UnwrapIfPromise()
            .AsString().Should().Be("a\"b\r\nX-Injected: 1|f");
    }

    [Test]
    public void NormalizesLoneNewlinesInNamesAndValuesButNotInFilenames()
    {
        // "Replace every occurrence of CR not followed by LF, and every occurrence of LF not preceded by CR,
        // in entry's name" — and in a non-file value. A filename is only converted to a scalar value string,
        // so its lone LF stays one and is escaped as %0A rather than %0D%0A.
        var engine = WebEngine();
        engine.Execute(@"
            var fd = new FormData();
            fd.append('n\nn\rn\r\nn', 'v\nv');
            fd.append('f', new File([''], 'a\nb'));
            var r = new Response(fd);");

        var boundary = Boundary(engine, "r");

        Payload(engine, "r").Should().Be(Bytes(
            $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"n%0D%0An%0D%0An%0D%0An\"\r\n"
            + "\r\n"
            + "v\r\nv\r\n"
            + $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"f\"; filename=\"a%0Ab\"\r\n"
            + "Content-Type: application/octet-stream\r\n"
            + "\r\n"
            + "\r\n"
            + $"--{boundary}--\r\n"));
    }

    [Test]
    public void EncodesNamesAndValuesAsUtf8()
    {
        var engine = WebEngine();
        engine.Execute(@"
            var fd = new FormData();
            fd.append('ключ', 'значение');
            fd.append('f', new File(['日本'], 'ファイル.txt', { type: 'text/plain' }));
            var r = new Response(fd);
            var again = new Response(fd);");

        var boundary = Boundary(engine, "r");

        Payload(engine, "r").Should().Be(Bytes(
            $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"ключ\"\r\n"
            + "\r\n"
            + "значение\r\n"
            + $"--{boundary}\r\n"
            + "Content-Disposition: form-data; name=\"f\"; filename=\"ファイル.txt\"\r\n"
            + "Content-Type: text/plain\r\n"
            + "\r\n"
            + "日本\r\n"
            + $"--{boundary}--\r\n"));

        // The names come back as the script wrote them.
        engine.Evaluate("again.formData().then(fd => fd.get('ключ'))").UnwrapIfPromise().AsString().Should().Be("значение");
    }

    [Test]
    public void GeneratesAFreshWellFormedBoundaryEveryTime()
    {
        var engine = WebEngine();
        engine.Execute("function body() { return new Response(new FormData()); }");

        var first = Boundary(engine, "body()");
        var second = Boundary(engine, "body()");

        first.Should().NotBe(second);

        foreach (var boundary in new[] { first, second })
        {
            // RFC 2046 section 5.1.1 allows 1 to 70 characters; the parsing draft asks for at least 27 and at
            // least 95 bits of randomness, which 24 characters out of 62 comfortably clears.
            boundary.Length.Should().Be(44);
            boundary.Should().StartWith("----JintFormBoundary");

            foreach (var c in boundary.Substring("----JintFormBoundary".Length))
            {
                char.IsAsciiLetterOrDigit(c).Should().BeTrue();
            }
        }
    }

    [Test]
    public void ReadsBackWhatItWrote()
    {
        var engine = WebEngine();
        engine.Execute(@"
            var fd = new FormData();
            fd.append('a', '1');
            fd.append('a', '2');
            fd.append('doc', new File(['hello'], 'note.txt', { type: 'text/csv' }));
            var parsed = new Response(fd).formData();");

        engine.Evaluate("parsed.then(fd => [...fd.keys()].join(','))").UnwrapIfPromise().AsString().Should().Be("a,a,doc");
        engine.Evaluate("parsed.then(fd => fd.getAll('a').join(','))").UnwrapIfPromise().AsString().Should().Be("1,2");

        // A part with a filename is a File, whatever its Content-Type says.
        engine.Evaluate("parsed.then(fd => fd.get('doc') instanceof File)").UnwrapIfPromise().AsBoolean().Should().BeTrue();
        engine.Evaluate("parsed.then(fd => fd.get('doc').name)").UnwrapIfPromise().AsString().Should().Be("note.txt");
        engine.Evaluate("parsed.then(fd => fd.get('doc').type)").UnwrapIfPromise().AsString().Should().Be("text/csv");
        engine.Evaluate("parsed.then(fd => fd.get('doc').text())").UnwrapIfPromise().AsString().Should().Be("hello");

        // The FormData it answers with is a real one of this realm.
        engine.Evaluate("parsed.then(fd => fd instanceof FormData)").UnwrapIfPromise().AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ARequestReadsItsOwnBodyBack()
    {
        var engine = WebEngine();
        engine.Execute(@"
            var fd = new FormData();
            fd.append('a', '1');
            var r = new Request('https://example.org/', { method: 'POST', body: fd });");

        engine.Evaluate("r.formData().then(fd => fd.get('a'))").UnwrapIfPromise().AsString().Should().Be("1");

        // The body mixin's disturbed flag covers formData() like every other consumer.
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.formData().then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");
    }

    [Test]
    public void TheFilenameParameterAloneDecidesFileOrString()
    {
        // "Each part whose Content-Disposition header does not contain a filename parameter must be parsed
        // into an entry whose value is the UTF-8 decoded without BOM content of the part. This is done
        // regardless of the presence or the value of a Content-Type header."
        var engine = WebEngine();
        engine.Execute("var parsed = " + ParseExpression(
            "--b\r\n"
            + "Content-Disposition: form-data; name=\"typed\"\r\n"
            + "Content-Type: application/json\r\n"
            + "\r\n"
            + "{}\r\n"
            + "--b\r\n"
            + "Content-Disposition: form-data; name=\"untyped\"; filename=\"f.bin\"\r\n"
            + "\r\n"
            + "raw\r\n"
            + "--b--\r\n") + ";");

        engine.Evaluate("parsed.then(fd => typeof fd.get('typed'))").UnwrapIfPromise().AsString().Should().Be("string");
        engine.Evaluate("parsed.then(fd => fd.get('untyped') instanceof File)").UnwrapIfPromise().AsBoolean().Should().BeTrue();

        // RFC 7578 section 4.4: a part's Content-Type "defaults to text/plain".
        engine.Evaluate("parsed.then(fd => fd.get('untyped').type)").UnwrapIfPromise().AsString().Should().Be("text/plain");
        engine.Evaluate("parsed.then(fd => fd.get('untyped').text())").UnwrapIfPromise().AsString().Should().Be("raw");
    }

    [Test]
    public void ParsesAPartNamedCharsetLikeAnyOther()
    {
        // "A part whose Content-Disposition header contains a name parameter whose value is `_charset_` is
        // parsed like any other part. It does not change the encoding."
        var engine = WebEngine();
        engine.Execute("var parsed = " + ParseExpression(
            "--b\r\n"
            + "Content-Disposition: form-data; name=\"_charset_\"\r\n"
            + "\r\n"
            + "iso-8859-1\r\n"
            + "--b\r\n"
            + "Content-Disposition: form-data; name=\"v\"\r\n"
            + "\r\n"
            + "é\r\n"
            + "--b--\r\n") + ";");

        engine.Evaluate("parsed.then(fd => fd.get('_charset_'))").UnwrapIfPromise().AsString().Should().Be("iso-8859-1");
        engine.Evaluate("parsed.then(fd => fd.get('v'))").UnwrapIfPromise().AsString().Should().Be("é");
    }

    [Test]
    public void ToleratesTransportPaddingAndAMissingTrailingNewline()
    {
        // RFC 2046's transport-padding — "receivers MUST be able to handle padding added by message
        // transports" — and its `[CRLF epilogue]`, which is optional, so a producer that ends the body at the
        // close delimiter still writes something every implementation reads.
        var engine = WebEngine();
        engine.Execute("var parsed = " + ParseExpression(
            "--b \t\r\n"
            + "Content-Disposition: form-data; name=\"a\"\r\n"
            + "\r\n"
            + "1\r\n"
            + "--b--") + ";");

        engine.Evaluate("parsed.then(fd => fd.get('a'))").UnwrapIfPromise().AsString().Should().Be("1");
    }

    [Test]
    public void IgnoresHeadersThatAreNeitherDispositionNorType()
    {
        // RFC 7578 section 4.8: header fields other than these "MUST be ignored".
        var engine = WebEngine();
        engine.Execute("var parsed = " + ParseExpression(
            "--b\r\n"
            + "X-Something: whatever\r\n"
            + "Content-Disposition: form-data; name=\"a\"\r\n"
            + "Content-Transfer-Encoding: binary\r\n"
            + "\r\n"
            + "1\r\n"
            + "--b--\r\n") + ";");

        engine.Evaluate("parsed.then(fd => fd.get('a'))").UnwrapIfPromise().AsString().Should().Be("1");
    }

    [Test]
    public void KeepsAPartWhoseBodyContainsTheBoundaryText()
    {
        // The draft parser scans for the boundary alone and then assumes the four bytes before it were CRLF
        // and two dashes; this one looks for RFC 2046's whole delimiter, so boundary-looking text inside a
        // part is just text.
        var engine = WebEngine();
        engine.Execute("var parsed = " + ParseExpression(
            "--b\r\n"
            + "Content-Disposition: form-data; name=\"a\"\r\n"
            + "\r\n"
            + "text --b more\r\n"
            + "--b--\r\n") + ";");

        engine.Evaluate("parsed.then(fd => fd.get('a'))").UnwrapIfPromise().AsString().Should().Be("text --b more");
    }

    // A preamble before the first delimiter.
    [TestCase("ignore me\r\n--b\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n--b--\r\n")]
    // No close delimiter at all: the last part never ends.
    [TestCase("--b\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n")]
    // A part with no Content-Disposition names no field.
    [TestCase("--b\r\nContent-Type: text/plain\r\n\r\n1\r\n--b--\r\n")]
    // A Content-Disposition that is not the form-data shape.
    [TestCase("--b\r\nContent-Disposition: attachment; name=\"a\"\r\n\r\n1\r\n--b--\r\n")]
    // An unterminated quoted name.
    [TestCase("--b\r\nContent-Disposition: form-data; name=\"a\r\n\r\n1\r\n--b--\r\n")]
    // A header line with no colon.
    [TestCase("--b\r\nContent-Disposition\r\n\r\n1\r\n--b--\r\n")]
    // Bare LFs where the framing needs CRLFs.
    [TestCase("--b\nContent-Disposition: form-data; name=\"a\"\n\n1\n--b--\n")]
    // An empty body, which has no delimiter in it at all.
    [TestCase("")]
    public void RejectsAMalformedBodyWithATypeError(string body)
    {
        var engine = WebEngine();
        engine.Evaluate(ParseExpression(body) + ".then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError: Failed to parse body as multipart/form-data");
    }

    // An asterisk is an HTTP token character, so it survives the MIME parse and has to be refused here.
    [TestCase("b*")]
    // 70 characters is RFC 2046's limit, so 71 is one too many.
    [TestCase("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    // "NOT ending with white space", which is why bcharsnospace exists as its own production.
    [TestCase("b ")]
    public void RejectsABoundaryThatIsNotOne(string boundary)
    {
        // A boundary outside RFC 2046's own production is refused rather than searched for: it is the one
        // part of a Content-Type a hostile producer fully controls, and matching an arbitrary byte run would
        // turn a body into whatever the sender wanted it to parse as. Each body here is well formed *for its
        // declared boundary*, so only the boundary check can be what refuses it.
        var engine = WebEngine();
        engine.Execute($"var boundary = '{boundary}';");
        engine.Execute(@"
            var body = '--' + boundary + '\r\nContent-Disposition: form-data; name=""n""\r\n\r\n1\r\n--' + boundary + '--\r\n';
            var r = new Response(body, { headers: { 'content-type': 'multipart/form-data; boundary=""' + boundary + '""' } });");

        // The body is exactly what the same boundary, spelled legally, parses without complaint.
        engine.Evaluate("r.headers.get('content-type')").AsString().Should().Contain(boundary);

        engine.Evaluate("r.formData().then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError: Failed to parse body as multipart/form-data");
    }

    [Test]
    public void RejectsAMultipartBodyWithNoBoundaryParameter()
    {
        var engine = WebEngine();
        engine.Evaluate(@"new Response('--b--\r\n', { headers: { 'content-type': 'multipart/form-data' } })
                .formData().then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");
    }

    [Test]
    public void AcceptsALegallySpelledBoundaryOfTheSameShape()
    {
        // The control for the theory above: change nothing but the boundary's spelling and it parses.
        var engine = WebEngine();
        engine.Evaluate(@"new Response('--b:b\r\nContent-Disposition: form-data; name=""n""\r\n\r\n1\r\n--b:b--\r\n',
                { headers: { 'content-type': 'multipart/form-data; boundary=""b:b""' } })
                .formData().then(fd => fd.get('n'))")
            .UnwrapIfPromise().AsString().Should().Be("1");
    }

    [Test]
    public void ReadsTheBoundaryOutOfAQuotedParameter()
    {
        // RFC 7578 section 4.1: "it is often necessary to enclose the boundary parameter values in quotes".
        var engine = WebEngine();
        engine.Evaluate(@"new Response('--a,b\r\nContent-Disposition: form-data; name=""n""\r\n\r\n1\r\n--a,b--\r\n',
                { headers: { 'content-type': 'multipart/form-data; boundary=""a,b""' } })
                .formData().then(fd => fd.get('n'))")
            .UnwrapIfPromise().AsString().Should().Be("1");
    }

    [Test]
    public void RefusesAContentTypeItCannotParseAsFormData()
    {
        var engine = WebEngine();

        // The standard's final step, reached both when the essence matches neither arm and when there is no
        // Content-Type at all: throw a TypeError.
        engine.Evaluate("new Response('{}', { headers: { 'content-type': 'application/json' } }).formData().then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");

        engine.Evaluate("new Response(new Blob(['x'])).formData().then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");
    }

    [Test]
    public void DeclaresFormDataOnBothBodyMixins()
    {
        var engine = WebEngine();

        // A mixin's members are copied onto every interface that includes it, so these are two functions.
        engine.Evaluate("typeof Request.prototype.formData").AsString().Should().Be("function");
        engine.Evaluate("typeof Response.prototype.formData").AsString().Should().Be("function");
        engine.Evaluate("Request.prototype.formData === Response.prototype.formData").AsBoolean().Should().BeFalse();

        engine.Evaluate("Response.prototype.formData.length").AsNumber().Should().Be(0);
        engine.Evaluate("Response.prototype.formData.name").AsString().Should().Be("formData");

        // And it brand-checks its receiver like every other member of these prototypes.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Response.prototype.formData.call({})"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Request.prototype.formData.call({})"));
    }
}
#endif
