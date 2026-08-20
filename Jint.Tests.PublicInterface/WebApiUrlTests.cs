#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The <c>URL</c> and <c>URLSearchParams</c> globals seen from outside the assembly: what a host has to write
/// to get them, what it gets when it writes nothing, and the attributes they carry.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so nothing here can reach the parser directly — which is the
/// point. Everything is asserted through the script surface and the public options API, the way an embedder
/// would.
/// </remarks>
public class WebApiUrlTests
{
    [Fact]
    public void ADefaultEngineHasNoUrlGlobals()
    {
        var engine = new Engine();

        engine.Evaluate("typeof URL").AsString().Should().Be("undefined");
        engine.Evaluate("typeof URLSearchParams").AsString().Should().Be("undefined");
        engine.Evaluate("typeof URLPattern").AsString().Should().Be("undefined");
        engine.Evaluate("'URL' in globalThis").AsBoolean().Should().BeFalse();
        engine.Evaluate("'URLSearchParams' in globalThis").AsBoolean().Should().BeFalse();
        engine.Evaluate("'URLPattern' in globalThis").AsBoolean().Should().BeFalse();

        // Nor an engine that asked for a different feature.
        var console = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        console.Evaluate("typeof URL").AsString().Should().Be("undefined");
    }

    [Fact]
    public void TheUrlFlagInstallsEveryInterfaceOfTheStandardFamily()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Url));

        engine.Evaluate("typeof URL").AsString().Should().Be("function");
        engine.Evaluate("typeof URLSearchParams").AsString().Should().Be("function");

        // URLPattern has no flag of its own: it is defined in terms of the URL parser and matches against URLs,
        // so it would have no independent meaning on an engine that has no URL.
        engine.Evaluate("typeof URLPattern").AsString().Should().Be("function");

        // ... and nothing else the group could have brought along.
        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
    }

    [Fact]
    public void TheDefaultSetIncludesUrl()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new URL('https://example.com/a?b=c').href").AsString().Should().Be("https://example.com/a?b=c");
        engine.Evaluate("new URLSearchParams('b=c').get('b')").AsString().Should().Be("c");

        WebApiFeatures.Default.Should().HaveFlag(WebApiFeatures.Url);
    }

    [Fact]
    public void GivesEachGlobalTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Url));

        // An interface object is writable and configurable but not enumerable.
        foreach (var name in new[] { "URL", "URLSearchParams", "URLPattern" })
        {
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(globalThis, '{name}')").AsObject();

            descriptor.Get("writable").AsBoolean().Should().BeTrue();
            descriptor.Get("configurable").AsBoolean().Should().BeTrue();
            descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public void LeavesAGlobalTheHostAlreadyOwns()
    {
        var marker = new JsString("host's own URL");

        var engine = new Engine(options => options
            .AddLazyGlobal("URL", _ => marker)
            .UseWebApis(WebApiFeatures.Url));

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("URL").Should().BeSameAs(marker);

        // The sibling global it did not claim is still installed.
        engine.Evaluate("typeof URLSearchParams").AsString().Should().Be("function");
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Url));

        engine.Evaluate("new ShadowRealm().evaluate('typeof URL')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof URLSearchParams')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof URLPattern')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof URL").AsString().Should().Be("function");
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Url));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var first = new URL('https://example.com/');");
        engine.Evaluate("first.href").AsString().Should().Be("https://example.com/");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof first").AsString().Should().Be("undefined");
        engine.Evaluate("new URL('https://other.example/').href").AsString().Should().Be("https://other.example/");
    }

    [Fact]
    public void IsUsableFromTheHostSide()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Url));

        var url = engine.Evaluate("new URL('https://example.com:8443/a/b?x=1#f')").AsObject();

        url.Get("protocol").AsString().Should().Be("https:");
        url.Get("hostname").AsString().Should().Be("example.com");
        url.Get("port").AsString().Should().Be("8443");
        url.Get("pathname").AsString().Should().Be("/a/b");
        url.Get("origin").AsString().Should().Be("https://example.com:8443");

        url.Set("search", "y=2");
        url.Get("href").AsString().Should().Be("https://example.com:8443/a/b?y=2#f");
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEnginesIndependently()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Url);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("var url = new URL('https://first.example/');");
        second.Execute("var url = new URL('https://second.example/');");

        first.Evaluate("url.href").AsString().Should().Be("https://first.example/");
        second.Evaluate("url.href").AsString().Should().Be("https://second.example/");

        // Nothing about the interfaces is shared between engines: they are per realm, like every intrinsic.
        first.Evaluate("URL").Should().NotBeSameAs(second.Evaluate("URL"));
    }

    [Fact]
    public void ParsesTheHardCasesTheStandardIsFor()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Url));

        // The cases a System.Uri wrapper would get wrong, all straight out of the URL Standard's own tables.
        engine.Evaluate("new URL('https:example.org').href").AsString().Should().Be("https://example.org/");
        engine.Evaluate("new URL('https://////example.com///').href").AsString().Should().Be("https://example.com///");
        engine.Evaluate("new URL('https://example.com/././foo').href").AsString().Should().Be("https://example.com/foo");
        engine.Evaluate("new URL('\\\\example\\\\..\\\\demo/.\\\\', 'https://example.com/').href").AsString().Should().Be("https://example.com/demo/");
        engine.Evaluate("new URL('file:///C|/demo').href").AsString().Should().Be("file:///C:/demo");
        engine.Evaluate("new URL('..', 'file:///C:/demo').href").AsString().Should().Be("file:///C:/");
        engine.Evaluate("new URL('file://loc%61lhost/').href").AsString().Should().Be("file:///");
        engine.Evaluate("new URL('https://example.org/foo bar').href").AsString().Should().Be("https://example.org/foo%20bar");
        engine.Evaluate("new URL('https://EXAMPLE.com/../x').href").AsString().Should().Be("https://example.com/x");
        engine.Evaluate("new URL('https://example/%25?%25#%25').href").AsString().Should().Be("https://example/%25?%25#%25");

        // Hosts: IPv4 in every radix the standard accepts, IPv6 compression, and IDNA.
        engine.Evaluate("new URL('https://0xffffffff/').hostname").AsString().Should().Be("255.255.255.255");
        engine.Evaluate("new URL('https://0/').hostname").AsString().Should().Be("0.0.0.0");
        engine.Evaluate("new URL('https://[0:0::1]/').hostname").AsString().Should().Be("[::1]");
        engine.Evaluate("new URL('https://[1:2:3:4:5:6:1.2.3.4]/').hostname").AsString().Should().Be("[1:2:3:4:5:6:102:304]");
        engine.Evaluate("new URL('https://☕.example/').hostname").AsString().Should().Be("xn--53h.example");

        // ... and the ones that must fail.
        engine.Evaluate("URL.canParse('https://ex ample.org/')").AsBoolean().Should().BeFalse();
        engine.Evaluate("URL.canParse('https://example.com:demo')").AsBoolean().Should().BeFalse();
        engine.Evaluate("URL.canParse('http://[www.example.com]/')").AsBoolean().Should().BeFalse();
        engine.Evaluate("URL.canParse('https://09/')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void MatchesUrlsWithUrlPatternFromTheHostSide()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Url));

        var pattern = engine.Evaluate("new URLPattern('https://*.example.com/products/:id')").AsObject();

        pattern.Get("hostname").AsString().Should().Be("*.example.com");
        pattern.Get("pathname").AsString().Should().Be("/products/:id");
        pattern.Get("hasRegExpGroups").AsBoolean().Should().BeFalse();

        engine.SetValue("pattern", pattern);
        engine.Evaluate("pattern.test('https://shop.example.com/products/74205')").AsBoolean().Should().BeTrue();
        engine.Evaluate("pattern.test('https://shop.example.com/orders/74205')").AsBoolean().Should().BeFalse();

        var result = engine.Evaluate("pattern.exec('https://shop.example.com/products/74205')").AsObject();
        result.Get("pathname").AsObject().Get("groups").AsObject().Get("id").AsString().Should().Be("74205");
        result.Get("hostname").AsObject().Get("groups").AsObject().Get("0").AsString().Should().Be("shop");

        // A component pattern that will not canonicalize is a TypeError from the constructor, not a silent
        // never-matching pattern.
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => engine.Evaluate("new URLPattern({ protocol: 'http{' })"));
    }

    [Fact]
    public void UrlPatternSurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Url));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var p = new URLPattern({ pathname: '/a/:b' });");
        engine.Evaluate("p.test({ pathname: '/a/c' })").AsBoolean().Should().BeTrue();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof p").AsString().Should().Be("undefined");
        engine.Evaluate("new URLPattern({ pathname: '/x' }).test({ pathname: '/x' })").AsBoolean().Should().BeTrue();
    }
}
#endif
