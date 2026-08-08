using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A wrapped CLR object installed as the <c>[[Prototype]]</c> of a JS object (issue #2925). Inherited
/// property reads always worked because <c>ReflectionDescriptor</c> ignores the JS receiver and reads
/// from the instance it was resolved from; inherited <em>method calls</em> now do the same — a receiver
/// with no CLR identity of its own (a plain JS object, whose <c>ToObject</c> would synthesize a stand-in)
/// invokes on the owning instance. A receiver that unwraps to a genuinely different CLR object stays an
/// error, surfaced as a catchable TypeError rather than a raw <c>TargetException</c>.
/// </summary>
public class HostObjectAsPrototypeTests
{
    public sealed class PrototypeHost
    {
        public string Greeting => "hello";
        public int Compute(int a, int b) => a + b;
        public static int StaticCompute(int a, int b) => a + b;
    }

    public sealed class UnrelatedHost
    {
        public int Unrelated => 1;
    }

    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.SetValue("host", new PrototypeHost());
        engine.SetValue("other", new UnrelatedHost());
        return engine;
    }

    [Fact]
    public void InheritedPropertyResolvesThroughThePrototypeChain()
    {
        var engine = CreateEngine();
        engine.Evaluate("var obj = {}; Object.setPrototypeOf(obj, host); obj.Greeting").AsString().Should().Be("hello");
    }

    [Fact]
    public void InheritedMethodInvokesOnTheOwningInstance()
    {
        var engine = CreateEngine();
        engine.Execute("var obj = {}; Object.setPrototypeOf(obj, host);");
        engine.Evaluate("obj.Compute(40, 2)").AsNumber().Should().Be(42);

        // warm repeat: the compiled fast lane engages once the receiver resolves to the owning instance
        engine.Evaluate("var sum = 0; for (var i = 0; i < 100; i++) { sum += obj.Compute(i, 1); } sum").AsNumber().Should().Be(5050);
    }

    [Fact]
    public void InheritedMethodWorksThroughDunderProto()
    {
        var engine = CreateEngine();
        engine.Evaluate("var obj = {}; obj.__proto__ = host; obj.Compute(40, 2)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void InheritedMethodWorksThroughObjectCreate()
    {
        var engine = CreateEngine();
        engine.Evaluate("Object.create(host).Compute(40, 2)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void InheritedMethodWorksWhenPrototypeIsSetFromTheHostSide()
    {
        var engine = new Engine();
        var host = new PrototypeHost();
        engine.Execute("var obj = {};");
        var obj = engine.Evaluate("obj").AsObject();
        obj.Prototype = (ObjectInstance) JsValue.FromObject(engine, host);

        engine.Evaluate("obj.Greeting").AsString().Should().Be("hello");
        engine.Evaluate("obj.Compute(40, 2)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void ExtractedMethodRetargetedOntoAPlainJsReceiverUsesTheOwningInstance()
    {
        var engine = CreateEngine();
        engine.Evaluate("var f = host.Compute; f.call({}, 40, 2)").AsNumber().Should().Be(42);
        engine.Evaluate("host.Compute.bind({})(40, 2)").AsNumber().Should().Be(42);
        engine.Evaluate("Reflect.apply(host.Compute, {}, [40, 2])").AsNumber().Should().Be(42);

        // undefined receiver already fell back to the owning instance and still does
        engine.Evaluate("var g = host.Compute; g(40, 2)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void ForeignClrReceiverSurfacesACatchableTypeError()
    {
        var engine = CreateEngine();

        engine.Evaluate("var f = host.Compute; try { f.call(other, 40, 2); 'no error' } catch (e) { e instanceof TypeError }").AsBoolean().Should().BeTrue();

        var ex = Invoking(() => engine.Evaluate("var h = host.Compute; h.call(other, 40, 2)")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Method 'Compute' called on incompatible receiver");
    }

    [Fact]
    public void StaticMethodIgnoresTheReceiverEntirely()
    {
        var engine = CreateEngine();
        engine.Evaluate("var s = host.StaticCompute; s.call(other, 40, 2)").AsNumber().Should().Be(42);
        engine.Evaluate("var obj = Object.create(host); obj.StaticCompute(40, 2)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void ProxyReceiverUnwrapsToTheProxiedInstance()
    {
        var engine = CreateEngine();
        engine.Evaluate("new Proxy(host, {}).Compute(40, 2)").AsNumber().Should().Be(42);
        engine.Evaluate("var f = host.Compute; f.call(new Proxy(host, {}), 40, 2)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void RevokedProxyReceiverSurfacesACatchableTypeError()
    {
        var engine = CreateEngine();
        engine.Evaluate("""
            var f = host.Compute;
            var r = Proxy.revocable({}, {});
            r.revoke();
            try { f.call(r.proxy, 40, 2); 'no error' } catch (e) { e instanceof TypeError }
            """).AsBoolean().Should().BeTrue();
    }
}
