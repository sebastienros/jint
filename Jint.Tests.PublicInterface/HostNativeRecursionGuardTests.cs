#nullable enable

using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

public class HostNativeRecursionGuardTests
{
    /// <summary>
    /// A prototype chain whose links are <em>adjacent</em> host objects — one <c>ObjectWrapper</c> after
    /// another, with no ordinary object between any two of them. It is the one chain shape the iterative
    /// walk cannot flatten: a wrapper answers a member miss by forwarding the read to its prototype and
    /// then inspecting the answer for <c>Options.Interop.ThrowOnUnresolvedMember</c>, so the forward is not
    /// a tail call and the link cannot join the loop. A hop to an <em>ordinary</em> link re-enters that loop
    /// and costs nothing further; a hop to another wrapper is a native frame, and a wrapper does not
    /// override <c>SetPrototypeOf</c>, so script builds the chain itself and its depth is an input. Twenty
    /// thousand links ended the host process with a native stack overflow no <c>catch</c> could see
    /// (sebastienros/jint#4087, the shape left over from #4076).
    /// <para>
    /// Both settings of <c>ThrowOnUnresolvedMember</c> are rows because the post-check is the whole reason
    /// the forward cannot be flattened, so the fix has to hold with it on and off alike. What either may
    /// answer is the read's own result or a catchable <c>RangeError</c>; what neither may do is end the
    /// process. <see cref="AShortChainOfAdjacentWrappersAnswersExactlyAsItDid"/> is what says the probe did
    /// not buy that by changing the answer.
    /// </para>
    /// </summary>
    public static TestCases<string, bool> AdjacentWrapperChains => new()
    {
        { "lenient", false },
        { "strict", true },
    };

    [TestCaseSource(nameof(AdjacentWrapperChains))]
    public void AChainOfAdjacentHostWrappersRaisesACatchableErrorAndTheEngineRecovers(string route, bool throwOnUnresolvedMember)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine(options =>
            {
                options.AllowClr();
                options.Interop.ThrowOnUnresolvedMember = throwOnUnresolvedMember;
            });

            engine.SetValue("w", WrapperChain(engine, 20000));

            var outcome = engine.Evaluate("""
                for (var i = 1; i < w.length; i++) { Object.setPrototypeOf(w[i - 1], w[i]); }
                var outcome;
                try { outcome = String(w[0].missing); }
                catch (error) { outcome = error.name + ':' + error.message; }
                outcome;
                """).AsString();

            // Resolving the miss outright would be just as correct — the probe bounds the chain, it does
            // not shorten it — but no thread this test may run on holds twenty thousand of these frames,
            // so what is actually seen is the error the probe raises in place of the dead process.
            outcome.Should().BeOneOf("undefined", "RangeError:Maximum call stack size exceeded");

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: 1024 * 1024);
    }

    /// <summary>
    /// The same chain three links long, which every stack holds: a probe on the forward must not change
    /// what a wrapper answers. Reading <c>tail</c> walks all three links and resolves on the last, which is
    /// what says the chain is a chain; reading a name no link carries is <c>undefined</c> when the option is
    /// off and the host-facing <see cref="MissingMemberException"/> the option exists for when it is on.
    /// </summary>
    [Test]
    public void AShortChainOfAdjacentWrappersAnswersExactlyAsItDid()
    {
        const string Link = "for (var i = 1; i < w.length; i++) { Object.setPrototypeOf(w[i - 1], w[i]); }";

        using var lenient = new Engine(options => options.AllowClr());
        lenient.SetValue("w", WrapperChain(lenient, 3));
        lenient.Evaluate(Link);
        lenient.Evaluate("w[0].Tail").AsString().Should().Be("reached");
        lenient.Evaluate("String(w[0].missing)").AsString().Should().Be("undefined");

        using var strict = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.ThrowOnUnresolvedMember = true;
        });
        strict.SetValue("w", WrapperChain(strict, 3));
        strict.Evaluate(Link);
        strict.Evaluate("w[0].Tail").AsString().Should().Be("reached");
        strict.Invoking(e => e.Evaluate("w[0].missing")).Should().Throw<MissingMemberException>();
    }

    /// <summary>
    /// <paramref name="length"/> host objects, each wrapped once and held by a JavaScript array so the
    /// wrappers keep their identity across reads — <c>w[i]</c> has to be the same object every time or the
    /// script would be setting the prototype of a wrapper it then throws away. The last link is a different
    /// type, so a read that resolves on it can only have walked the whole chain.
    /// </summary>
    private static JsValue WrapperChain(Engine engine, int length)
    {
        var links = new JsValue[length];
        for (var i = 0; i < length - 1; i++)
        {
            links[i] = JsValue.FromObject(engine, new HostChainLink());
        }

        links[length - 1] = JsValue.FromObject(engine, new HostChainTail());
        return new JsArray(engine, links);
    }

    private sealed class HostChainLink
    {
        public string Kind => "link";
    }

    private sealed class HostChainTail
    {
        public string Tail => "reached";
    }

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
