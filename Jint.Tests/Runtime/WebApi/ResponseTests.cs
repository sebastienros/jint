#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>Response</c> class as the Fetch Standard specifies it —
/// https://fetch.spec.whatwg.org/#response-class.
/// </summary>
public class ResponseTests
{
    private static Engine WebEngine() => new(options => options.UseFetch());

    private static JsValue Eval(string source) => WebEngine().Evaluate(source);

    [Fact]
    public void DefaultsToAnEmpty200()
    {
        Eval("new Response().status").AsNumber().Should().Be(200);
        Eval("new Response().ok").AsBoolean().Should().BeTrue();
        Eval("new Response().statusText").AsString().Should().Be("");
        Eval("new Response().type").AsString().Should().Be("default");
        Eval("new Response().url").AsString().Should().Be("");
        Eval("new Response().redirected").AsBoolean().Should().BeFalse();
        Eval("new Response().bodyUsed").AsBoolean().Should().BeFalse();
        Eval("[...new Response().headers].length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void OkIsExactlyTheTwoHundreds()
    {
        Eval("new Response(null, { status: 200 }).ok").AsBoolean().Should().BeTrue();
        Eval("new Response(null, { status: 299 }).ok").AsBoolean().Should().BeTrue();
        Eval("new Response(null, { status: 300 }).ok").AsBoolean().Should().BeFalse();
        Eval("new Response(null, { status: 404 }).ok").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void RefusesAStatusOutsideTheAllowedRange()
    {
        foreach (var status in new[] { "199", "600", "0" })
        {
            Assert.Throws<JavaScriptException>(() => Eval($"new Response(null, {{ status: {status} }})"))
                .Message.Should().Contain("outside the range");
        }

        // The member is an unsigned short, so it wraps modulo 2^16 before the range check.
        Eval("new Response(null, { status: 65736 }).status").AsNumber().Should().Be(200);
    }

    [Fact]
    public void RefusesAStatusTextThatIsNotAReasonPhrase()
    {
        Eval("new Response(null, { statusText: 'All good' }).statusText").AsString().Should().Be("All good");

        Assert.Throws<JavaScriptException>(() => Eval("new Response(null, { statusText: 'a\\r\\nb' })"))
            .Message.Should().Contain("Invalid status text");
    }

    [Fact]
    public void RefusesABodyOnANullBodyStatus()
    {
        // https://fetch.spec.whatwg.org/#null-body-status
        foreach (var status in new[] { "204", "205", "304" })
        {
            Assert.Throws<JavaScriptException>(() => Eval($"new Response('x', {{ status: {status} }})"))
                .Message.Should().Contain("null body status");
        }

        // A null body is fine on those statuses, obviously.
        Eval("new Response(null, { status: 204 }).status").AsNumber().Should().Be(204);
    }

    [Fact]
    public void ExtractsTheBodyBeforeTheRangeCheck()
    {
        // https://fetch.spec.whatwg.org/#dom-response steps 3-5: the body is extracted before "initialize a
        // response" runs its checks, so the unsupported body is what is reported.
        Assert.Throws<JavaScriptException>(() => Eval("new Response(new FormData(), { status: 999 })"))
            .Message.Should().Contain("multipart/form-data");
    }

    [Fact]
    public void ReadsTheBodyBackInEveryShape()
    {
        var engine = WebEngine();
        engine.Execute("function res() { return new Response('{\"a\":1}', { headers: { 'content-type': 'application/json' } }); }");

        engine.Evaluate("res().text()").UnwrapIfPromise().AsString().Should().Be("{\"a\":1}");
        engine.Evaluate("res().json()").UnwrapIfPromise().AsObject().Get("a").AsNumber().Should().Be(1);
        engine.Evaluate("res().arrayBuffer()").UnwrapIfPromise().AsObject().Get("byteLength").AsNumber().Should().Be(7);
        engine.Evaluate("res().bytes().then(b => b instanceof Uint8Array && b.length === 7 && b[0] === 123)")
            .UnwrapIfPromise().AsBoolean().Should().BeTrue();

        // blob() takes its type from the Content-Type header.
        engine.Evaluate("res().blob().then(b => b.type)").UnwrapIfPromise().AsString().Should().Be("application/json");
        engine.Evaluate("new Response('x').blob().then(b => b.type)").UnwrapIfPromise().AsString().Should().Be("text/plain;charset=utf-8");
    }

    [Fact]
    public void ConsumingANullBodyIsAlwaysAllowed()
    {
        // https://fetch.spec.whatwg.org/#body-unusable — only a *non-null* body can be unusable, so a
        // bodyless response reads as the empty string any number of times and never flips bodyUsed.
        var engine = WebEngine();
        engine.Execute("var r = new Response();");

        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("");
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("r.text()").UnwrapIfPromise().AsString().Should().Be("");
    }

    [Fact]
    public void ASecondConsumeRejectsRatherThanThrowing()
    {
        // https://fetch.spec.whatwg.org/#concept-body-consume-body — "return a promise rejected with a
        // TypeError", never a synchronous throw, which is what lets a fetch chain be written without a try.
        var engine = WebEngine();
        engine.Execute("var r = new Response('hi');");
        engine.Evaluate("r.text()").UnwrapIfPromise();

        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();

        // No throw here — the rejection is the answer.
        var second = engine.Evaluate("r.text().then(() => 'resolved', e => e.constructor.name + ': ' + e.message)");
        second.UnwrapIfPromise().AsString().Should().Be("TypeError: Body has already been consumed");
    }

    [Fact]
    public void MalformedJsonRejectsWithASyntaxError()
    {
        var engine = WebEngine();
        engine.Execute("var r = new Response('{oops');");

        engine.Evaluate("r.json().then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("SyntaxError");

        // The body still counts as consumed, exactly as a successful parse would leave it.
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CloneSharesTheBytesAndNotTheUsedFlag()
    {
        var engine = WebEngine();
        engine.Execute("var a = new Response('hi', { status: 201, statusText: 'Made', headers: { 'x-a': '1' } }); var b = a.clone();");

        engine.Evaluate("b.status").AsNumber().Should().Be(201);
        engine.Evaluate("b.statusText").AsString().Should().Be("Made");
        engine.Evaluate("b.headers.get('x-a')").AsString().Should().Be("1");

        engine.Evaluate("a.text()").UnwrapIfPromise().AsString().Should().Be("hi");
        engine.Evaluate("a.bodyUsed").AsBoolean().Should().BeTrue();
        engine.Evaluate("b.bodyUsed").AsBoolean().Should().BeFalse();
        engine.Evaluate("b.text()").UnwrapIfPromise().AsString().Should().Be("hi");

        // The headers are copied, not shared.
        engine.Execute("b.headers.set('x-b', '2');");
        engine.Evaluate("a.headers.has('x-b')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void CloneThrowsSynchronouslyForAnUsedBody()
    {
        var engine = WebEngine();
        engine.Execute("var r = new Response('hi'); r.text();");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("r.clone()"))
            .Message.Should().Contain("already used");
    }

    [Fact]
    public void ErrorIsAnImmutableZeroStatusResponse()
    {
        // https://fetch.spec.whatwg.org/#dom-response-error
        var engine = WebEngine();
        engine.Execute("var r = Response.error();");

        engine.Evaluate("r.type").AsString().Should().Be("error");
        engine.Evaluate("r.status").AsNumber().Should().Be(0);
        engine.Evaluate("r.ok").AsBoolean().Should().BeFalse();
        engine.Evaluate("r.body").Should().Be(JsValue.Null);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("r.headers.set('a', '1')"))
            .Message.Should().Contain("immutable");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("r.headers.append('a', '1')"))
            .Message.Should().Contain("immutable");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("r.headers.delete('a')"))
            .Message.Should().Contain("immutable");
    }

    [Fact]
    public void RedirectCarriesTheLocationAndRefusesANonRedirectStatus()
    {
        // https://fetch.spec.whatwg.org/#dom-response-redirect
        Eval("Response.redirect('https://example.org/a').status").AsNumber().Should().Be(302);
        Eval("Response.redirect('https://example.org/a').headers.get('location')").AsString().Should().Be("https://example.org/a");
        Eval("Response.redirect('https://example.org/a', 301).status").AsNumber().Should().Be(301);

        Assert.Throws<JavaScriptException>(() => Eval("Response.redirect('https://example.org/a', 200)"))
            .Message.Should().Contain("Invalid status code");

        Assert.Throws<JavaScriptException>(() => Eval("Response.redirect('nope')"))
            .Message.Should().Contain("Failed to parse URL");

        // The headers are immutable, so a script cannot rewrite where the redirect points.
        Assert.Throws<JavaScriptException>(() => Eval("Response.redirect('https://example.org/a').headers.set('location', 'https://evil.example/')"))
            .Message.Should().Contain("immutable");
    }

    [Fact]
    public void JsonSerializesAndCarriesTheJsonContentType()
    {
        // https://fetch.spec.whatwg.org/#dom-response-json
        var engine = WebEngine();

        engine.Evaluate("Response.json({ a: 1 }).headers.get('content-type')").AsString().Should().Be("application/json");
        engine.Evaluate("Response.json({ a: 1 }).text()").UnwrapIfPromise().AsString().Should().Be("{\"a\":1}");
        engine.Evaluate("Response.json({ a: 1 }, { status: 201 }).status").AsNumber().Should().Be(201);

        // An explicit Content-Type in the init wins, because the headers are filled first.
        engine.Evaluate("Response.json({}, { headers: { 'content-type': 'application/problem+json' } }).headers.get('content-type')")
            .AsString().Should().Be("application/problem+json");

        // "If result is undefined, throw a TypeError" — https://infra.spec.whatwg.org/#serialize-a-javascript-value-to-a-json-string.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Response.json(undefined)"))
            .Message.Should().Contain("not JSON serializable");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Response.json(() => {})"));

        // The body goes through "initialize a response" too, so a null body status is a TypeError here as well.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Response.json(null, { status: 204 })"))
            .Message.Should().Contain("null body status");
    }

    [Fact]
    public void HasNoOwnPropertiesAndTheRightToStringTag()
    {
        Eval("Object.getOwnPropertyNames(new Response()).length").AsNumber().Should().Be(0);
        Eval("Object.prototype.toString.call(new Response())").AsString().Should().Be("[object Response]");
    }

    [Fact]
    public void HasNoFormDataMember()
    {
        Eval("'formData' in Response.prototype").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheBodyMixinIsPerInterfaceRatherThanShared()
    {
        // A WebIDL mixin's members are copied onto every interface that includes it, so the two `text`
        // functions are different objects — exactly as in a browser.
        Eval("Request.prototype.text === Response.prototype.text").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void BrandChecksEveryMember()
    {
        foreach (var member in new[] { "type", "url", "redirected", "status", "ok", "statusText", "headers", "body", "bodyUsed" })
        {
            Assert.Throws<JavaScriptException>(() => Eval($"Response.prototype.{member}"))
                .Message.Should().Contain("Response");
        }

        foreach (var member in new[] { "clone()", "text()", "json()", "blob()", "bytes()", "arrayBuffer()" })
        {
            Assert.Throws<JavaScriptException>(() => Eval($"Response.prototype.{member}"))
                .Message.Should().Contain("Response");
        }
    }

    [Fact]
    public void IsConstructibleAsABaseClass()
    {
        Eval("(() => { class R extends Response {}; const r = new R('hi'); return r.status + ':' + (r instanceof R); })()")
            .AsString().Should().Be("200:true");
    }
}
#endif
