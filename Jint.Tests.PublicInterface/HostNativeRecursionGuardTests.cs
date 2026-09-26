#nullable enable

using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
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
            // Either the probe's RangeError or a clean completion, never a dead host: what the case rules
            // out is the native stack overflow, and a route that fits the 1 MB stack has nothing to
            // overflow. Which routes fit is a property of the runtime and the day's frame sizes, not of
            // the guard - the .NET Framework JIT turns the empty proxy forward into a tail call that
            // consumes no stack, and ten thousand bound-call frames came to fit on linux x64 once the
            // frames around them got leaner - so the row is not pinned to one of the two answers.
            _ = route;
            outcome.Should().BeOneOf("none", "RangeError:Maximum call stack size exceeded");

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: 1024 * 1024);
    }

    /// <summary>
    /// Two recursions across a <c>ShadowRealm</c> boundary, each a native frame per level, where a failure is
    /// copied into a <c>TypeError</c> at every level on its way back up
    /// (https://tc39.es/proposal-shadowrealm/#sec-create-type-error-copy). The probe raised its
    /// <c>RangeError</c> at the bottom of both, and the host process still ended: the copy was thrown from
    /// inside the <c>catch</c> handling the failure it copies, so each level nested one more exception
    /// dispatch on top of every frame down to the bottom, and nothing probes an exception dispatch. Five
    /// hundred round trips on a 1 MB thread were enough on both .NET 10 and .NET Framework; the first row
    /// makes ten times that.
    /// <para>
    /// The first row is <c>WrappedFunction</c>'s <c>[[Call]]</c> over a chain script builds by passing a
    /// function back and forth; the second is <c>WrappedFunctionCreate</c>, which reads <c>name</c> to copy it
    /// and here finds a getter that wraps its own receiver again, so it has no depth to choose at all. The
    /// message is asserted whole because the copy's mark used to be added once per level, which made the
    /// message — and every copy on the way up — longer with each one.
    /// </para>
    /// </summary>
    public static TestCases<string, string> CrossRealmCopyChains => new()
    {
        {
            "wrapped call",
            "const sr = new ShadowRealm(); const id = sr.evaluate('x => x'); let f = function () { return 1; }; for (let i = 0; i < 5000; i++) f = id(f); f();"
        },
        {
            "wrapped create",
            "const sr = new ShadowRealm(); const g = function () {}; Object.defineProperty(g, 'name', { get: sr.evaluate('(function () {})') }); sr.evaluate('f => f')(g);"
        },
    };

    [TestCaseSource(nameof(CrossRealmCopyChains))]
    public void AFailureCopiedAcrossAShadowRealmBoundaryAtEveryLevelIsCatchable(string route, string script)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();

            var thrown = engine.Invoking(e => e.Execute(script)).Should().Throw<JavaScriptException>().Which;
            thrown.Message.Should().Be("Cross-Realm Error: Maximum call stack size exceeded");
            engine.SetValue("thrown", thrown.Error);
            engine.Evaluate("thrown instanceof TypeError").AsBoolean().Should().BeTrue();

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
    public static TestCases<string, string, string> DeepPrototypeChains => new()
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

    [TestCaseSource(nameof(DeepPrototypeChains))]
    public void ADeepPrototypeChainIsWalkedWithoutExhaustingTheNativeStack(string route, string operation, string expected)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();
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
        }, maxStackSize: 1024 * 1024);
    }

    /// <summary>
    /// The same chain, built out of the shaped prototypes a host declares through
    /// <see cref="JsObjectShape"/> — what a Web IDL binding generator emits, and what a page's ~170
    /// interface prototypes are moving to. Every level overrides nothing and runs the ordinary algorithm,
    /// so the walk must resolve it in its loop rather than hand it the rest of the chain: a hand-over is a
    /// native frame per level, which at this depth is the probe's <c>RangeError</c> instead of the answer.
    /// <para>
    /// That is what makes this an assertion about the <em>walk</em> and not merely about the answers: a
    /// shaped chain shallow enough to recurse through would answer identically either way. One shape
    /// instantiated at every level is deliberate — a shape is process-shared and a host declares it once
    /// per interface, so this is also the allocation shape a real binding has.
    /// </para>
    /// </summary>
    public static TestCases<string, string, string> DeepShapedPrototypeChains => new()
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

    [TestCaseSource(nameof(DeepShapedPrototypeChains))]
    public void ADeepShapedPrototypeChainIsWalkedWithoutExhaustingTheNativeStack(string route, string operation, string expected)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();

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
        }, maxStackSize: 1024 * 1024);
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
    public static TestCases<string, string, string> DeepProxyChains => new()
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

    [TestCaseSource(nameof(DeepProxyChains))]
    public void ADeepProxyChainRaisesACatchableErrorAndTheEngineRecovers(string route, string handler, string operation)
    {
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();
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
        }, maxStackSize: 1024 * 1024);
    }

    /// <summary>
    /// A chain of trapless proxies over a constructor, used where a built-in reads a property of a
    /// constructor it was handed rather than calling it: an array's species constructor
    /// (https://tc39.es/ecma262/#sec-arrayspeciescreate reads <c>@@species</c>) and <c>Reflect.construct</c>'s
    /// <c>newTarget</c> (https://tc39.es/ecma262/#sec-getprototypefromconstructor reads <c>prototype</c>).
    /// Neither read has a trap in the way, so each is <c>JsProxy.Get</c> forwarding to its target once per
    /// link, and it is that forward's probe (#4078) that has to answer — nothing on either route calls
    /// through the chain.
    /// <para>
    /// Two hundred thousand links, because the species route first asks <c>GetFunctionRealm</c>
    /// (https://tc39.es/ecma262/#sec-getfunctionrealm), which recursed once per <c>[[ProxyTarget]]</c> with no
    /// probe until #4165 made it a walk, so the depth is also what says it still is: a recursion there ends the
    /// host before the read begins. What the read itself answers is the <c>RangeError</c> on runtimes that
    /// keep the forwarding frames, and the value on .NET Framework, whose JIT makes the trapless forward a
    /// tail call — the same carve-out as the trapless read above.
    /// </para>
    /// </summary>
    public static TestCases<string, string, string> DeepTraplessProxyConstructorChains => new()
    {
        {
            "array species",
            "var a = [1, 2, 3]; a.constructor = P; outcome = String(a.map(function (x) { return x * 2; }));",
            "2,4,6"
        },
        {
            "Reflect.construct newTarget",
            "outcome = String(Object.getPrototypeOf(Reflect.construct(function () {}, [], P)) === F.prototype);",
            "true"
        },
    };

    [TestCaseSource(nameof(DeepTraplessProxyConstructorChains))]
    public void ADeepTraplessProxyChainReadAsAConstructorRaisesACatchableError(string route, string operation, string frameworkAnswer)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();
            var outcome = engine.Evaluate("""
                var F = function () {};
                var P = F;
                for (var i = 0; i < 200000; i++) { P = new Proxy(P, {}); }
                var outcome;
                try {
                """ + operation + """
                } catch (error) { outcome = error.name + ':' + error.message; }
                String(outcome);
                """).AsString();
