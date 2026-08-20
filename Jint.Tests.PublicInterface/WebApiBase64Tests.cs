#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The <c>Base64</c> web API feature seen from outside the assembly: what a host writes to get
/// <c>atob</c> and <c>btoa</c>, what it gets when it writes nothing, and the property shape a script
/// finds them in.
/// </summary>
public class WebApiBase64Tests
{
    [Fact]
    public void ADefaultEngineHasNeitherFunction()
    {
        var engine = new Engine();

        engine.Evaluate("typeof atob").AsString().Should().Be("undefined");
        engine.Evaluate("typeof btoa").AsString().Should().Be("undefined");
        engine.Evaluate("'btoa' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheFeatureFlagInstallsBoth()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Base64));

        engine.Evaluate("btoa('hello')").AsString().Should().Be("aGVsbG8=");
        engine.Evaluate("atob('aGVsbG8=')").AsString().Should().Be("hello");

        // Asking for base64 alone does not bring anything else along.
        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("undefined");
        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
    }

    [Fact]
    public void TheDefaultSetIncludesIt()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof atob").AsString().Should().Be("function");
        engine.Evaluate("typeof btoa").AsString().Should().Be("function");
    }

    [Fact]
    public void GivesTheFunctionsTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Base64));

        // Operations of a WebIDL interface mixin are enumerable, unlike interface objects —
        // https://webidl.spec.whatwg.org/#es-operations.
        foreach (var name in new[] { "atob", "btoa" })
        {
            engine.SetValue("name", name);
            var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, name)").AsObject();

            descriptor.Get("writable").AsBoolean().Should().BeTrue();
            descriptor.Get("configurable").AsBoolean().Should().BeTrue();
            descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own btoa");

        var engine = new Engine(options => options
            .AddLazyGlobal("btoa", _ => marker)
            .UseWebApis(WebApiFeatures.Base64));

        engine.Evaluate("btoa").Should().BeSameAs(marker);
        engine.Evaluate("typeof atob").AsString().Should().Be("function");
    }

    [Fact]
    public void ReportsAFailureAsADomExceptionAHostCanRead()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Base64));

        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate("btoa('\\u20ac')"));

        exception.Error.Get("name").AsString().Should().Be("InvalidCharacterError");
        exception.Error.Get("code").AsNumber().Should().Be(5);

        // It is a real Error, so a host logging it gets a message and a stack.
        engine.SetValue("thrown", exception.Error);
        engine.Evaluate("thrown instanceof Error").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof thrown.stack").AsString().Should().Be("string");
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Base64));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Evaluate("btoa('first')");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("btoa('second')").AsString().Should().Be("c2Vjb25k");
    }
}
#endif
