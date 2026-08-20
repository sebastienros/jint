#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>Blob</c>, <c>File</c> and <c>FormData</c> seen from outside the assembly: what a host has to write to
/// get them, what it gets when it writes nothing, and the attributes the globals carry.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party. The pin
/// that matters most is <see cref="ADefaultEngineHasNoFileGlobals"/>: the surface is opt-in, and an engine
/// that did not ask for it must be the engine it was before any of this existed.
/// </remarks>
public class WebApiFilesTests
{
    [Fact]
    public void ADefaultEngineHasNoFileGlobals()
    {
        var engine = new Engine();

        engine.Evaluate("typeof Blob").AsString().Should().Be("undefined");
        engine.Evaluate("typeof File").AsString().Should().Be("undefined");
        engine.Evaluate("typeof FormData").AsString().Should().Be("undefined");
        engine.Evaluate("'Blob' in globalThis").AsBoolean().Should().BeFalse();

        // Not even an engine that named the group but no feature.
        new Engine(options => options.WebApi.Features = WebApiFeatures.None)
            .Evaluate("typeof Blob").AsString().Should().Be("undefined");

        // ... nor one that asked for a different feature.
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof Blob").AsString().Should().Be("undefined");
    }

    [Fact]
    public void TheFilesFlagInstallsAllThreeInterfaces()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Files));

        engine.Evaluate("typeof Blob").AsString().Should().Be("function");
        engine.Evaluate("typeof File").AsString().Should().Be("function");
        engine.Evaluate("typeof FormData").AsString().Should().Be("function");

        // DOMException comes with any feature, because it is how the web APIs report a failure.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    [Fact]
    public void TheDefaultSetIncludesFiles()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof Blob").AsString().Should().Be("function");
        WebApiFeatures.Default.Should().HaveFlag(WebApiFeatures.Files);
    }

    [Fact]
    public void TheGlobalsCarryTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Files));

        // An interface object is writable and configurable but NOT enumerable —
        // https://webidl.spec.whatwg.org/#es-interfaces.
        foreach (var name in new[] { "Blob", "File", "FormData" })
        {
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(globalThis, '{name}')").AsObject();

            descriptor.Get("writable").AsBoolean().Should().BeTrue();
            descriptor.Get("configurable").AsBoolean().Should().BeTrue();
            descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own Blob");

        var engine = new Engine(options => options
            .AddLazyGlobal("Blob", _ => marker)
            .UseWebApis(WebApiFeatures.Files));

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("Blob").Should().BeSameAs(marker);

        // The names it did not claim are still installed.
        engine.Evaluate("typeof FormData").AsString().Should().Be("function");
    }

    [Fact]
    public void AShadowRealmDoesNotGetThem()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Files));

        // Only the principal realm's global object is touched — deliberately more conservative than a
        // browser, where these are [Exposed=*].
        engine.Evaluate("new ShadowRealm().evaluate('typeof Blob')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof FormData')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof Blob").AsString().Should().Be("function");
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Files));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var b = new Blob(['x']);");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof b").AsString().Should().Be("undefined");
        engine.Evaluate("new Blob(['abc']).size").AsNumber().Should().Be(3);
    }

    [Fact]
    public void TheWholeRoundTripWorksFromAHostsPointOfView()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Files));

        var formData = engine.Evaluate("""
            (function () {
                const form = new FormData();
                form.append('greeting', new File(['hello'], 'greeting.txt', { type: 'text/plain' }));
                form.append('who', 'world');
                return form;
            })()
            """);

        // A host gets a real JsValue back and can hand it to the next evaluation.
        formData.IsObject().Should().BeTrue();
        engine.SetValue("fd", formData);

        engine.Evaluate("fd.get('greeting').name").AsString().Should().Be("greeting.txt");
        engine.Evaluate("fd.get('greeting').type").AsString().Should().Be("text/plain");
        engine.Evaluate("fd.get('greeting').text()").UnwrapIfPromise().AsString().Should().Be("hello");
        engine.Evaluate("fd.get('who')").AsString().Should().Be("world");
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEnginesIndependently()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Files);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("var b = new Blob(['first']);");
        second.Execute("var b = new Blob(['second-one']);");

        first.Evaluate("b.size").AsNumber().Should().Be(5);
        second.Evaluate("b.size").AsNumber().Should().Be(10);

        // Nothing is shared between the two: each realm builds its own interface objects.
        first.Evaluate("b instanceof Blob").AsBoolean().Should().BeTrue();
        second.Evaluate("b instanceof Blob").AsBoolean().Should().BeTrue();
    }
}
#endif