#if NETFRAMEWORK
            outcome.Should().BeOneOf(frameworkAnswer, "RangeError:Maximum call stack size exceeded");
#else
            _ = frameworkAnswer;
            outcome.Should().Be("RangeError:Maximum call stack size exceeded");
#endif

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: 1024 * 1024);
    }

    /// <summary>
    /// A target whose own <c>@@hasInstance</c> method asks <c>instanceof</c> of that same target, which the operator
    /// answers by calling the method again (https://tc39.es/ecma262/#sec-instanceofoperator step 3): a recursion script
    /// controls, one native frame per level, and no call expression anywhere in it.
    /// <para>
    /// Every route is a row on both stack lanes. With the default <c>StackOverflowGuard</c> a script function probes on
    /// entry, so the first two routes were already a <c>RangeError</c> there; with <c>MaxExecutionStackCount</c> it does
    /// not, and the only probe that lane arms sits in the call expression this recursion never evaluates, so both ended
    /// the host with a native stack overflow (<c>InstanceOfBinaryExpression</c> → <c>ScriptFunction.CallOnce</c> repeated
    /// to the bottom). The second route's method is a built-in rather than a script function —
    /// <c>Function.prototype.call</c>, which calls the target with <c>V</c> as <c>this</c> — which is why the probe is
    /// on every method but the intrinsic, not on script functions only. The <c>eval</c> route ended the host on both
    /// lanes: the source it evaluates comes back to the operator without entering any function, so nothing probed at all.
    /// </para>
    /// </summary>
    public static TestCases<string, string, bool> HasInstanceRecursions => new()
    {
        { "class static method, StackOverflowGuard", ClassStaticHasInstance, false },
        { "class static method, MaxExecutionStackCount", ClassStaticHasInstance, true },
        { "built-in method, StackOverflowGuard", BuiltInHasInstance, false },
        { "built-in method, MaxExecutionStackCount", BuiltInHasInstance, true },
        { "eval, StackOverflowGuard", EvalHasInstance, false },
        { "eval, MaxExecutionStackCount", EvalHasInstance, true },
    };

    private const string ClassStaticHasInstance =
        "class C { static [Symbol.hasInstance](v) { return v instanceof C; } } outcome = String({} instanceof C);";

    private const string BuiltInHasInstance =
        "var C = function () { return this instanceof C; }; Object.defineProperty(C, Symbol.hasInstance, { value: Function.prototype.call }); outcome = String({} instanceof C);";

    // Indirect eval runs in the global scope, so both bindings are global variables.
    private const string EvalHasInstance =
        "var s = 's instanceof C'; var C = function () {}; Object.defineProperty(C, Symbol.hasInstance, { value: eval }); outcome = String(s instanceof C);";

    [TestCaseSource(nameof(HasInstanceRecursions))]
    public void AHasInstanceMethodThatAsksInstanceofOfItsOwnTargetRaisesACatchableError(string route, string operation, bool maxExecutionStackCountLane)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine(options =>
            {
                if (maxExecutionStackCountLane)
                {
                    options.Constraints.MaxExecutionStackCount = 500;
                }
            });

            var outcome = engine.Evaluate("""
                var outcome;
                try {
                """ + operation + """
                } catch (error) { outcome = error.name + ':' + error.message; }
                String(outcome);
                """).AsString();
            outcome.Should().Be("RangeError:Maximum call stack size exceeded");

            // the engine recovers, and a method that does not recurse answers as it always did beside the intrinsic
            engine.Evaluate("""
                class D { static [Symbol.hasInstance](v) { return v === 1; } }
                [1 instanceof D, 2 instanceof D, [] instanceof Array, {} instanceof Array].join();
                """).AsString().Should().Be("true,false,true,false");
        }, maxStackSize: 1024 * 1024);
    }

    /// <summary>
    /// Recursions made of evaluations rather than of calls: each level hands source text back to the
    /// evaluator, and the evaluator re-enters the interpreter without entering a function. The first four
    /// never reach a function body at all, so none of the probes a function entry carries saw them, and a
    /// direct eval (<c>var s = 'eval(s)'; eval(s)</c>) ended the host process with a native stack overflow on
    /// every target framework. The <c>@@hasInstance</c> row is <c>eval</c> reached with no call expression in
    /// the loop, which nothing else on that route probes either.
    /// <para>
    /// The two <c>Function</c> constructor rows were never broken — the function the constructor builds is
    /// entered through an ordinary call, whose callee probes — and are here so the pair states the shape
    /// that was: evaluating source text is guarded, whichever built-in hands it over.
    /// </para>
    /// </summary>
    public static TestCases<string, string> EvaluatedSourceRecursions => new()
    {
        { "direct eval", "var s = 'eval(s)'; eval(s);" },
        { "indirect eval", "var s = '(0, eval)(s)'; (0, eval)(s);" },
        { "optional-call eval", "var s = 'eval?.(s)'; eval?.(s);" },
        { "eval as a callback", "var s = '[s].forEach(eval)'; [s].forEach(eval);" },
        { "eval as @@hasInstance", "var o = {}; Object.defineProperty(o, Symbol.hasInstance, { value: eval }); var s = 's instanceof o'; s instanceof o;" },
        { "new Function", "var s = 'new Function(s)()'; new Function(s)();" },
        { "Function.prototype.constructor", "var s = 'Function.prototype.constructor(s)()'; Function.prototype.constructor(s)();" },
    };

    [TestCaseSource(nameof(EvaluatedSourceRecursions))]
    public void ARecursionThroughEvaluatedSourceRaisesACatchableErrorAndTheEngineRecovers(string route, string script)
    {
        _ = route;
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();
            EvaluateCatching(engine, script).Should().Be("RangeError:Maximum call stack size exceeded");

            engine.Evaluate("eval('6 * 7')").AsNumber().Should().Be(42);
        }, maxStackSize: 1024 * 1024);
    }

    /// <summary>
    /// The same recursions on the <see cref="Options.ConstraintOptions.MaxExecutionStackCount"/> lane, which
    /// continues a call chain on a fresh thread when the stack runs low and throws once the call stack holds
    /// more than the configured count. A direct eval is dispatched without a call-stack frame, so a recursion
    /// made only of direct evals never grew that count: it hopped to a new thread at every exhaustion, leaving
    /// the one below it blocked, and never threw — the host lost a thread per hop and the call never returned.
    /// It counts as the call it is now, so the lane throws at its limit as it does for any other recursion.
    /// <c>eval?.()</c> took the same frameless dispatch and hung the same way; the other rows push a frame per
    /// level (the <c>EvalFunction</c>, <c>forEach</c>, the constructed function) and were bounded already.
    /// <para>
    /// The join ceiling is what reports the hop that never ends, and is never what passes a test: a bounded
    /// recursion returns in well under a second. <c>@@hasInstance</c> is not a row: with no call expression in
    /// its loop it never reaches this lane's count, and what bounds it instead is the operator's own probe
    /// before any method but the intrinsic, which
    /// <see cref="AHasInstanceMethodThatAsksInstanceofOfItsOwnTargetRaisesACatchableError"/> pins on both lanes.
    /// </para>
    /// </summary>
    public static TestCases<string, string> CountedEvaluatedSourceRecursions => new()
    {
        { "direct eval", "var s = 'eval(s)'; eval(s);" },
        { "indirect eval", "var s = '(0, eval)(s)'; (0, eval)(s);" },
        { "optional-call eval", "var s = 'eval?.(s)'; eval?.(s);" },
        { "eval as a callback", "var s = '[s].forEach(eval)'; [s].forEach(eval);" },
        { "new Function", "var s = 'new Function(s)()'; new Function(s)();" },
    };

    [TestCaseSource(nameof(CountedEvaluatedSourceRecursions))]
    public void OnTheExecutionStackCountLaneARecursionThroughEvaluatedSourceStopsAtTheCount(string route, string script)
    {
        _ = route;
        DedicatedThread.Run(
            () =>
            {
                using var engine = new Engine(options => options.Constraints.MaxExecutionStackCount = 500);
                EvaluateCatching(engine, script).Should().Be("RangeError:Maximum call stack size exceeded");

                engine.Evaluate("eval('6 * 7')").AsNumber().Should().Be(42);
            },
            joinTimeout: TestBudgets.WedgeCeiling,
            timeoutMessage: $"'{route}' did not stop at the configured count within {TestBudgets.WedgeCeiling}; the lane is hopping threads without bound",
            maxStackSize: 1024 * 1024);
    }

    /// <summary>
    /// What the lane is <em>for</em>, through eval: a recursion deeper than the thread holds, but finite and
    /// inside the configured count, continues on a fresh thread and returns its answer. It is what rules out
    /// the other way to bound the rows above — probing eval under
    /// <see cref="Options.ConstraintOptions.StackOverflowGuard"/> on this lane too. That probe sits a few
    /// frames below the call expression's hop, so whichever of the two finds the stack low first decides, and
    /// measured on .NET 10 it was the probe: this recursion threw where it used to return, while .NET Framework,
    /// with other frame sizes, still hopped. Every level counts twice here (the function and the eval), which
    /// the count leaves ample room for.
    /// </summary>
    public static TestCases<string, string> FiniteRecursionsThroughEval => new()
    {
        { "direct eval", "function f(n) { return n === 0 ? 0 : eval('f(n - 1)') + 1; } f(3000);" },
        { "indirect eval", "function f(n) { return n === 0 ? 0 : (0, eval)('f(' + (n - 1) + ')') + 1; } f(3000);" },
    };

    [TestCaseSource(nameof(FiniteRecursionsThroughEval))]
    public void OnTheExecutionStackCountLaneAFiniteRecursionThroughEvalDeeperThanTheThreadReturns(string route, string script)
    {
        _ = route;
        DedicatedThread.Run(
            () =>
            {
                using var engine = new Engine(options => options.Constraints.MaxExecutionStackCount = 100_000);
                engine.Evaluate(script).AsNumber().Should().Be(3000);
            },
            joinTimeout: TestBudgets.WedgeCeiling,
            timeoutMessage: $"'{route}' did not return within {TestBudgets.WedgeCeiling}",
            maxStackSize: 1024 * 1024);
    }

    private static string EvaluateCatching(Engine engine, string script) => engine.Evaluate("""
        var caught;
        try {
        """ + script + """
        } catch (error) { caught = error; }
        caught === undefined ? 'none' : caught.name + ':' + caught.message;
        """).AsString();

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
