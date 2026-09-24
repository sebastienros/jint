#nullable enable

using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

public class HostNativeRecursionGuardTests
{
    public static TheoryData<string, string> NativeTraversals => new()
    {
        { "flat dense", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; a.flat(Infinity);" },
        { "flat species", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; a.constructor = { [Symbol.species]: function () { return {}; } }; a.flat(Infinity);" },
        { "join", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; a.join();" },
        { "locale", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; a.toLocaleString();" },
        { "JSON array", "var a = [1]; for (var i = 0; i < 10000; i++) a = [a]; JSON.stringify(a);" },
        { "JSON object", "var a = {}; for (var i = 0; i < 10000; i++) a = { next: a }; JSON.stringify(a);" },
    };

    [Theory]
    [MemberData(nameof(NativeTraversals))]
    public void NativeTraversalRaisesACatchableErrorAndTheEngineRecovers(string route, string script)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            var engine = Guarded();
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
        }, maxStackSize: SmallStack);
    }

    public static TheoryData<string, string> NativeForwardingChains => new()
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

    [Theory]
    [MemberData(nameof(NativeForwardingChains))]
    public void NativeForwardingChainRaisesACatchableErrorAndTheEngineRecovers(string route, string script)
    {
        DedicatedThread.Run(() =>
        {
            var engine = Guarded();
            var outcome = engine.Evaluate("""
                var caught;
                try {
                """ + script + """
                } catch (error) { caught = error; }
                caught === undefined ? 'none' : caught.name + ':' + caught.message;
                """).AsString();
            // A forwarding hop the JIT can turn into a tail call consumes no stack and legitimately completes:
            // the .NET Framework JIT does it for the empty proxy forward, and the x64 System V JIT does it for
            // this branch's bound-call forward (Linux x64 answers "none" where Windows and ARM64 raise). The
            // guard's promise is a catchable error whenever the stack does run out, never that it must.
            if (route is "proxy call" or "bound call")
            {
                outcome.Should().BeOneOf("none", "RangeError:Maximum call stack size exceeded");
            }
            else
            {
                outcome.Should().Be("RangeError:Maximum call stack size exceeded");
            }

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: ForwardingStack);
    }

    [Fact]
    public void HostCallableRecursionRaisesACatchableErrorAndTheEngineRecovers()
    {
        DedicatedThread.Run(() =>
        {
            var engine = Guarded();
            ClrFunction clrFunction = null!;
            clrFunction = new ClrFunction(engine, "clrRecurse", (_, _) => engine.Call(clrFunction));
            engine.SetValue("clrRecurse", clrFunction);

            engine.Evaluate("""
                var clrCaught;
                try { clrRecurse(); } catch (error) { clrCaught = error; }
                clrCaught instanceof RangeError && clrCaught.message === 'Maximum call stack size exceeded';
                """).AsBoolean().Should().BeTrue();

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: SmallStack);
    }

    [Fact]
    public void HostConstructorRecursionRaisesACatchableErrorAndTheEngineRecovers()
    {
        DedicatedThread.Run(() =>
        {
            var engine = Guarded();
            engine.SetValue("Recursive", new RecursiveHostConstructor(engine));

            engine.Evaluate("""
                var caught;
                try { new Recursive(); } catch (error) { caught = error; }
                caught instanceof RangeError && caught.message === 'Maximum call stack size exceeded';
                """).AsBoolean().Should().BeTrue();

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: SmallStack);
    }

    /// <summary>
    /// A prototype chain twenty thousand links deep, and the property operations that have to walk it:
    /// <c>[[Get]]</c>, <c>[[Set]]</c>, <c>[[HasProperty]]</c>, and the <c>[[HasProperty]]</c> a <c>with</c>
    /// statement reaches through its object environment. Every one of them used to descend one native frame
    /// per link and end the host process with a stack overflow no <c>catch</c> could see (#4076).
    /// <para>
    /// What each asserts is the <em>answer</em>, not a <c>RangeError</c>: a chain of ordinary objects is
    /// walked iteratively now, so its depth costs no stack at all and the read, the write and the lookup all
    /// resolve. The hits are there to prove the chain really is twenty thousand links long — a flat object
    /// would answer every miss just as happily.
    /// </para>
    /// </summary>
    public static TheoryData<string, string, string> DeepPrototypeChains => new()
    {
        { "read hit", "outcome = x.deep;", "found" },
        { "read miss", "outcome = String(x.missing);", "undefined" },
        { "inherited getter", "outcome = x.computed;", "found!" },
        { "write", "x.missing = 1; outcome = x.missing + ':' + x.hasOwnProperty('missing');", "1:true" },
        { "inherited setter", "x.written = 2; outcome = String(sink);", "2" },
        { "in hit", "outcome = String('deep' in x);", "true" },
        { "in miss", "outcome = String('missing' in x);", "false" },
        { "with hit", "with (x) { outcome = deep; }", "found" },
        { "with miss", "with (x) { outcome = typeof missing; }", "undefined" },
    };

    [Theory]
    [MemberData(nameof(DeepPrototypeChains))]
    public void ADeepPrototypeChainIsWalkedWithoutExhaustingTheNativeStack(string route, string operation, string expected)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = Guarded();
            var outcome = engine.Evaluate("""
                var sink;
                var x = {
                  deep: 'found',
                  get computed() { return this.deep + '!'; },
                  set written(value) { sink = value; }
                };
                for (var i = 0; i < 20000; i++) { x = { __proto__: x }; }
                var outcome;
                try {
                """ + operation + """
                } catch (error) { outcome = error.name + ':' + error.message; }
                String(outcome);
                """).AsString();
            outcome.Should().Be(expected);

            engine.Evaluate("({ a: 1 }).a").AsNumber().Should().Be(1);
            engine.Evaluate("'a' in { a: 1 }").AsBoolean().Should().BeTrue();
        }, maxStackSize: SmallStack);
    }

    /// <summary>
    /// The same chain, built out of the shaped prototypes a host declares through
    /// <see cref="JsObjectShape"/> — what a Web IDL binding generator emits. Every level overrides nothing and
    /// runs the ordinary algorithm, so the walk must resolve it in its loop rather than hand it the rest of the
    /// chain: a hand-over is a native frame per level, which at this depth is the probe's <c>RangeError</c>
    /// instead of the answer.
    /// <para>
    /// That is what makes this an assertion about the <em>walk</em> and not merely about the answers: a
    /// shaped chain shallow enough to recurse through would answer identically either way. One shape
    /// instantiated at every level is deliberate — a shape is process-shared and a host declares it once
    /// per interface, so this is also the allocation shape a real binding has.
    /// </para>
    /// </summary>
    public static TheoryData<string, string, string> DeepShapedPrototypeChains => new()
    {
        { "read hit", "outcome = x.tag;", "shaped" },
        { "read miss", "outcome = String(x.missing);", "undefined" },
        { "inherited getter", "outcome = x.computed;", "shaped!" },
        { "write", "x.missing = 1; outcome = x.missing + ':' + x.hasOwnProperty('missing');", "1:true" },
        { "in hit", "outcome = String('tag' in x);", "true" },
        { "in miss", "outcome = String('missing' in x);", "false" },
        { "with hit", "with (x) { outcome = tag; }", "shaped" },
        { "with miss", "with (x) { outcome = typeof missing; }", "undefined" },
    };

    [Theory]
    [MemberData(nameof(DeepShapedPrototypeChains))]
    public void ADeepShapedPrototypeChainIsWalkedWithoutExhaustingTheNativeStack(string route, string operation, string expected)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = Guarded();

            var shape = new JsObjectShape.Builder()
                .Constant("tag", new JsString("shaped"))
                .Accessor("computed", static (thisObject, _) => new JsString(((ObjectInstance) thisObject).Get("tag").AsString() + "!"))
                .Build();

            ObjectInstance level = shape.Instantiate(engine);
            for (var i = 0; i < 20000; i++)
            {
                level = shape.Instantiate(engine, level);
            }

            engine.SetValue("chainLeaf", level);

            var outcome = engine.Evaluate("""
                var x = Object.create(chainLeaf);
                var outcome;
                try {
                """ + operation + """
                } catch (error) { outcome = error.name + ':' + error.message; }
                String(outcome);
                """).AsString();
            outcome.Should().Be(expected);

            engine.Evaluate("({ a: 1 }).a").AsNumber().Should().Be(1);
        }, maxStackSize: SmallStack);
    }

    /// <summary>
    /// The same operations over a chain of <em>proxies</em>. A proxy runs its own algorithm rather than the
    /// ordinary one, so it cannot be walked: each hop is a native frame, and the chain is bounded the way
    /// every other forwarding hop in the engine is — by a probe, which turns the overflow into a catchable
    /// <c>RangeError</c>.
    /// <para>
    /// Both handlers are here because only one of them was ever broken, and the pair is what says so. A
    /// <b>trapless</b> proxy forwards the whole algorithm itself (<c>return target.Get(property, receiver)</c>)
    /// with nothing in between, and that is the shape that ended the host process. A <b>trapped</b> one
    /// reaches its trap through <c>ICallable.Call</c>, where the callee probes for itself, so it already
    /// raised this <c>RangeError</c> before any of this — which is precisely why the fix probes the forward
    /// and not the entry of each operation. Trapped proxies are the shape real code ships (every reactivity
    /// library puts a <c>get</c> trap on every object it proxies), so a probe at the entry would be a third
    /// probe on the common path buying nothing.
    /// </para>
    /// </summary>
    public static TheoryData<string, string, string> DeepProxyChains => new()
    {
        { "trapless read", TraplessHandler, "outcome = String(x.missing);" },
        { "trapless write", TraplessHandler, "x.missing = 1; outcome = 'written';" },
        { "trapless has", TraplessHandler, "outcome = String('missing' in x);" },
        { "trapped read", ForwardingHandler, "outcome = String(x.missing);" },
        { "trapped write", ForwardingHandler, "x.missing = 1; outcome = 'written';" },
        { "trapped has", ForwardingHandler, "outcome = String('missing' in x);" },
    };

    private const string TraplessHandler = "{}";

    private const string ForwardingHandler = """
        {
          get: function (target, key) { return target[key]; },
          set: function (target, key, value) { target[key] = value; return true; },
          has: function (target, key) { return key in target; }
        }
        """;

    [Theory]
    [MemberData(nameof(DeepProxyChains))]
    public void ADeepProxyChainRaisesACatchableErrorAndTheEngineRecovers(string route, string handler, string operation)
    {
        DedicatedThread.Run(() =>
        {
            using var engine = Guarded();
            var outcome = engine.Evaluate("""
                var handler =
                """ + handler + """
                ;
                var x = {};
                for (var i = 0; i < 10000; i++) { x = new Proxy(x, handler); }
                var outcome;
                try {
                """ + operation + """
                } catch (error) { outcome = error.name + ':' + error.message; }
                String(outcome);
                """).AsString();
#if NETFRAMEWORK
            // Same carve-out as the trapless proxy *call* above, and for the same reason: the .NET Framework
            // JIT turns `return target.Get(property, receiver)` into a tail call, so this one route can
            // consume no stack and legitimately answer the read. Modern runtimes keep the forwarding frames,
            // and a trap in the way is a real call on every target framework.
            if (route == "trapless read")
            {
                outcome.Should().BeOneOf("undefined", "RangeError:Maximum call stack size exceeded");
            }
            else
            {
                outcome.Should().Be("RangeError:Maximum call stack size exceeded");
            }
#else
            _ = route;
            outcome.Should().Be("RangeError:Maximum call stack size exceeded");
#endif

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: SmallStack);
    }

    private const int SmallStack = 1024 * 1024;

    /// <summary>
    /// A quarter of <see cref="SmallStack"/> for the forwarding chains: on this branch a bound-call or proxy hop
    /// is light enough that ten thousand of them fit in a mebibyte on Linux x64, and light enough on ARM64 that
    /// the hop count needed to overflow it kills the host before the probe can answer. A stack this size runs
    /// out for every frame size at a depth the probe sees first.
    /// </summary>
    private const int ForwardingStack = 256 * 1024;

    /// <summary>
    /// The probe is gated on <c>StackOverflowGuard</c>, which is opt-in on this branch, so every engine
    /// here asks for it.
    /// </summary>
    private static Engine Guarded() => new(options => options.Constraints.StackOverflowGuard = true);

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
