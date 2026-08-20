#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The opt-in <c>structuredClone</c> global seen from outside the assembly: what a host has to write to get
/// it, what it gets when it writes nothing, and how the algorithm treats the objects a host puts into script.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party.
/// </remarks>
public class WebApiStructuredCloneTests
{
    private sealed class HostRecord
    {
        public string Name { get; set; } = "host";

        public int Count { get; set; } = 1;
    }

    [Fact]
    public void ADefaultEngineHasNoStructuredClone()
    {
        var engine = new Engine();

        engine.Evaluate("typeof structuredClone").AsString().Should().Be("undefined");
        engine.Evaluate("'structuredClone' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheFeatureFlagInstallsIt()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));

        engine.Evaluate("typeof structuredClone").AsString().Should().Be("function");
        engine.Evaluate("structuredClone({ a: [1, 2] }).a[1]").AsNumber().Should().Be(2);

        // DOMException comes with any web API, because it is how this one reports a refusal.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    [Fact]
    public void TheDefaultSetIncludesIt()
    {
        WebApiFeatures.Default.Should().HaveFlag(WebApiFeatures.StructuredClone);

        new Engine(options => options.UseWebApis()).Evaluate("typeof structuredClone").AsString().Should().Be("function");
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own structuredClone");

        var engine = new Engine(options => options
            .AddLazyGlobal("structuredClone", _ => marker)
            .UseWebApis(WebApiFeatures.StructuredClone));

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("structuredClone").Should().BeSameAs(marker);
    }

    [Fact]
    public void HasTheAttributesWebIdlGivesAGlobalOperation()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));

        engine.Evaluate("var d = Object.getOwnPropertyDescriptor(globalThis, 'structuredClone');");
        engine.Evaluate("d.writable").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.configurable").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof d.get").AsString().Should().Be("undefined");

        engine.Evaluate("structuredClone.length").AsNumber().Should().Be(1);
        engine.Evaluate("structuredClone.name").AsString().Should().Be("structuredClone");
    }

    [Fact]
    public void RefusesAWrappedClrObject()
    {
        var engine = new Engine(options => options
            .AllowClr()
            .UseWebApis(WebApiFeatures.StructuredClone));

        engine.SetValue("record", new HostRecord());

        // A CLR object projected into script is the engine's equivalent of a platform object that is not
        // serializable: cloning it would produce a plain object that has quietly lost its identity and its
        // behaviour, so it is refused instead.
        var result = engine.Evaluate(@"
            (function() {
                try { structuredClone(record); return 'no error'; }
                catch (e) { return e.name + '/' + (e instanceof DOMException) + '/' + e.code; }
            })()").AsString();

        result.Should().Be("DataCloneError/true/25");
    }

    [Fact]
    public void ClonesAHostSuppliedObjectGraph()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));

        // What a host builds with the public JsObject factories is an ordinary object, so it clones.
        var source = JsObject.CreateFromEntries(
            engine,
            [
                new KeyValuePair<string, JsValue>("name", new JsString("host")),
                new KeyValuePair<string, JsValue>("count", JsNumber.Create(3)),
            ]);

        engine.SetValue("source", source);
        engine.Evaluate("var clone = structuredClone(source);");

        engine.Evaluate("clone !== source").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.name").AsString().Should().Be("host");
        engine.Evaluate("clone.count").AsNumber().Should().Be(3);
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEngines()
    {
        var options = new Options().UseWebApis(WebApiFeatures.StructuredClone);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Evaluate("structuredClone({ v: 1 }).v").AsNumber().Should().Be(1);
        second.Evaluate("structuredClone({ v: 2 }).v").AsNumber().Should().Be(2);

        // Each engine has its own function object; nothing about the clone is shared through the options.
        first.Evaluate("typeof structuredClone").AsString().Should().Be("function");
        second.Evaluate("typeof structuredClone").AsString().Should().Be("function");
    }

    [Fact]
    public void TransfersAnArrayBufferForAHost()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));

        engine.Execute(@"
            var buffer = new ArrayBuffer(3);
            new Uint8Array(buffer).set([7, 8, 9]);
            var clone = structuredClone(buffer, { transfer: [buffer] });");

        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
        engine.Evaluate("Array.from(new Uint8Array(clone)).join(',')").AsString().Should().Be("7,8,9");
    }
}
#endif
