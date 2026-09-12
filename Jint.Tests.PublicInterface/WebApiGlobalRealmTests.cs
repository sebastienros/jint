#if NET8_0_OR_GREATER
using Jint.Native;

namespace Jint.Tests.PublicInterface;

public sealed class WebApiGlobalRealmTests
{
    [TestCase("Response", false)]
    [TestCase("Response", true)]
    [TestCase("Event", false)]
    [TestCase("Event", true)]
    [TestCase("DOMException", false)]
    [TestCase("DOMException", true)]
    public void AFirstReadFromAnotherRealmKeepsThePrincipalConstructor(string name, bool restore)
    {
        using var engine = CreateEngine();
        var global = engine.Global;
        var functionPrototype = engine.Intrinsics.Function.Get("prototype").AsObject();
        var shadow = engine.Intrinsics.ShadowRealm.Construct();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        JsValue previous = JsValue.Undefined;
        if (restore)
        {
            previous = global.Get(name);
            engine.Advanced.RestoreGlobalSnapshot(snapshot);
        }

        JsValue observed = JsValue.Undefined;
        var readInAnotherRealm = false;
        shadow.SetValue("read", new Action(() =>
        {
            readInAnotherRealm = !ReferenceEquals(engine.Global, global);
            observed = global.Get(name);
        }));
        shadow.Evaluate("read()");

        readInAnotherRealm.Should().BeTrue();
        observed.AsObject().Prototype.Should().BeSameAs(functionPrototype);
        global.Get(name).Should().BeSameAs(observed, "a second read keeps the materialized intrinsic");
        if (restore)
        {
            observed.Should().BeSameAs(previous, "a restore re-arms the descriptor without replacing its realm's intrinsic");
        }
        shadow.Evaluate("typeof " + name).Should().Be("undefined", "binding a descriptor does not widen realm exposure");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SelfKeepsTheOwningGlobalWhenFirstReadFromAnotherRealm(bool restore)
    {
        using var engine = CreateEngine();
        var global = engine.Global;
        var shadow = engine.Intrinsics.ShadowRealm.Construct();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        if (restore)
        {
            global.Get("self").Should().BeSameAs(global);
            engine.Advanced.RestoreGlobalSnapshot(snapshot);
        }

        JsValue observed = JsValue.Undefined;
        shadow.SetValue("read", new Action(() => observed = global.Get("self")));
        shadow.Evaluate("read()");

        observed.Should().BeSameAs(global);
        shadow.Evaluate("typeof self").Should().Be("undefined");
    }

    [TestCase("Response")]
    [TestCase("Event")]
    [TestCase("DOMException")]
    [TestCase("self")]
    public void TheHostsLazyOverrideIsNotMaterializedOrReplacedByInstallation(string name)
    {
        var reads = 0;
        using var engine = new Engine(options => options
            .AddLazyGlobal(name, _ => { reads++; return new JsString("host"); })
            .UseWebApis(WebApiFeatures.Fetch | WebApiFeatures.Events | WebApiFeatures.GlobalEvents));
        var global = engine.Global;
        var shadow = engine.Intrinsics.ShadowRealm.Construct();
        reads.Should().Be(0);
        JsValue observed = JsValue.Undefined;
        shadow.SetValue("read", new Action(() => observed = global.Get(name)));
        shadow.Evaluate("read()");

        observed.Should().Be("host");
        global.Get(name).Should().BeSameAs(observed);
        reads.Should().Be(1);
    }

    private static Engine CreateEngine()
        => new(options => options.UseWebApis(WebApiFeatures.Fetch | WebApiFeatures.Events | WebApiFeatures.GlobalEvents));
}
#endif
