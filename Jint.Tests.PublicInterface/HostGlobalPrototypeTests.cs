using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A wrapped CLR object installed as the prototype of <c>globalThis</c> (issue #2925). Only the global
/// object participates in bare-identifier resolution, so this is where the spec-shaped
/// GlobalEnvironmentRecord lanes (HasBinding/GetBindingValue walking the prototype chain) meet interop:
/// members inherited from the host must resolve as free identifiers, through <c>typeof</c>, and as calls.
/// </summary>
public class HostGlobalPrototypeTests
{
    public sealed class GlobalHost
    {
        public string Greeting => "hello";
        public int Compute(int a, int b) => a + b;
    }

    [Fact]
    public void MembersInheritedFromAHostGlobalPrototypeResolveAsBareIdentifiers()
    {
        var engine = new Engine();
        engine.SetValue("host", new GlobalHost());
        engine.Execute("Object.setPrototypeOf(globalThis, host);");

        engine.Evaluate("Greeting").AsString().Should().Be("hello");
        engine.Evaluate("typeof Greeting").AsString().Should().Be("string");
        engine.Evaluate("typeof Compute").AsString().Should().Be("function");
        engine.Evaluate("Compute(40, 2)").AsNumber().Should().Be(42);
        engine.Evaluate("globalThis.Compute(40, 2)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void HostSidePrototypeAssignmentBehavesTheSame()
    {
        var engine = new Engine();
        var host = new GlobalHost();
        engine.Global.Prototype = (ObjectInstance) JsValue.FromObject(engine, host);

        engine.Evaluate("typeof Compute").AsString().Should().Be("function");
        engine.Evaluate("Compute(40, 2)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void HostMembersResolveFromDeeperInThePrototypeChain()
    {
        var engine = new Engine();
        engine.SetValue("host", new GlobalHost());
        // host sits one level below the direct prototype
        engine.Execute("Object.setPrototypeOf(globalThis, Object.create(host));");

        engine.Evaluate("Greeting").AsString().Should().Be("hello");
        engine.Evaluate("typeof Compute").AsString().Should().Be("function");
        engine.Evaluate("Compute(40, 2)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void OwnGlobalsAndDeclarationsStillShadowInheritedHostMembers()
    {
        var engine = new Engine();
        engine.SetValue("host", new GlobalHost());
        engine.Execute("Object.setPrototypeOf(globalThis, host);");

        // a getter-only CLR property surfaces as inherited non-writable data: sloppy assignment
        // is a silent no-op per OrdinarySet, so no shadowing own property appears
        engine.Evaluate("globalThis.Greeting = 'own'; Greeting").AsString().Should().Be("hello");
        engine.Evaluate("globalThis.hasOwnProperty('Greeting')").AsBoolean().Should().BeFalse();

        // defineProperty and var hoisting create own bindings that shadow the host
        engine.Evaluate("Object.defineProperty(globalThis, 'Greeting', { value: 'own', writable: true, configurable: true }); Greeting").AsString().Should().Be("own");
        engine.Execute("var Compute = 1;");
        engine.Evaluate("typeof Compute").AsString().Should().Be("number");
    }
}
