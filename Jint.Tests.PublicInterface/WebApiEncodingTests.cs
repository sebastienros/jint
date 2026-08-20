#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The <c>Encoding</c> web API feature seen from outside the assembly: what a host writes to get
/// <c>TextEncoder</c> and <c>TextDecoder</c>, what it gets when it writes nothing, and the property shape
/// a script finds them in.
/// </summary>
public class WebApiEncodingTests
{
    [Fact]
    public void ADefaultEngineHasNeitherInterface()
    {
        var engine = new Engine();

        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("undefined");
        engine.Evaluate("typeof TextDecoder").AsString().Should().Be("undefined");
        engine.Evaluate("'TextEncoder' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheFeatureFlagInstallsBoth()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Encoding));

        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("function");
        engine.Evaluate("typeof TextDecoder").AsString().Should().Be("function");

        // Asking for encoding alone does not bring anything else along.
        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
        engine.Evaluate("typeof atob").AsString().Should().Be("undefined");

        // DOMException comes with any feature, since it is how they all report failure.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    [Fact]
    public void TheDefaultSetIncludesIt()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("function");
        engine.Evaluate("typeof TextDecoder").AsString().Should().Be("function");
    }

    [Fact]
    public void AnotherFeatureAloneDoesNotInstallThem()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Base64));

        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("undefined");
        engine.Evaluate("typeof TextDecoder").AsString().Should().Be("undefined");
    }

    [Fact]
    public void GivesTheInterfaceObjectsTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Encoding));

        // An interface object is writable and configurable but not enumerable —
        // https://webidl.spec.whatwg.org/#es-interfaces.
        foreach (var name in new[] { "TextEncoder", "TextDecoder" })
        {
            engine.SetValue("name", name);
            var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, name)").AsObject();

            descriptor.Get("writable").AsBoolean().Should().BeTrue();
            descriptor.Get("configurable").AsBoolean().Should().BeTrue();
            descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own TextEncoder");

        var engine = new Engine(options => options
            .AddLazyGlobal("TextEncoder", _ => marker)
            .UseWebApis(WebApiFeatures.Encoding));

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("TextEncoder").Should().BeSameAs(marker);

        // ... and the name it did not claim is still installed.
        engine.Evaluate("typeof TextDecoder").AsString().Should().Be("function");
    }

    [Fact]
    public void EncodesAndDecodesForTheHost()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Encoding));

        engine.Evaluate("new TextEncoder().encode('Grüße').join(',')").AsString().Should().Be("71,114,195,188,195,159,101");
        engine.Evaluate("new TextDecoder().decode(new Uint8Array([71, 114, 195, 188, 195, 159, 101]))").AsString().Should().Be("Grüße");
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEngines()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Encoding);

        var first = new Engine(options);
        var second = new Engine(options);

        // Decoder state is per instance and therefore per engine; nothing lives on the shared options.
        first.Evaluate("var d = new TextDecoder(); d.decode(new Uint8Array([0xE2, 0x82]), { stream: true });");
        second.Evaluate("new TextDecoder().decode(new Uint8Array([0xAC])).charCodeAt(0)").AsNumber().Should().Be(0xFFFD);
        first.Evaluate("d.decode(new Uint8Array([0xAC]))").AsString().Should().Be("€");
    }
}
#endif
