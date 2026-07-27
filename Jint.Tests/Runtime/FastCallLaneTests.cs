using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Invariants of the built-in fast-call lane (<c>Function.GetFastCallShape</c> / <c>CallFast</c>).
///
/// The lane caches a callee per call site and may skip the pooled argument array and, for calls it
/// can prove reach no user code, the call-stack frame. Every test here asserts behaviour that must
/// hold whether or not the lane engages, so they are a live regression baseline as built-ins are
/// progressively annotated: none of them should ever need to change.
/// </summary>
public class FastCallLaneTests
{
    /// <summary>
    /// Re-assigning a built-in swaps the value inside the existing PropertyDescriptor without
    /// bumping any version counter, so nothing weaker than a callee identity check notices. The
    /// site must also recover when the original is restored — a one-way deopt would be a silent
    /// permanent slowdown.
    /// </summary>
    [Fact]
    public void ReassigningABuiltinDeoptimizesTheCallSiteAndRestoringItReoptimizes()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f(x) { return Math.abs(x); }
            const original = Math.abs;
            const before = f(-5);
            Math.abs = function () { return 999; };
            const patched = f(-5);
            Math.abs = original;
            const restored = f(-5);
            before + "," + patched + "," + restored;
            """);

        result.AsString().Should().Be("5,999,5");
    }

    [Fact]
    public void DeletingABuiltinIsObservedByAWarmedCallSite()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f(x) { return Math.abs(x); }
            f(-1); f(-1); f(-1);
            delete Math.abs;
            let threw = false;
            try { f(-1); } catch (e) { threw = e instanceof TypeError; }
            threw;
            """);

        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReplacingTheWholeNamespaceIsObservedByAWarmedCallSite()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f(x) { return Math.abs(x); }
            f(-1); f(-1);
            globalThis.Math = { abs: function () { return 42; } };
            f(-7);
            """);

        result.AsNumber().Should().Be(42);
    }

    /// <summary>
    /// A lexical binding shadows the global, so a warmed site must resolve to the shadowing value.
    /// </summary>
    [Fact]
    public void AShadowingLocalBindingWins()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f(x) { return Math.abs(x); }
            const warm = f(-3);
            function shadowed(x) {
                const Math = { abs: function () { return -1; } };
                return Math.abs(x);
            }
            warm + "," + shadowed(-3);
            """);

        result.AsString().Should().Be("3,-1");
    }

    /// <summary>
    /// The single most important invariant: a built-in's own frame is observable in error.stack.
    /// An object argument coerces through user <c>valueOf</c>, so the frame must NOT be elided —
    /// the fast lane's argument-shape guard exists precisely to keep this case on the framed path.
    /// </summary>
    [Fact]
    public void TheBuiltinsOwnFrameStaysInErrorStackWhenAnArgumentCoercesThroughUserCode()
    {
        var engine = new Engine();
        var stack = engine.Evaluate("""
            function boom() {
                return Math.floor({ valueOf: function () { throw new Error("x"); } });
            }
            let captured = "";
            try { boom(); } catch (e) { captured = e.stack; }
            captured;
            """).AsString();

        stack.Should().Contain("at floor", "the built-in frame must survive; user valueOf can read it");
    }

    /// <summary>
    /// Same guard from the receiver side: a non-primitive receiver coerces via ToJsString/ToObject,
    /// which can run user code, so such calls must keep their frame and their exact semantics.
    /// </summary>
    [Fact]
    public void ABoxedReceiverProducesTheSameResultAsAPrimitiveOne()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function take(s) { return s.charCodeAt(1) + "|" + s.charAt(1) + "|" + s.substring(0, 2); }
            take("abc") === take(new String("abc"));
            """);

        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void NonNumericArgumentsStillCoerceExactlyOnce()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let calls = 0;
            const pos = { valueOf: function () { calls++; return 1; } };
            const c = "abc".charCodeAt(pos);
            c + "," + calls;
            """);

        result.AsString().Should().Be("98,1");
    }

    /// <summary>
    /// The frame invariant again, but at a <em>warmed</em> site — the state the lane's leaf branch is
    /// only ever reached from. <see cref="NonNumericArgumentsStillCoerceExactlyOnce"/> calls each
    /// built-in once, so the site never warms and the leaf branch is never taken. These four
    /// String.prototype methods take their arguments as plain <c>JsValue</c> and coerce them in the
    /// body; they claim Leaf under declared per-argument guards, and an object is exactly what those
    /// guards exist to turn away — it must keep the frame the user <c>valueOf</c> can observe.
    /// </summary>
    [Theory]
    [InlineData("\"abc\".charCodeAt(p)", "charCodeAt")]
    [InlineData("\"abc\".charAt(p)", "charAt")]
    [InlineData("\"abc\".substring(p)", "substring")]
    [InlineData("\"abc\".substring(0, p)", "substring")]
    [InlineData("\"abc\".slice(p)", "slice")]
    [InlineData("\"abc\".slice(0, p)", "slice")]
    public void AWarmedSiteKeepsTheBuiltinFrameWhenAnArgumentCoercesThroughUserCode(string call, string builtin)
    {
        var engine = new Engine();
        var stack = engine.Evaluate($$"""
            function f(p) { return {{call}}; }
            f(1); f(1);
            let captured = "";
            try { f({ valueOf: function () { throw new Error("x"); } }); } catch (e) { captured = e.stack; }
            captured;
            """).AsString();

        stack.Should().Contain("at " + builtin, "the built-in frame must survive at a warmed site too");
    }

    /// <summary>
    /// The other half of the elided frame: the frame push is also where <c>LimitRecursion</c> is
    /// charged. The recursion cycle here contains no interpreted frame at all — a user
    /// <c>valueOf</c> reached through a coercion is invoked directly, without one — so the built-in's
    /// own frame is the only thing bounding it. The two priming calls matter: a call site caches its
    /// callee only after a dispatch <em>returns</em>, so a site that first recurses into itself is
    /// still cold all the way down and never reaches the leaf branch.
    /// </summary>
    [Fact]
    public void ARecursionThatOnlyPassesThroughABuiltinIsStillChargedTheRecursionLimit()
    {
        var engine = new Engine(options => options.LimitRecursion(24));
        engine.Execute("""
            var depth = 0;
            var arg = 0;
            var o = { valueOf: function () { depth++; if (depth > 200) return 0; return "abc".charCodeAt(arg); } };
            o.valueOf(); o.valueOf();
            depth = 0; arg = o;
            """);

        try
        {
            engine.Execute("\"abc\".charCodeAt(o);");
        }
        catch (RecursionDepthOverflowException)
        {
            // expected: the recursion limit is what must stop this, not the 200-deep escape hatch
        }

        engine.Evaluate("depth").AsNumber().Should().BeLessThan(100,
            "the frame the leaf lane elides is also what charges LimitRecursion");
    }

    /// <summary>
    /// Absent-argument semantics for the four String.prototype methods whose numeric parameters the
    /// fast-call lane touches. An absent <c>end</c> means "to the end of the string", which is
    /// deliberately NOT the same as an explicit NaN — a difference that survives only while the
    /// undefined-ness of the argument is still observable in the body.
    /// </summary>
    [Theory]
    [InlineData("\"abcdef\".substring(2)", "cdef")]
    [InlineData("\"abcdef\".substring(2, undefined)", "cdef")]
    [InlineData("\"abcdef\".substring(2, NaN)", "ab")]
    [InlineData("\"abcdef\".substring(undefined, 2)", "ab")]
    [InlineData("\"abcdef\".slice(2)", "cdef")]
    [InlineData("\"abcdef\".slice(2, undefined)", "cdef")]
    [InlineData("\"abcdef\".slice(2, NaN)", "")]
    [InlineData("\"abcdef\".slice(undefined, 2)", "ab")]
    [InlineData("String(\"abc\".charCodeAt())", "97")]
    [InlineData("String(\"abc\".charCodeAt(undefined))", "97")]
    [InlineData("String(\"abc\".charCodeAt(NaN))", "97")]
    [InlineData("\"abc\".charAt()", "a")]
    [InlineData("\"abc\".charAt(undefined)", "a")]
    [InlineData("\"abc\".charAt(NaN)", "a")]
    public void AbsentNumericArgumentsKeepTheirSpecDefinedMeaning(string expression, string expected)
    {
        var engine = new Engine();

        // Once cold, once warm — the lane must not change the answer.
        engine.Evaluate($"function f() {{ return {expression}; }} f();").AsString().Should().Be(expected);
        engine.Evaluate($"function g() {{ return {expression}; }} g(); g();").AsString().Should().Be(expected);
    }

    /// <summary>
    /// Coercion order: the receiver's ToString runs before an argument's valueOf, per steps 2→3 of
    /// each of these methods. Only observable when both are objects.
    /// </summary>
    [Theory]
    [InlineData("charCodeAt")]
    [InlineData("charAt")]
    [InlineData("substring")]
    public void TheReceiverIsCoercedBeforeTheArguments(string method)
    {
        var engine = new Engine();
        var order = engine.Evaluate($$"""
            const log = [];
            const receiver = { toString: function () { log.push("this"); return "abc"; } };
            const pos = { valueOf: function () { log.push("arg"); return 1; } };
            function f(r, p) { return String.prototype.{{method}}.call(r, p); }
            f("abc", 1); f("abc", 1);
            f(receiver, pos);
            log.join(",");
            """).AsString();

        order.Should().Be("this,arg");
    }

    /// <summary>
    /// The receiver-only String.prototype methods claim Leaf under a String receiver guard. A
    /// receiver that is anything else — a boxed String, an object with a user <c>toString</c> —
    /// must fail the guard and keep both its frame and its exact semantics, at a warmed site.
    /// </summary>
    [Theory]
    [InlineData("trim")]
    [InlineData("trimStart")]
    [InlineData("trimEnd")]
    [InlineData("toUpperCase")]
    [InlineData("toLowerCase")]
    public void AReceiverGuardedLeafBuiltinStillFramesEveryOtherReceiver(string method)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            function f(r) { return r.{{method}}(); }
            const primitive = f("  aB  ");
            f("  aB  ");
            const boxed = f(new String("  aB  "));
            let stack = "";
            const host = { toString: function () { stack = new Error().stack; return "  aB  "; } };
            host.{{method}} = String.prototype.{{method}};
            const hosted = f(host);
            [primitive === boxed, primitive === hosted, stack.indexOf("at {{method}}") >= 0].join(",");
            """).AsString();

        result.Should().Be("true,true,true");
    }

    /// <summary>
    /// A declared argument guard is a claim about values, not about the method, so the receiver guard
    /// still has to hold independently: a boxed or hosted receiver coerces through user code and must
    /// stay framed even when the argument is a plain number that satisfies its own guard.
    /// </summary>
    [Theory]
    [InlineData("charCodeAt", "1")]
    [InlineData("charAt", "1")]
    [InlineData("substring", "0, 2")]
    [InlineData("slice", "0, 2")]
    public void ADeclaredArgumentGuardDoesNotExcuseTheReceiverGuard(string method, string args)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            function f(r) { return r.{{method}}({{args}}); }
            const primitive = f("abc");
            f("abc");
            const boxed = f(new String("abc"));
            let stack = "";
            const host = { toString: function () { stack = new Error().stack; return "abc"; } };
            host.{{method}} = String.prototype.{{method}};
            const hosted = f(host);
            [primitive === boxed, primitive === hosted, stack.indexOf("at {{method}}") >= 0].join(",");
            """).AsString();

        result.Should().Be("true,true,true");
    }

    /// <summary>
    /// The declared guards name numbers and <c>undefined</c>. Every other primitive — a string, a
    /// boolean, null, a symbol — fails them and must take the framed path with unchanged semantics,
    /// including the TypeError a symbol argument raises, which the frameless lane must never carry.
    /// </summary>
    [Theory]
    [InlineData("\"abcdef\".substring(\"2\")", "cdef")]
    [InlineData("\"abcdef\".substring(true, \"3\")", "bc")]
    [InlineData("\"abcdef\".slice(null, \"3\")", "abc")]
    [InlineData("String(\"abc\".charCodeAt(\"1\"))", "98")]
    [InlineData("\"abc\".charAt(false)", "a")]
    public void NonConformingPrimitiveArgumentsKeepTheirSemanticsAtAWarmedSite(string expression, string expected)
    {
        var engine = new Engine();

        engine.Evaluate($"function f() {{ return {expression}; }} f();").AsString().Should().Be(expected);
        engine.Evaluate($"function g() {{ return {expression}; }} g(); g(); g();").AsString().Should().Be(expected);
    }

    [Theory]
    [InlineData("\"abc\".substring(p)")]
    [InlineData("\"abc\".slice(0, p)")]
    [InlineData("\"abc\".charCodeAt(p)")]
    [InlineData("\"abc\".charAt(p)")]
    public void ASymbolArgumentStillRaisesACatchableTypeErrorAtAWarmedSite(string call)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            function f(p) { return {{call}}; }
            f(1); f(1);
            let name = "";
            try { f(Symbol()); } catch (e) { name = e.constructor.name; }
            name;
            """).AsString();

        result.Should().Be("TypeError");
    }

    /// <summary>
    /// The variadic lane's whole point: the span it hands the built-in is sized to the call site's
    /// arity, so an omitted argument is absent rather than an <c>undefined</c> element. Every row
    /// here would change answer if the two argument registers were passed through padded, and each
    /// is asserted cold and warm because only the warm run reaches the lane.
    /// </summary>
    [Theory]
    [InlineData("Math.max()", "-Infinity")]
    [InlineData("Math.max(undefined)", "NaN")]
    [InlineData("Math.max(1)", "1")]
    [InlineData("Math.max(1, 2)", "2")]
    [InlineData("Math.max(1, 2, 3)", "3")]
    [InlineData("Math.min()", "Infinity")]
    [InlineData("Math.min(undefined)", "NaN")]
    [InlineData("Math.min(4, 2)", "2")]
    [InlineData("Math.hypot()", "0")]
    [InlineData("Math.hypot(3, 4)", "5")]
    [InlineData("[].push()", "0")]
    [InlineData("[].push(undefined)", "1")]
    [InlineData("[].push(1, 2)", "2")]
    [InlineData("[].push(1, 2, 3)", "3")]
    [InlineData("\"a\".concat()", "a")]
    [InlineData("\"a\".concat(undefined)", "aundefined")]
    [InlineData("\"a\".concat(\"b\", \"c\")", "abc")]
    public void AVariadicBuiltinSeesTheSitesRealArity(string expression, string expected)
    {
        var engine = new Engine();

        engine.Evaluate($"function f() {{ return String({expression}); }} f();").AsString().Should().Be(expected);
        engine.Evaluate($"function g() {{ return String({expression}); }} g(); g(); g();").AsString().Should().Be(expected);
    }

    /// <summary>
    /// A spread makes the arity dynamic, which is precisely what the lane cannot serve, so such a
    /// site must never take it — and must keep giving the right answer.
    /// </summary>
    [Fact]
    public void ASpreadArgumentListStaysOffTheVariadicLane()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f(xs) { return Math.max(...xs); }
            const out = [f([1, 2]), f([5, 3, 9]), f([])];
            const a = [];
            function g(xs) { return a.push(...xs); }
            out.push(g([1, 2]), g([3]), a.length);
            out.join(",");
            """).AsString();

        result.Should().Be("2,9,-Infinity,2,3,3");
    }

    /// <summary>
    /// <c>Math.max</c> is leaf only under the Number guard its <c>[Rest, ToNumber]</c> tail derives.
    /// An object element coerces through user <c>valueOf</c>, so the frame that code can read in
    /// <c>error.stack</c> must survive — at a warmed site, which is the only place the leaf branch
    /// is reachable from.
    /// </summary>
    [Theory]
    [InlineData("Math.max(1, p)", "max")]
    [InlineData("Math.min(p, 1)", "min")]
    [InlineData("Math.hypot(p)", "hypot")]
    public void AWarmedVariadicSiteKeepsTheBuiltinFrameWhenAnElementCoercesThroughUserCode(string call, string builtin)
    {
        var engine = new Engine();
        var stack = engine.Evaluate($$"""
            function f(p) { return {{call}}; }
            f(1); f(1);
            let captured = "";
            try { f({ valueOf: function () { throw new Error("x"); } }); } catch (e) { captured = e.stack; }
            captured;
            """).AsString();

        stack.Should().Contain("at " + builtin, "the built-in frame must survive at a warmed variadic site too");
    }

    /// <summary>
    /// <c>push</c> takes the lane but never its leaf branch: the generic path drives <c>[[Set]]</c> on
    /// an arbitrary array-like, so a setter or a proxy trap is user code that needs the frame — and
    /// its exceptions must stay catchable.
    /// </summary>
    [Fact]
    public void PushKeepsItsFrameAndItsSemanticsForExoticReceivers()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const log = [];
            // The callee has to be `o.push` for the site to reach the lane at all, so every receiver
            // below carries the very same function object and the site stays monomorphic and warm.
            function f(o, v) { return o.push(v); }
            const warm = [];
            f(warm, 1); f(warm, 2);

            const target = { length: 0, push: Array.prototype.push };
            const proxied = new Proxy(target, {
                set: function (t, k, v) { log.push(k); t[k] = v; return true; },
            });
            const viaProxy = f(proxied, "x");

            let stack = "";
            const withSetter = { length: 0, push: Array.prototype.push };
            Object.defineProperty(withSetter, "0", { set: function () { stack = new Error().stack; } });
            f(withSetter, "y");

            let frozen = "";
            const sealed = Object.freeze({ length: 0, push: Array.prototype.push });
            try { f(sealed, "z"); } catch (e) { frozen = e.constructor.name; }

            [warm.length, viaProxy, log.join("+"), stack.indexOf("at push") >= 0, frozen].join(",");
            """).AsString();

        result.Should().Be("2,1,0+length,true,TypeError");
    }

    /// <summary>
    /// Sanity for the annotation sweep: each newly lane-eligible built-in must give the same answer
    /// warm as cold, including the receiver TypeError its brand check raises.
    /// </summary>
    [Fact]
    public void NewlyLaneEligibleBuiltinsAgreeWarmAndCold()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const out = [];
            function run(f, a, b) {
                const cold = f(a, b);
                f(a, b);
                return f(a, b) === cold ? cold : "DRIFT";
            }
            const m = new Map();
            out.push(run(function (k, v) { m.set(k, v); return m.get(k); }, "k", 7));
            const s = new Set([1, 2]);
            out.push(run(function (v) { return s.has(v); }, 1));
            out.push(run(function (v) { return new Set([1, 2]).delete(v); }, 2));
            out.push(run(function (v) { return [1, 2, 3].reverse().join(""); }));
            out.push(run(function (v) { return [1, 2, 3].toReversed().join(""); }));
            out.push(run(function (v) { return [1, 2, 3].shift(); }));
            out.push(run(function (v) { return (1.23456).toFixed(v); }, 2));
            out.push(run(function (v) { return Math.trunc(v); }, -4.7));
            out.push(run(function (v) { return /a(b)/.test(v); }, "cab"));
            out.push(run(function (v) { return ({ x: 1 }).hasOwnProperty(v); }, "x"));
            out.push(run(function (v) { return isNaN(v); }, "nope"));
            out.push(run(function (v) { return isFinite(v); }, "12"));
            out.push(run(function (v) { return typeof Date.now(); }));
            let brand = "";
            try { const g = Map.prototype.get; g({}, 1); g({}, 1); } catch (e) { brand = e.constructor.name; }
            out.push(brand);
            out.join("|");
            """).AsString();

        result.Should().Be("7|true|true|321|321|1|1.23|-4|true|true|true|true|number|TypeError");
    }

    /// <summary>
    /// The audit every migration from a raw <c>JsCallArguments</c> body to positional parameters has to
    /// pass: the lane pads its argument registers with <c>Undefined</c>, so an absent argument and an
    /// explicit <c>undefined</c> become indistinguishable inside the body. Each pair below must therefore
    /// already agree — and each is checked cold as well as warm, since only the warm call takes the lane.
    /// </summary>
    [Theory]
    [InlineData("\"abcdef\".includes(\"cd\")", "true")]
    [InlineData("\"abcdef\".includes(\"cd\", undefined)", "true")]
    [InlineData("\"abcdef\".includes(\"cd\", 3)", "false")]
    [InlineData("\"abcdef\".endsWith(\"ef\")", "true")]
    [InlineData("\"abcdef\".endsWith(\"ef\", undefined)", "true")]
    [InlineData("\"abcdef\".endsWith(\"cd\", 4)", "true")]
    [InlineData("String([1,2,3].indexOf(2))", "1")]
    [InlineData("String([1,2,3].indexOf(2, undefined))", "1")]
    [InlineData("String([1,2,3].indexOf(2, 2))", "-1")]
    [InlineData("String([1,2,3].some(function (v) { return v === 2; }))", "true")]
    [InlineData("String([1,2,3].some(function (v) { return v === 2; }, undefined))", "true")]
    [InlineData("String([1,2,3].find(function (v) { return v === 2; }))", "2")]
    [InlineData("String([1,2,3].find(function (v) { return v === 2; }, undefined))", "2")]
    [InlineData("String([1,2,3].findIndex(function (v) { return v === 2; }))", "1")]
    [InlineData("String([1,2,3].findIndex(function (v) { return v === 2; }, undefined))", "1")]
    [InlineData("(255).toString()", "255")]
    [InlineData("(255).toString(undefined)", "255")]
    [InlineData("(255).toString(16)", "ff")]
    public void AnAbsentOptionalArgumentMatchesAnExplicitUndefined(string expression, string expected)
    {
        var engine = new Engine();

        engine.Evaluate($"function f() {{ return String({expression}); }} f();").AsString().Should().Be(expected);
        engine.Evaluate($"function g() {{ return String({expression}); }} g(); g();").AsString().Should().Be(expected);
    }

    /// <summary>
    /// The sibling of <see cref="NewlyLaneEligibleBuiltinsAgreeWarmAndCold"/> for the built-ins whose
    /// bodies had to be migrated off <c>JsCallArguments</c> to become lane-expressible. Callback-taking
    /// methods are included with a <c>thisArg</c>, since that is the second argument register.
    /// </summary>
    [Fact]
    public void MigratedLaneEligibleBuiltinsAgreeWarmAndCold()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const out = [];
            function run(f, a, b) {
                const cold = f(a, b);
                f(a, b);
                return f(a, b) === cold ? cold : "DRIFT";
            }
            out.push(run(function (v, p) { return "abcdef".includes(v, p); }, "cd", 1));
            out.push(run(function (v, p) { return "abcdef".endsWith(v, p); }, "cd", 4));
            out.push(run(function (v, p) { return [1, 2, 3].indexOf(v, p); }, 3, 1));
            const box = { limit: 2 };
            out.push(run(function (f, t) { return [1, 2, 3].some(f, t); }, function (v) { return v > this.limit; }, box));
            out.push(run(function (f, t) { return [1, 2, 3].find(f, t); }, function (v) { return v > this.limit; }, box));
            out.push(run(function (f, t) { return [1, 2, 3].findIndex(f, t); }, function (v) { return v > this.limit; }, box));
            out.push(run(function (v) { return (255).toString(v); }, 16));
            let brand = "";
            try { const t = Number.prototype.toString; t.call({}); t.call({}); } catch (e) { brand = e.constructor.name; }
            out.push(brand);
            let uncallable = "";
            try { [1].find(1); [1].find(1); } catch (e) { uncallable = e.constructor.name; }
            out.push(uncallable);
            out.join("|");
            """).AsString();

        result.Should().Be("true|true|2|true|3|2|ff|TypeError|TypeError");
    }

    /// <summary>
    /// Step 7 of <c>String.prototype.endsWith</c> keys on <c>endPosition</c> being the value
    /// <c>undefined</c>, not on the argument being absent. Jint read the argument with a non-undefined
    /// default that only applied when it was missing, so an explicit <c>undefined</c> was coerced to 0 and
    /// searched an empty prefix. test262 does not discriminate the two (its only <c>undefined</c> case
    /// uses an empty search string, which matches at any position).
    /// </summary>
    [Fact]
    public void EndsWithTreatsAnExplicitUndefinedEndPositionAsTheStringLength()
    {
        var engine = new Engine();

        engine.Evaluate("'abc'.endsWith('c', undefined)").AsBoolean().Should().BeTrue();
        engine.Evaluate("'abc'.endsWith('c')").AsBoolean().Should().BeTrue();
        engine.Evaluate("'abc'.endsWith('b', undefined)").AsBoolean().Should().BeFalse();
        // an explicit numeric position is unaffected
        engine.Evaluate("'abc'.endsWith('b', 2)").AsBoolean().Should().BeTrue();
        engine.Evaluate("'abc'.endsWith('c', 0)").AsBoolean().Should().BeFalse();
        // null still coerces to 0, per step 7's else branch
        engine.Evaluate("'abc'.endsWith('c', null)").AsBoolean().Should().BeFalse();
        engine.Evaluate("'abc'.endsWith('', null)").AsBoolean().Should().BeTrue();
    }

#if !NETFRAMEWORK // Math.f16round needs System.Half, which .NET Framework does not have
    /// <summary>
    /// <c>Math.f16round</c> must return a Number for every input. It used to hand back the argument
    /// itself on the infinity / signed-zero branches, which is only the same value when the argument
    /// was already a number.
    /// </summary>
    [Fact]
    public void F16RoundAlwaysReturnsANumber()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const inf = Math.f16round({ valueOf: function () { return Infinity; } });
            const negZero = Math.f16round({ valueOf: function () { return -0; } });
            const zero = Math.f16round({ valueOf: function () { return 0; } });
            const warm = Math.f16round(1.337) === Math.f16round(1.337);
            [typeof inf, inf, typeof negZero, 1 / negZero, typeof zero, 1 / zero, warm].join(",");
            """).AsString();

        result.Should().Be("number,Infinity,number,-Infinity,number,Infinity,true");
    }
