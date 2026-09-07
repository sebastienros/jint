#nullable enable

using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

public class HostNativeRecursionGuardTests
{
    public static TestCases<string, string> NativeTraversals => new()
    {
        { "flat dense", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; a.flat(Infinity);" },
        { "flat species", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; a.constructor = { [Symbol.species]: function () { return {}; } }; a.flat(Infinity);" },
        { "join", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; a.join();" },
        { "locale", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; a.toLocaleString();" },
        { "JSON array", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; JSON.stringify(a);" },
        { "JSON object", "var a = {}; for (var i = 0; i < 10000; i++) a = { next: a }; JSON.stringify(a);" },
    };

    [TestCaseSource(nameof(NativeTraversals))]
    public void NativeTraversalRaisesACatchableErrorAndTheEngineRecovers(string route, string script)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();
            var outcome = engine.Evaluate("""
                var caught;
                try {
                """ + script + """
                } catch (error) { caught = error; }
                caught === undefined ? 'none' : caught.name + ':' + caught.message;
                """).AsString();
            outcome.Should().Be("RangeError:Maximum call stack size exceeded");

            engine.Evaluate("JSON.stringify({ ok: [1, 2] })").AsString().Should().Be("{\"ok\":[1,2]}");
            engine.Evaluate("[1, 2].flat().join(',')").AsString().Should().Be("1,2");
        }, maxStackSize: 1024 * 1024);
    }

    public static TestCases<string, string> NativeForwardingChains => new()
    {
        {
            "bound call",
            "var f = function () { return 1; }; for (var i = 0; i < 10000; i++) f = f.bind(null); f();"
        },
        {
            "proxy call",
            "var f = function () { return 1; }; for (var i = 0; i < 10000; i++) f = new Proxy(f, {}); f();"
        },
        {
            "proxy construct",
            "var C = function () {}; for (var i = 0; i < 10000; i++) C = new Proxy(C, {}); new C();"
        },
    };

    [TestCaseSource(nameof(NativeForwardingChains))]
    public void NativeForwardingChainRaisesACatchableErrorAndTheEngineRecovers(string route, string script)
    {
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();
            var outcome = engine.Evaluate("""
                var caught;
                try {
                """ + script + """
                } catch (error) { caught = error; }
                caught === undefined ? 'none' : caught.name + ':' + caught.message;
                """).AsString();
#if NETFRAMEWORK
            // The .NET Framework JIT turns the empty proxy-forwarding call into a tail call, so this one
            // route can consume no stack and legitimately complete. Modern runtimes retain the forwarding
            // frames, and construct forwarding still does on every target.
            if (route == "proxy call")
            {
                outcome.Should().BeOneOf("none", "RangeError:Maximum call stack size exceeded");
            }
            else
            {
                outcome.Should().Be("RangeError:Maximum call stack size exceeded");
            }
#else
            outcome.Should().Be("RangeError:Maximum call stack size exceeded");
#endif

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: 1024 * 1024);
    }

    [Test]
    public void HostCallableRecursionRaisesACatchableErrorAndTheEngineRecovers()
    {
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();
            var hostFunction = new RecursiveHostFunction(engine);
            ClrFunction clrFunction = null!;
            clrFunction = new ClrFunction(engine, "clrRecurse", (_, _) => engine.Call(clrFunction));
            engine.SetValue("hostRecurse", hostFunction);
            engine.SetValue("clrRecurse", clrFunction);

            engine.Evaluate("""
                var hostCaught;
                var clrCaught;
                try { hostRecurse(); } catch (error) { hostCaught = error; }
                try { clrRecurse(); } catch (error) { clrCaught = error; }
                hostCaught instanceof RangeError && clrCaught instanceof RangeError;
                """).AsBoolean().Should().BeTrue();

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: 1024 * 1024);
    }

    [Test]
    public void HostConstructorRecursionRaisesACatchableErrorAndTheEngineRecovers()
    {
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();
            engine.SetValue("Recursive", new RecursiveHostConstructor(engine));

            engine.Evaluate("""
                var caught;
                try { new Recursive(); } catch (error) { caught = error; }
                caught instanceof RangeError && caught.message === 'Maximum call stack size exceeded';
                """).AsBoolean().Should().BeTrue();

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: 1024 * 1024);
    }

    private sealed class RecursiveHostFunction : HostFunction
    {
        private readonly Engine _hostEngine;

        public RecursiveHostFunction(Engine engine) : base(engine, "hostRecurse")
        {
            _hostEngine = engine;
        }

        protected override JsValue Invoke(JsValue thisObject, JsValue[] arguments) => _hostEngine.Call(this);
    }

    private sealed class RecursiveHostConstructor : Constructor
    {
        private readonly Engine _hostEngine;

        public RecursiveHostConstructor(Engine engine) : base(engine, "Recursive")
        {
            _hostEngine = engine;
        }

        public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget) => _hostEngine.Construct(this);
    }
}
