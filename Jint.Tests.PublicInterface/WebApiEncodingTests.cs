#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The <c>Encoding</c> web API feature seen from outside the assembly: what a host writes to get
/// <c>TextEncoder</c> and <c>TextDecoder</c>, what it gets when it writes nothing, and the property shape
/// a script finds them in.
/// </summary>
public class WebApiEncodingTests
{
    [Test]
    public void ADefaultEngineHasNeitherInterface()
    {
        var engine = new Engine();

        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("undefined");
        engine.Evaluate("typeof TextDecoder").AsString().Should().Be("undefined");
        engine.Evaluate("'TextEncoder' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
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

    [Test]
    public void TheDefaultSetIncludesIt()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("function");
        engine.Evaluate("typeof TextDecoder").AsString().Should().Be("function");
    }

    [Test]
    public void AnotherFeatureAloneDoesNotInstallThem()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Base64));

        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("undefined");
        engine.Evaluate("typeof TextDecoder").AsString().Should().Be("undefined");
    }

    [Test]
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

    [Test]
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

    [Test]
    public void EncodesAndDecodesForTheHost()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Encoding));

        engine.Evaluate("new TextEncoder().encode('Grüße').join(',')").AsString().Should().Be("71,114,195,188,195,159,101");
        engine.Evaluate("new TextDecoder().decode(new Uint8Array([71, 114, 195, 188, 195, 159, 101]))").AsString().Should().Be("Grüße");
    }

    [Test]
    public void DecodesTheLegacyEncodingsForTheHost()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Encoding));

        // The bytes a host reads out of a legacy file or an old HTTP response, decoded through the index
        // the Encoding Standard gives windows-1252.
        engine.Evaluate("new TextDecoder('windows-1252').decode(new Uint8Array([0x93, 0x47, 0x72, 0xFC, 0xDF, 0x65, 0x94]))")
            .AsString().Should().Be("\u201CGrüße\u201D");

        // ISO-8859-1 and every one of its aliases are labels for windows-1252. That is the standard's own
        // rule and the one embedders are most often surprised by, so it is pinned from out here too.
        engine.Evaluate("new TextDecoder('iso-8859-1').encoding").AsString().Should().Be("windows-1252");
        engine.Evaluate("new TextDecoder('latin1').decode(new Uint8Array([0x80])).charCodeAt(0)")
            .AsNumber().Should().Be(0x20AC);

        // x-user-defined maps the upper half into the private use area instead, which is how a script gets
        // arbitrary bytes through a string unharmed.
        engine.Evaluate("new TextDecoder('x-user-defined').decode(new Uint8Array([0xFF])).charCodeAt(0)")
            .AsNumber().Should().Be(0xF7FF);

        // The legacy multi-byte encodings are not implemented, and say so rather than decoding as something
        // else; nor is the replacement encoding, which the specification itself requires be refused.
        foreach (var label in new[] { "shift_jis", "gbk", "big5", "replacement", "iso-2022-kr" })
        {
            engine.SetValue("label", label);
            Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoder(label)"))
                .Error.Get("name").AsString().Should().Be("RangeError");
        }
    }

    [Test]
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