#endif

    /// <summary>
    /// A Prepared&lt;Script&gt; shares its handler tree across engines, so a call-site cache populated
    /// by one engine must never be honoured for another engine's built-in instances.
    /// </summary>
    [Fact]
    public void APreparedScriptReusedAcrossEnginesDoesNotLeakItsCachedCallee()
    {
        var prepared = Engine.PrepareScript("""
            function f(x) { return Math.abs(x); }
            f(-1); f(-2);
            f(-3);
            """);

        var first = new Engine();
        first.Evaluate(prepared).AsNumber().Should().Be(3);

        // Second engine patches its own Math before running the very same prepared nodes.
        var second = new Engine();
        second.Execute("Math.abs = function () { return 123; };");
        second.Evaluate(prepared).AsNumber().Should().Be(123);

        // First engine must be unaffected by the second engine's patch.
        first.Evaluate(prepared).AsNumber().Should().Be(3);
    }

    [Fact]
    public void ArgumentsAreEvaluatedLeftToRightExactlyOnce()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const order = [];
            function a() { order.push("a"); return 0; }
            function b() { order.push("b"); return 2; }
            const out = "hello".substring(a(), b());
            out + "|" + order.join(",");
            """);

        result.AsString().Should().Be("he|a,b");
    }

    /// <summary>
    /// Spread cannot be served by a fixed-arity lane; it must fall through and stay correct.
    /// </summary>
    [Fact]
    public void SpreadArgumentsStillWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const args = [1, 3];
            "abcdef".substring(...args) + "," + Math.max(...[1, 9, 4]);
            """);

        result.AsString().Should().Be("bc,9");
    }

    [Fact]
    public void OptionalCallAndOptionalChainingStillWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const s = "abc";
            const missing = undefined;
            (s?.charAt(1) ?? "?") + "," + (missing?.charAt(1) ?? "?");
            """);

        result.AsString().Should().Be("b,?");
    }

    /// <summary>
    /// Generator frames suspend mid-argument-list, which requires ExpressionCache's resume buffer;
    /// the fast lane must decline there rather than evaluating arguments straight into locals.
    /// </summary>
    [Fact]
    public void ArgumentsContainingYieldStillResumeCorrectly()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* g() {
                const r = "abcdef".substring(yield 1, yield 2);
                return r;
            }
            const it = g();
            it.next();
            it.next(1);
            it.next(4);
            """);

        result.Get("value").AsString().Should().Be("bcd");
    }

    [Fact]
    public void CallAndApplyAndBindAreUnaffected()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const cc = String.prototype.charCodeAt;
            const bound = cc.bind("abc");
            cc.call("abc", 0) + "," + cc.apply("abc", [1]) + "," + bound(2);
            """);

        result.AsString().Should().Be("97,98,99");
    }

    [Fact]
    public void ZeroArgumentDateGettersAgreeWithTheirFramedForm()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const d = new Date(Date.UTC(2021, 4, 17, 8, 30, 15));
            function viaSite(x) { return x.getUTCFullYear(); }
            viaSite(d); viaSite(d);
            const direct = Date.prototype.getUTCFullYear.call(d);
            viaSite(d) === direct && direct === 2021;
            """);

        result.AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// A receiver of the wrong brand must still produce the spec TypeError, with the built-in's own
    /// frame present, rather than being silently served by a receiver-guarded fast path.
    /// </summary>
    [Fact]
    public void AWrongBrandReceiverStillThrowsTypeError()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let message = "";
            try { Date.prototype.getUTCFullYear.call({}); }
            catch (e) { message = e.constructor.name; }
            message;
            """);

        result.AsString().Should().Be("TypeError");
    }
}
