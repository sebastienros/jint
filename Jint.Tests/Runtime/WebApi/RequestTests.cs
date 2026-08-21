#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>Request</c> class as the Fetch Standard specifies it —
/// https://fetch.spec.whatwg.org/#request-class — reduced to the members this implementation has.
/// </summary>
public class RequestTests
{
    private static Engine WebEngine() => new(options => options.UseFetch());

    private static JsValue Eval(string source) => WebEngine().Evaluate(source);

    [Fact]
    public void DefaultsToAGetOfTheParsedUrl()
    {
        Eval("new Request('https://example.org').method").AsString().Should().Be("GET");

        // The URL is the serialization of the parsed record, so it is normalized.
        Eval("new Request('https://example.org').url").AsString().Should().Be("https://example.org/");
        Eval("new Request('HTTPS://Example.ORG:443/a/../b?q#f').url").AsString().Should().Be("https://example.org/b?q#f");

        Eval("new Request('https://example.org').redirect").AsString().Should().Be("follow");
        Eval("new Request('https://example.org').bodyUsed").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void HasNoBaseUrlSoARelativeInputFails()
    {
        // The specification parses against the entry settings object's API base URL — a document's URL, which
        // an embedded engine has none of.
        Assert.Throws<JavaScriptException>(() => Eval("new Request('/a')"))
            .Message.Should().Contain("Failed to parse URL");
    }

    [Fact]
    public void RefusesAUrlCarryingCredentials()
    {
        // https://fetch.spec.whatwg.org/#dom-request step 5.3.
        Assert.Throws<JavaScriptException>(() => Eval("new Request('https://u:p@example.org/')"))
            .Message.Should().Contain("credentials");
    }

    [Fact]
    public void NormalizesOnlyTheSixStandardMethods()
    {
        // https://fetch.spec.whatwg.org/#concept-method-normalize
        Eval("new Request('https://example.org', { method: 'post' }).method").AsString().Should().Be("POST");
        Eval("new Request('https://example.org', { method: 'DeLeTe' }).method").AsString().Should().Be("DELETE");

        // patch is not on the list, so its casing survives — exactly as in a browser.
        Eval("new Request('https://example.org', { method: 'patch' }).method").AsString().Should().Be("patch");
        Eval("new Request('https://example.org', { method: 'weird' }).method").AsString().Should().Be("weird");
    }

    [Fact]
    public void RefusesAMethodThatIsNotATokenOrIsForbidden()
    {
        Assert.Throws<JavaScriptException>(() => Eval("new Request('https://example.org', { method: 'a b' })"));
        Assert.Throws<JavaScriptException>(() => Eval("new Request('https://example.org', { method: '' })"));

        // https://fetch.spec.whatwg.org/#forbidden-method — a script must not open a proxy tunnel.
        foreach (var method in new[] { "CONNECT", "trace", "TrAcK" })
        {
            Assert.Throws<JavaScriptException>(() => Eval($"new Request('https://example.org', {{ method: '{method}' }})"))
                .Message.Should().Contain("not a valid HTTP method");
        }
    }

    [Fact]
    public void RefusesABodyOnGetAndHead()
    {
        Assert.Throws<JavaScriptException>(() => Eval("new Request('https://example.org', { body: 'x' })"))
            .Message.Should().Contain("cannot have body");

        Assert.Throws<JavaScriptException>(() => Eval("new Request('https://example.org', { method: 'HEAD', body: 'x' })"));

        // An explicit null body is no body at all, so this is fine.
        Eval("new Request('https://example.org', { body: null }).method").AsString().Should().Be("GET");
    }

    [Fact]
    public void ExtractsEveryBodyInitAndItsImpliedContentType()
    {
        // https://fetch.spec.whatwg.org/#concept-bodyinit-extract
        var engine = WebEngine();
        engine.Execute("function req(b) { return new Request('https://example.org', { method: 'POST', body: b }); }");

        engine.Evaluate("req('hi').headers.get('content-type')").AsString().Should().Be("text/plain;charset=UTF-8");
        engine.Evaluate("req('hi').text()").UnwrapIfPromise().AsString().Should().Be("hi");

        engine.Evaluate("req(new URLSearchParams({ a: '1 2' })).headers.get('content-type')")
            .AsString().Should().Be("application/x-www-form-urlencoded;charset=UTF-8");
        engine.Evaluate("req(new URLSearchParams({ a: '1 2' })).text()").UnwrapIfPromise().AsString().Should().Be("a=1+2");

        engine.Evaluate("req(new Blob(['ab'], { type: 'text/csv' })).headers.get('content-type')")
            .AsString().Should().Be("text/csv");

        // A typeless blob implies no Content-Type at all.
        engine.Evaluate("req(new Blob(['ab'])).headers.has('content-type')").AsBoolean().Should().BeFalse();

        // Nor does a buffer source.
        engine.Evaluate("req(new Uint8Array([104, 105])).headers.has('content-type')").AsBoolean().Should().BeFalse();
        engine.Evaluate("req(new Uint8Array([104, 105])).text()").UnwrapIfPromise().AsString().Should().Be("hi");
        engine.Evaluate("req(new Uint8Array([1, 104, 105, 2]).subarray(1, 3)).text()").UnwrapIfPromise().AsString().Should().Be("hi");
        engine.Evaluate("req(new DataView(new Uint8Array([104, 105]).buffer)).text()").UnwrapIfPromise().AsString().Should().Be("hi");

        // Anything else is a USVString.
        engine.Evaluate("req(42).text()").UnwrapIfPromise().AsString().Should().Be("42");
    }

    [Fact]
    public void TakesAFormDataBodyAsMultipart()
    {
        // https://fetch.spec.whatwg.org/#concept-bodyinit-extract: "Set type to `multipart/form-data;
        // boundary=`, followed by the multipart/form-data boundary string generated by the
        // multipart/form-data encoding algorithm."
        var engine = WebEngine();
        engine.Execute(@"
            var fd = new FormData();
            fd.append('a', '1');
            var r = new Request('https://example.org', { method: 'POST', body: fd });");

        engine.Evaluate("r.headers.get('content-type').startsWith('multipart/form-data; boundary=')")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("r.formData().then(parsed => parsed.get('a'))").UnwrapIfPromise().AsString().Should().Be("1");
    }

    [Fact]
    public void CopiesTheBytesOfABufferSourceBody()
    {
        // A body that could change under the engine after it was set would be a request-smuggling primitive.
        Eval(@"(() => {
                const bytes = new Uint8Array([104, 105]);
                const r = new Request('https://example.org', { method: 'POST', body: bytes });
                bytes[0] = 111;
                return r;
            })().text()").UnwrapIfPromise().AsString().Should().Be("hi");
    }

    [Fact]
    public void AnExplicitContentTypeWins()
    {
        // The headers are filled before the body's implied type is appended, so an explicit one survives.
        Eval("new Request('https://example.org', { method: 'POST', body: 'x', headers: { 'content-type': 'text/csv' } }).headers.get('content-type')")
            .AsString().Should().Be("text/csv");
    }

    [Fact]
    public void CopiesFromAnotherRequest()
    {
        var engine = WebEngine();
        engine.Execute("var a = new Request('https://example.org/a', { method: 'POST', body: 'hi', headers: { 'x-a': '1' } });");

        engine.Evaluate("new Request(a).method").AsString().Should().Be("POST");
        engine.Evaluate("new Request(a).url").AsString().Should().Be("https://example.org/a");
        engine.Evaluate("new Request(a).headers.get('x-a')").AsString().Should().Be("1");
        engine.Evaluate("new Request(a).text()").UnwrapIfPromise().AsString().Should().Be("hi");

        // Copying does not consume the original.
        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeFalse();

        // An explicit headers member replaces the copied list wholesale rather than adding to it.
        engine.Evaluate("new Request(a, { headers: { 'x-b': '2' } }).headers.has('x-a')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void RefusesToCopyAnAlreadyReadRequest()
    {
        var engine = WebEngine();
        engine.Execute("var a = new Request('https://example.org', { method: 'POST', body: 'hi' }); a.text();");

        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeTrue();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Request(a)"))
            .Message.Should().Contain("already used");
    }

    [Fact]
    public void AlwaysHasASignalThatFollowsTheOneGiven()
    {
        var engine = WebEngine();

        engine.Evaluate("new Request('https://example.org').signal instanceof AbortSignal").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Request('https://example.org').signal.aborted").AsBoolean().Should().BeFalse();

        // https://fetch.spec.whatwg.org/#dom-request — "make this's signal follow signal".
        engine.Execute("var c = new AbortController(); var r = new Request('https://example.org', { signal: c.signal });");
        engine.Evaluate("r.signal.aborted").AsBoolean().Should().BeFalse();
        engine.Execute("c.abort('gone');");
        engine.Evaluate("r.signal.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.signal.reason").AsString().Should().Be("gone");

        // ... and it is a different object, so aborting the request's signal does not abort the controller's.
        engine.Evaluate("r.signal === c.signal").AsBoolean().Should().BeFalse();

        // An already-aborted signal is inherited as aborted.
        engine.Evaluate("(() => { const c2 = new AbortController(); c2.abort(); return new Request('https://example.org', { signal: c2.signal }).signal.aborted; })()")
            .AsBoolean().Should().BeTrue();

        // An explicit null means no signal to follow, not a type error.
        engine.Evaluate("new Request('https://example.org', { signal: null }).signal.aborted").AsBoolean().Should().BeFalse();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Request('https://example.org', { signal: {} })"))
            .Message.Should().Contain("AbortSignal");
    }

    [Fact]
    public void ValidatesTheRedirectEnumeration()
    {
        Eval("new Request('https://example.org', { redirect: 'manual' }).redirect").AsString().Should().Be("manual");
        Eval("new Request('https://example.org', { redirect: 'error' }).redirect").AsString().Should().Be("error");

        Assert.Throws<JavaScriptException>(() => Eval("new Request('https://example.org', { redirect: 'nope' })"))
            .Message.Should().Contain("RequestRedirect");
    }

    [Fact]
    public void ReadsInitMembersInLexicographicalOrder()
    {
        // WebIDL converts a dictionary's members in lexicographical order of their identifiers, which is
        // observable when they are getters: body, headers, method, redirect, signal.
        Eval(@"(() => {
                const seen = [];
                const init = {};
                for (const name of ['signal', 'redirect', 'method', 'headers', 'body']) {
                    Object.defineProperty(init, name, { get() { seen.push(name); return undefined; }, enumerable: true });
                }
                new Request('https://example.org', init);
                return seen.join(',');
            })()").AsString().Should().Be("body,headers,method,redirect,signal");
    }

    [Fact]
    public void CloneSharesTheBytesAndNotTheUsedFlag()
    {
        var engine = WebEngine();
        engine.Execute("var a = new Request('https://example.org', { method: 'POST', body: 'hi' }); var b = a.clone();");

        engine.Evaluate("a.text()").UnwrapIfPromise().AsString().Should().Be("hi");
        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeTrue();
        engine.Evaluate("b.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("b.text()").UnwrapIfPromise().AsString().Should().Be("hi");

        // The headers are copied, not shared.
        engine.Execute("b.headers.set('x-b', '1');");
        engine.Evaluate("a.headers.has('x-b')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void CloneThrowsSynchronouslyForAnUsedBody()
    {
        // clone does not return a promise, so this is the one Body member that throws rather than rejects.
        var engine = WebEngine();
        engine.Execute("var a = new Request('https://example.org', { method: 'POST', body: 'hi' }); a.text();");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("a.clone()"))
            .Message.Should().Contain("already used");
    }

    [Fact]
    public void ClonesSignalFollowsTheOriginal()
    {
        var engine = WebEngine();
        engine.Execute("var c = new AbortController(); var a = new Request('https://example.org', { signal: c.signal }); var b = a.clone();");

        engine.Execute("c.abort();");
        engine.Evaluate("b.signal.aborted").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void BodyIsAStreamAndBodyUsedTracksConsumption()
    {
        var engine = WebEngine();
        engine.Execute("var a = new Request('https://example.org', { method: 'POST', body: 'hi' });");

        // https://fetch.spec.whatwg.org/#dom-body-body — the body's stream, and null only when there is no
        // body at all, which every GET necessarily has not.
        engine.Evaluate("Object.prototype.toString.call(a.body)").AsString().Should().Be("[object ReadableStream]");
        engine.Evaluate("new Request('https://example.org').body").Should().Be(JsValue.Null);

        // [SameObject]-like in practice: the stream is created once and kept.
        engine.Evaluate("a.body === a.body").AsBoolean().Should().BeTrue();

        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("a.arrayBuffer()").UnwrapIfPromise();
        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReadingTheBodyStreamDirectlyIsWhatDisturbsIt()
    {
        // bodyUsed is the stream's disturbed flag, so touching the stream is what flips it — not calling one
        // of the mixin's consumers.
        var engine = WebEngine();
        engine.Execute("var a = new Request('https://example.org', { method: 'POST', body: 'hi' }); var s = a.body;");

        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeFalse();

        engine.Execute("var r = s.getReader();");

        // Locked but not yet disturbed: bodyUsed is still false, and a consumer already rejects.
        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("a.text().then(() => 'resolved', e => e.constructor.name)").UnwrapIfPromise().AsString().Should().Be("TypeError");

        engine.Evaluate("r.read().then(x => x.value.length)").UnwrapIfPromise().AsNumber().Should().Be(2);
        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AcceptsAReadableStreamAsTheBody()
    {
        var engine = WebEngine();
        engine.Execute(@"
            var source = new ReadableStream({ start(c) { c.enqueue(new Uint8Array([104, 105])); c.close(); } });
            var a = new Request('https://example.org', { method: 'POST', body: source, duplex: 'half' });");

        // https://fetch.spec.whatwg.org/#concept-bodyinit-extract — the ReadableStream arm becomes the
        // body's stream itself, and implies no Content-Type.
        engine.Evaluate("a.body === source").AsBoolean().Should().BeTrue();
        engine.Evaluate("a.headers.has('content-type')").AsBoolean().Should().BeFalse();

        engine.Evaluate("a.text()").UnwrapIfPromise().AsString().Should().Be("hi");
        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request step 41: "If initBody is non-null and init["duplex"] does
    /// not exist, then throw a TypeError." The member is what makes a script say out loud that it knows the
    /// whole request is sent before the response is read.
    /// </summary>
    /// <remarks>
    /// The step keys on the body's <i>source</i> being null, which is only true of the <c>ReadableStream</c>
    /// arm — so every other <c>BodyInit</c> may carry <c>duplex</c> or not as it likes, and neither is
    /// refused. And it keys on <c>initBody</c>, not on the final body, so a request built <i>from another
    /// request</i> that has a stream body needs no <c>duplex</c> of its own.
    /// </remarks>
    [Fact]
    public void RequiresDuplexForAStreamBodyAndForNothingElse()
    {
        var engine = WebEngine();
        engine.Execute("function stream() { return new ReadableStream({ start(c) { c.enqueue(new Uint8Array([104])); c.close(); } }); }");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Request('https://example.org', { method: 'POST', body: stream() })"))
            .Message.Should().Contain("duplex");

        engine.Evaluate("new Request('https://example.org', { method: 'POST', body: stream(), duplex: 'half' }).method")
            .AsString().Should().Be("POST");

        // A body with a source needs nothing, and is not refused for supplying it either.
        engine.Evaluate("new Request('https://example.org', { method: 'POST', body: 'hi' }).method").AsString().Should().Be("POST");
        engine.Evaluate("new Request('https://example.org', { method: 'POST', body: 'hi', duplex: 'half' }).method").AsString().Should().Be("POST");

        // Copying a request whose body is a stream: initBody is null, so the step does not apply.
        engine.Execute("var streamed = new Request('https://example.org', { method: 'POST', body: stream(), duplex: 'half' });");
        engine.Evaluate("new Request(streamed).method").AsString().Should().Be("POST");
        engine.Evaluate("streamed.clone().method").AsString().Should().Be("POST");
    }

    /// <summary>
    /// <c>enum RequestDuplex { "half" };</c> — WebIDL refuses anything else, including the <c>"full"</c> the
    /// standard reserves for a duplex fetch nobody has specified. The attribute always reads back
    /// <c>"half"</c>: https://fetch.spec.whatwg.org/#dom-request-duplex.
    /// </summary>
    [Fact]
    public void HasADuplexAttributeThatOnlyAcceptsHalf()
    {
        var engine = WebEngine();

        engine.Evaluate("new Request('https://example.org').duplex").AsString().Should().Be("half");
        engine.Evaluate("new Request('https://example.org', { duplex: 'half' }).duplex").AsString().Should().Be("half");

        foreach (var invalid in new[] { "'full'", "'HALF'", "''", "null", "1" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"new Request('https://example.org', {{ duplex: {invalid} }})"))
                .Error.Get("name").AsString().Should().Be("TypeError", invalid);
        }

        // An explicit undefined means "not present", as for every other member of the dictionary — so it
        // neither fails the enum conversion nor satisfies the requirement above.
        engine.Evaluate("new Request('https://example.org', { duplex: undefined }).duplex").AsString().Should().Be("half");

        // The attribute is an accessor on the prototype with a brand check, like every other one.
        engine.Evaluate("Object.getOwnPropertyDescriptor(Request.prototype, 'duplex').get.name").AsString().Should().Be("get duplex");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.getOwnPropertyDescriptor(Request.prototype, 'duplex').get.call({})"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void RefusesAReadableStreamThatIsAlreadyDisturbedOrLocked()
    {
        var engine = WebEngine();
        engine.Execute("var locked = new ReadableStream(); locked.getReader();");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Request('https://example.org', { method: 'POST', body: locked })"))
            .Message.Should().Contain("disturbed or locked");
    }

    [Fact]
    public void HasNoOwnPropertiesAndTheRightToStringTag()
    {
        Eval("Object.getOwnPropertyNames(new Request('https://example.org')).length").AsNumber().Should().Be(0);
        Eval("Object.prototype.toString.call(new Request('https://example.org'))").AsString().Should().Be("[object Request]");
    }

    [Fact]
    public void HasAFormDataMember()
    {
        // What it does with a body is MultipartTests' business; that it exists is this file's.
        Eval("typeof Request.prototype.formData").AsString().Should().Be("function");
    }

    [Fact]
    public void BrandChecksEveryMember()
    {
        foreach (var member in new[] { "method", "url", "headers", "redirect", "signal", "body", "bodyUsed" })
        {
            Assert.Throws<JavaScriptException>(() => Eval($"Request.prototype.{member}"))
                .Message.Should().Contain("Request");
        }

        foreach (var member in new[] { "clone()", "text()", "json()", "blob()", "bytes()", "arrayBuffer()" })
        {
            Assert.Throws<JavaScriptException>(() => Eval($"Request.prototype.{member}"))
                .Message.Should().Contain("Request");
        }
    }
}
#endif
