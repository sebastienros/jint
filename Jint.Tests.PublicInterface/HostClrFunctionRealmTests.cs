using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A <see cref="ClrFunction"/> a host builds against an <see cref="Engine"/> must belong to that engine's
/// principal realm, whatever else the engine has been asked to create in the meantime.
/// </summary>
/// <remarks>
/// The engine keeps the first intrinsics it built so that constructing a host function does not need a realm
/// passed in. Constructing a <c>ShadowRealm</c> — or <c>$262.createRealm()</c> under test262 — builds a second
/// set, and that assignment used to be unconditional, so every host function created afterwards took its
/// prototype from a realm the surrounding script cannot reach. It is reachable through an ordinary pattern:
/// a lazily registered global materializes on first read, which can easily be after a script has constructed
/// a shadow realm.
/// </remarks>
public class HostClrFunctionRealmTests
{
    [Fact]
    public void AHostFunctionBuiltBeforeAnyOtherRealmBelongsToTheMainRealm()
    {
        var engine = new Engine();

        engine.SetValue("hostFn", new ClrFunction(engine, "before", static (_, _) => JsValue.Undefined));

        engine.Evaluate("Object.getPrototypeOf(hostFn) === Function.prototype").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AHostFunctionBuiltAfterAShadowRealmStillBelongsToTheMainRealm()
    {
        var engine = new Engine();
        engine.Evaluate("new ShadowRealm()");

        engine.SetValue("hostFn", new ClrFunction(engine, "after", static (_, _) => JsValue.Undefined));

        engine.Evaluate("Object.getPrototypeOf(hostFn) === Function.prototype").AsBoolean().Should()
            .BeTrue("a host function belongs to the engine's principal realm, not to whichever realm was created last");
    }

    [Fact]
    public void ALazilyRegisteredGlobalMaterializingAfterAShadowRealmIsStillAnOrdinaryFunction()
    {
        // The reachable shape of the same defect: the global is declared before anything runs and built on
        // first read, which here happens after the script has constructed a shadow realm.
        var engine = new Engine(options => options.AddLazyGlobal(
            "log",
            static e => new ClrFunction(e, "log", static (_, _) => JsValue.Undefined)));

        engine.Evaluate("new ShadowRealm();");

        engine.Evaluate("log instanceof Function").AsBoolean().Should()
            .BeTrue("a host global must be a Function of the realm the script is running in");
    }
}
