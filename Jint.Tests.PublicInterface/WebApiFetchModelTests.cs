#if NET8_0_OR_GREATER
using Jint;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The fetch object model — <c>Headers</c>, <c>Request</c> and <c>Response</c> — seen from outside the
/// assembly: what a host has to write to get them, what it gets when it writes nothing, and what the
/// <c>Fetch</c> flag brings with it.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party. The pin
/// that matters most is <see cref="ADefaultEngineHasNoFetchGlobals"/> together with
/// <see cref="UseWebApisNeverEnablesFetch"/>: outbound network access is a decision a host makes explicitly,
/// never one it inherits from asking for "the web APIs".
/// </remarks>
public class WebApiFetchModelTests
{
    [Fact]
    public void ADefaultEngineHasNoFetchGlobals()
    {
        var engine = new Engine();

        foreach (var name in new[] { "Headers", "Request", "Response", "fetch" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
            engine.Evaluate($"'{name}' in globalThis").AsBoolean().Should().BeFalse();
        }

        // Not even an engine that named the group but no feature.
        new Engine(options => options.WebApi.Features = WebApiFeatures.None)
            .Evaluate("typeof Response").AsString().Should().Be("undefined");

        // ... nor one that asked for a different feature.
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof Response").AsString().Should().Be("undefined");
    }

    [Fact]
    public void UseWebApisNeverEnablesFetch()
    {
        var engine = new Engine(options => options.UseWebApis());

        foreach (var name in new[] { "Headers", "Request", "Response", "fetch" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
        }

        WebApiFeatures.Default.Should().NotHaveFlag(WebApiFeatures.Fetch);
    }

    [Fact]
    public void UseFetchInstallsTheObjectModel()
    {
        var engine = new Engine(options => options.UseFetch());

        engine.Evaluate("typeof Headers").AsString().Should().Be("function");
        engine.Evaluate("typeof Request").AsString().Should().Be("function");
        engine.Evaluate("typeof Response").AsString().Should().Be("function");

        // DOMException comes with any feature, because it is how the web APIs report a failure.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    [Fact]
    public void UseFetchImpliesEventsUrlAndFiles()
    {
        // fetch's own surface is built out of them: a Request always has an AbortSignal, its URL is a WHATWG
        // URL, and response.blob() answers with a Blob.
        var engine = new Engine(options => options.UseFetch());

        engine.Evaluate("typeof AbortController").AsString().Should().Be("function");
        engine.Evaluate("typeof AbortSignal").AsString().Should().Be("function");
        engine.Evaluate("typeof URL").AsString().Should().Be("function");
        engine.Evaluate("typeof URLSearchParams").AsString().Should().Be("function");
        engine.Evaluate("typeof Blob").AsString().Should().Be("function");

        // ... and it brings nothing else: console and the timers are still the host's own decision.
        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
        engine.Evaluate("typeof setTimeout").AsString().Should().Be("undefined");
    }

    [Fact]
    public void UseFetchOnlyRecordsTheFetchFlag()
    {
        var options = new Options().UseFetch();

        // The closure is applied when the engine is built, so what the host asked for is what it reads back.
        options.WebApi.Features.Should().Be(WebApiFeatures.Fetch);
    }

    [Fact]
    public void FeatureCallsAccumulateRatherThanReplace()
    {
        var options = new Options().UseConsole(ConsoleSink.Null).UseFetch();

        options.WebApi.Features.Should().HaveFlag(WebApiFeatures.Console);
        options.WebApi.Features.Should().HaveFlag(WebApiFeatures.Fetch);
    }

    [Fact]
    public void TheGlobalsCarryTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseFetch());

        foreach (var name in new[] { "Headers", "Request", "Response" })
        {
            // https://webidl.spec.whatwg.org/#es-interfaces — an interface object is writable and
            // configurable but not enumerable.
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(globalThis, '{name}')").AsObject();

            descriptor.Get("writable").AsBoolean().Should().BeTrue();
            descriptor.Get("configurable").AsBoolean().Should().BeTrue();
            descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public void AShadowRealmDoesNotGetThem()
    {
        var engine = new Engine(options => options.UseFetch());

        engine.Evaluate("new ShadowRealm().evaluate('typeof Response')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof Response").AsString().Should().Be("function");
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new Native.JsString("host's own Response");

        var engine = new Engine(options => options
            .AddLazyGlobal("Response", _ => marker)
            .UseFetch());

        engine.Evaluate("Response").Should().BeSameAs(marker);

        // The rest of the surface is still installed.
        engine.Evaluate("typeof Headers").AsString().Should().Be("function");
    }

    [Fact]
    public void TheObjectModelIsUsableWithoutAnyNetwork()
    {
        // Everything here is offline: the object model is exactly the part of fetch a host can adopt without
        // granting outbound network access.
        var engine = new Engine(options => options.UseFetch());

        engine.Execute("var r = new Response(JSON.stringify({ a: 1 }), { status: 201, headers: { 'content-type': 'application/json' } });");

        engine.Evaluate("r.status").AsNumber().Should().Be(201);
        engine.Evaluate("r.ok").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.headers.get('Content-Type')").AsString().Should().Be("application/json");
        engine.Evaluate("r.json()").UnwrapIfPromise().AsObject().Get("a").AsNumber().Should().Be(1);
        engine.Evaluate("r.bodyUsed").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void FetchOptionsCarryTheDocumentedDefaults()
    {
        var options = new Options().UseFetch();
        var fetch = options.WebApi.Fetch;

        fetch.HttpClient.Should().BeNull();
        fetch.HttpClientFactory.Should().BeNull();
        fetch.AllowedSchemes.Should().Equal("https", "http");
        fetch.UrlFilter(new Uri("https://example.org/")).Should().BeTrue();
        fetch.MaxResponseBytes.Should().Be(32 * 1024 * 1024);
        fetch.MaxRedirects.Should().Be(20);
        fetch.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        fetch.MaxConcurrentRequests.Should().Be(10);
    }

    [Fact]
    public void UseFetchHandsTheSettingsToTheCallback()
    {
        var options = new Options().UseFetch(f =>
        {
            f.AllowedSchemes.Remove("http");
            f.MaxResponseBytes = 1024;
            f.UrlFilter = uri => uri.Host == "example.org";
        });

        options.WebApi.Fetch.AllowedSchemes.Should().Equal("https");
        options.WebApi.Fetch.MaxResponseBytes.Should().Be(1024);
        options.WebApi.Fetch.UrlFilter(new Uri("https://evil.example/")).Should().BeFalse();
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEnginesIndependently()
    {
        var options = new Options().UseFetch();

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("var h = new Headers({ a: '1' });");
        second.Execute("var h = new Headers({ b: '2' });");

        first.Evaluate("h.has('b')").AsBoolean().Should().BeFalse();
        second.Evaluate("h.has('a')").AsBoolean().Should().BeFalse();

        // Each engine has its own interface objects, as it has its own of every other intrinsic.
        first.Evaluate("Headers").Should().NotBeSameAs(second.Evaluate("Headers"));
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseFetch());
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var r = new Response('hi');");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof r").AsString().Should().Be("undefined");
        engine.Evaluate("new Response('hi').text()").UnwrapIfPromise().AsString().Should().Be("hi");
    }
}
#endif
