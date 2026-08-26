#nullable enable

using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Interpreter;

namespace Jint.Tests.Runtime;

/// <summary>
/// A function whose body declares top-level <c>let</c>/<c>const</c> stores those bindings in the
/// fixed-slot environment and is instantiated by <c>FunctionDeclarationInstantiation</c>'s fast arm
/// (and therefore reaches the register-argument call lane). What makes that safe is that a slot can
/// express the two things a lexical binding needs and a <c>var</c> does not: the temporal dead zone
/// (an uninitialized binding, so an early read is a ReferenceError rather than <c>undefined</c>) and
/// immutability (assigning a <c>const</c> is a TypeError).
///
/// Those semantics fail SILENTLY if the slot fill is wrong — a TDZ slot mistakenly pre-filled with
/// <c>undefined</c> still passes every happy-path test — so they are what these tests pin, on both
/// the cold first call and the warmed call site, and across the environment/slot reuse that returns
/// the same storage to the next call.
/// </summary>
public class FixedSlotLexicalBindingTests
{
    private static JintFunctionDefinition.State StateOf(Engine engine, string name)
    {
        var function = (ScriptFunction) engine.GetValue(name);
        return function._functionDefinition!.Initialize();
    }

    [Test]
    public void ALexicalDeclarationDoesNotDisqualifyTheFixedSlotFastPath()
    {
        var engine = new Engine();
        engine.Execute("function withLet(a) { let x = a; const y = 1; return x + y; }");

        var state = StateOf(engine, "withLet");

        state.UseFixedSlots.Should().BeTrue();
        // The point of the change: a top-level let/const used to force the general instantiation arm.
        state.CanUseFastFDI.Should().BeTrue();
        state.SupportsRegisterCall.Should().BeTrue();
        // 1 parameter + 0 vars + 2 lexical bindings, lexical region last. The template covers every
        // non-parameter slot, so its length is SlotNames.Length - ParameterSlotCount.
        state.ParameterSlotCount.Should().Be(1);
        state.VarSlotCount.Should().Be(0);
        state.NonParameterSlotTemplate.Should().NotBeNull();
        state.NonParameterSlotTemplate!.Length.Should().Be(2);
        state.SlotNames!.Length.Should().Be(3);

        // The lexical region is the temporal dead zone: no value at all, and const is immutable.
        state.NonParameterSlotTemplate[0].IsInitialized().Should().BeFalse();
        state.NonParameterSlotTemplate[0].Mutable.Should().BeTrue();
        state.NonParameterSlotTemplate[1].IsInitialized().Should().BeFalse();
        state.NonParameterSlotTemplate[1].Mutable.Should().BeFalse();

        engine.Evaluate("withLet(41)").AsNumber().Should().Be(42);
    }

    [Test]
    public void AFunctionWithoutLexicalDeclarationsGetsAnAllUndefinedTemplate()
    {
        var engine = new Engine();
        engine.Execute("function onlyVars(a) { var x = a; var y = 1; return x + y; }");

        var state = StateOf(engine, "onlyVars");

        state.CanUseFastFDI.Should().BeTrue();
        // The template exists for every fixed-slot function — that is what lets the instantiation arm
        // be one unconditional copy — and a hoisted var's entry is exactly what the arm used to
        // construct in place: initialized to undefined, mutable.
        state.NonParameterSlotTemplate.Should().NotBeNull();
        state.NonParameterSlotTemplate!.Length.Should().Be(2);
        foreach (var binding in state.NonParameterSlotTemplate)
        {
            binding.IsInitialized().Should().BeTrue();
            binding.Value.Should().Be(JsValue.Undefined);
            binding.Mutable.Should().BeTrue();
            binding.CanBeDeleted.Should().BeFalse();
            binding.Strict.Should().BeFalse();
        }

        engine.Evaluate("onlyVars(41)").AsNumber().Should().Be(42);
    }

    [Test]
    public void AParameterOnlyFunctionGetsAnEmptyTemplate()
    {
        var engine = new Engine();
        engine.Execute("function add(a, b) { return a + b; }");

        var state = StateOf(engine, "add");

        state.CanUseFastFDI.Should().BeTrue();
        state.ParameterSlotCount.Should().Be(2);
        // Every slot is a parameter, so the arm's copy has nothing to do — and an empty template is
        // Array.Empty, so nothing is allocated for it either.
        state.NonParameterSlotTemplate.Should().NotBeNull();
        state.NonParameterSlotTemplate!.Length.Should().Be(0);

        engine.Evaluate("add(40, 2)").AsNumber().Should().Be(42);
    }

    [Test]
    public void AnInnerFunctionDeclarationStillFallsBackToTheGeneralArm()
    {
        var engine = new Engine();
        engine.Execute("function withInner(a) { function inner() { return a; } return inner(); }");

        var state = StateOf(engine, "withInner");

        // Function/class declarations are not slot-storable; the fallback is unchanged.
        state.UseFixedSlots.Should().BeFalse();
        state.CanUseFastFDI.Should().BeFalse();
    }

    [Test]
    public void ReadingALetBindingBeforeItsDeclarationThrowsReferenceError()
    {
        var engine = new Engine();
        engine.Execute("function f(a) { var seen = x; let x = a; return seen; }");

        // Not `undefined`: the binding exists from function entry but holds no value yet.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("f(1)"));
        engine.Evaluate("(function () { try { f(1); } catch (e) { return e instanceof ReferenceError; } })()")
            .AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ReadingAConstBindingBeforeItsDeclarationThrowsReferenceError()
    {
        var engine = new Engine();
        engine.Execute("function f(a) { var seen = c; const c = a; return seen; }");

        engine.Evaluate("(function () { try { f(1); } catch (e) { return e instanceof ReferenceError; } })()")
            .AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TypeofOnALetBindingBeforeItsDeclarationThrowsUnlikeAnUndeclaredName()
    {
        var engine = new Engine();
        engine.Execute("function f(a) { var t = typeof x; let x = a; return t; }");

        // typeof swallows the error only for an UNRESOLVABLE name; a TDZ binding resolves.
        engine.Evaluate("(function () { try { f(1); } catch (e) { return e instanceof ReferenceError; } })()")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof neverDeclaredAnywhere").AsString().Should().Be("undefined");
    }

    [Test]
    public void ALetBindingWithNoInitializerIsUndefinedOnlyAfterItsDeclaration()
    {
        var engine = new Engine();
        engine.Execute("function before(a) { var seen = x; let x; return seen; }");
        engine.Execute("function after(a) { let x; return x === undefined; }");

        engine.Evaluate("(function () { try { before(1); } catch (e) { return e instanceof ReferenceError; } })()")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("after(1)").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void AssigningToAConstSlotThrowsTypeErrorInBothModes()
    {
        var engine = new Engine();
        engine.Execute("function sloppy(a) { const c = a; c = 2; return c; }");
        engine.Execute("function strict(a) { 'use strict'; const c = a; c = 2; return c; }");

        foreach (var name in new[] { "sloppy", "strict" })
        {
            engine.Evaluate($"(function () {{ try {{ {name}(1); }} catch (e) {{ return e instanceof TypeError; }} }})()")
                .AsBoolean().Should().BeTrue($"{name} must reject the assignment");
        }
    }

    [Test]
    public void TheTemporalDeadZoneIsReEstablishedOnEveryCall()
    {
        // The environment and its Binding[] are pooled and handed to the next call, so a slot that
        // was initialized by the previous call must be back in the TDZ for this one.
        var engine = new Engine();
        engine.Execute(@"
            function probe(a) {
                var seen;
                try { seen = x; } catch (e) { seen = e.constructor.name; }
                let x = a;
                return seen;
            }
            function results(n) {
                var out = [];
                for (var i = 0; i < n; i++) { out.push(probe(i)); }
                return out.join(',');
            }");

        engine.Evaluate("results(5)").AsString().Should().Be("ReferenceError,ReferenceError,ReferenceError,ReferenceError,ReferenceError");
    }

    [Test]
    public void ConstnessSurvivesSlotReuseAcrossCalls()
    {
        var engine = new Engine();
        engine.Execute(@"
            function probe(a) {
                const c = a;
                try { c = a + 1; } catch (e) { return e.constructor.name; }
                return 'assigned';
            }
            function results(n) {
                var out = [];
                for (var i = 0; i < n; i++) { out.push(probe(i)); }
                return out.join(',');
            }");

        engine.Evaluate("results(4)").AsString().Should().Be("TypeError,TypeError,TypeError,TypeError");
    }

    [Test]
    public void TheTemporalDeadZoneHoldsThroughTheRegisterArgumentLane()
    {
        // The register lane only arms on the second-and-later evaluation of a call site with 1-4
        // non-spread arguments, so the loop is what warms it; the verdict must not change once warm.
        var engine = new Engine();
        engine.Execute(@"
            function probe(a, b) {
                var seen;
                try { seen = x; } catch (e) { seen = e.constructor.name; }
                let x = a + b;
                const y = x;
                return seen + ':' + y;
            }
            function results(n) {
                var out = [];
                for (var i = 0; i < n; i++) { out.push(probe(i, 1)); }
                return out.join('|');
            }");

        var state = StateOf(engine, "probe");
        state.SupportsRegisterCall.Should().BeTrue();

        engine.Evaluate("results(3)").AsString().Should().Be("ReferenceError:1|ReferenceError:2|ReferenceError:3");
    }

    [Test]
    public void AClosureCapturingALetSlotSeesTheInitializedValue()
    {
        var engine = new Engine();
        engine.Execute("function make(a) { let x = a * 2; const tag = 'v'; return function () { return tag + x; }; }");

        engine.Evaluate("make(21)()").AsString().Should().Be("v42");
        // Two live closures must not share a slot array.
        engine.Evaluate("var f1 = make(1), f2 = make(2); f1() + ',' + f2()").AsString().Should().Be("v2,v4");
    }

    [Test]
    public void AClosureCapturingALetSlotCannotObserveItInTheDeadZone()
    {
        var engine = new Engine();
        engine.Execute(@"
            function make(a) {
                var peek = function () { return x; };
                var early;
                try { early = peek(); } catch (e) { early = e.constructor.name; }
                let x = a;
                return early + ':' + peek();
            }");

        engine.Evaluate("make(7)").AsString().Should().Be("ReferenceError:7");
    }

    [Test]
    public void ParametersVarsAndLexicalBindingsLandInTheirOwnSlots()
    {
        var engine = new Engine();
        engine.Execute("function mix(a, b) { var v = a; let l = b; const c = a + b; v = v + l + c; return [a, b, v, l, c].join(','); }");

        var state = StateOf(engine, "mix");

        // 2 parameters + 1 var + 2 lexical: the template is the var and lexical regions, in that order.
        state.ParameterSlotCount.Should().Be(2);
        state.VarSlotCount.Should().Be(1);
        state.NonParameterSlotTemplate!.Length.Should().Be(3);
        state.NonParameterSlotTemplate[0].IsInitialized().Should().BeTrue();
        state.NonParameterSlotTemplate[1].IsInitialized().Should().BeFalse();
        state.NonParameterSlotTemplate[1].Mutable.Should().BeTrue();
        state.NonParameterSlotTemplate[2].IsInitialized().Should().BeFalse();
        state.NonParameterSlotTemplate[2].Mutable.Should().BeFalse();

        engine.Evaluate("mix(1, 2)").AsString().Should().Be("1,2,6,2,3");
    }

    [Test]
    public void RecursiveFramesEachGetTheirOwnDeadZone()
    {
        // Direct recursion pools environments through RecursiveEnvPool, so several frames are live
        // at once and each must start with its own uninitialized lexical slots.
        var engine = new Engine();
        engine.Execute(@"
            function fib(n) {
                var seen;
                try { seen = memo; } catch (e) { seen = 1; }
                const memo = n < 2 ? n : fib(n - 1) + fib(n - 2);
                return memo + (seen - 1);
            }");

        engine.Evaluate("fib(15)").AsNumber().Should().Be(610);
    }

    [Test]
    public void GeneratorsAndAsyncFunctionsKeepTheirDeadZone()
    {
        var engine = new Engine();
        engine.Execute(@"
            function* gen(a) {
                var seen;
                try { seen = x; } catch (e) { seen = e.constructor.name; }
                let x = a;
                yield seen;
                yield x;
            }
            async function asy(a) {
                var seen;
                try { seen = x; } catch (e) { seen = e.constructor.name; }
                const x = a;
                return seen + ':' + x;
            }");

        engine.Evaluate("var it = gen(9); it.next().value + ':' + it.next().value").AsString().Should().Be("ReferenceError:9");

        engine.Execute("var r; asy(9).then(function (v) { r = v; });");
        engine.Evaluate("r").AsString().Should().Be("ReferenceError:9");
    }

    [Test]
    public void ResumingAfterAnAwaitDoesNotPutInitializedSlotsBackIntoTheDeadZone()
    {
        // Instantiation must happen once per call, not once per resumption: re-running it would
        // stamp the template over slots the pre-await part of the body already initialized.
        var engine = new Engine();
        engine.Execute(@"
            var result;
            async function f(n) {
                let x = n + 1;
                const c = 10;
                var v = 5;
                await Promise.resolve(0);
                return x + c + v;
            }
            f(1).then(function (r) { result = r; }, function (e) { result = 'threw:' + e; });");

        engine.Evaluate("result").AsNumber().Should().Be(17);
    }

    [Test]
    public void TheDeadZoneIsObservableAcrossAnAwaitBoundary()
    {
        var engine = new Engine();
        engine.Execute(@"
            var result;
            async function f(n) {
                var seen;
                try { seen = x; } catch (e) { seen = e.constructor.name; }
                await Promise.resolve(0);
                let x = n;
                return seen + ':' + x;
            }
            f(3).then(function (r) { result = r; }, function (e) { result = 'threw:' + e; });");

        engine.Evaluate("result").AsString().Should().Be("ReferenceError:3");
    }

    [Test]
    public void ResumingAGeneratorKeepsItsInitializedLexicalSlots()
    {
        var engine = new Engine();
        engine.Execute("function* g(n) { let x = n + 1; const c = 10; yield x; yield x + c; }");

        engine.Evaluate("var it = g(1); it.next().value + ',' + it.next().value").AsString().Should().Be("2,12");
    }

    [Test]
    public void ATopLevelUsingDeclarationStillRegistersItsDisposal()
    {
        // `using` is lexically scoped like let, so it takes a slot too; the disposal is registered
        // when the declaration initializes the binding, and run by the call's DisposeResources.
        var engine = new Engine();
        engine.Execute(@"
            var disposed = false;
            function f(a) {
                using r = { [Symbol.dispose]: function () { disposed = true; } };
                return a + ':' + disposed;
            }
            var during = f(1);");

        engine.Evaluate("during").AsString().Should().Be("1:false");
        engine.Evaluate("disposed").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ADestructuringLexicalDeclarationKeepsItsDeadZone()
    {
        // Destructuring targets are excluded from JintVariableDeclaration's slot lane, so they
        // initialize through the Reference path — the slot must still start uninitialized.
        var engine = new Engine();
        engine.Execute(@"
            function f(a) {
                var seen;
                try { seen = p; } catch (e) { seen = e.constructor.name; }
                const { p, q } = a;
                return seen + ':' + p + ':' + q;
            }");

        engine.Evaluate("f({ p: 1, q: 2 })").AsString().Should().Be("ReferenceError:1:2");
    }

    [Test]
    public void ALetSlotShadowsAnOuterBindingWhileStillInTheDeadZone()
    {
        // The shadowed outer name must NOT be readable from inside the function before the inner
        // declaration executes — the slot exists from function entry, it is merely uninitialized.
        var engine = new Engine();
        engine.Execute(@"
            var shared = 'outer';
            function f(a) {
                var seen;
                try { seen = shared; } catch (e) { seen = e.constructor.name; }
                let shared = a;
                return seen + ':' + shared;
            }");

        engine.Evaluate("f('inner')").AsString().Should().Be("ReferenceError:inner");
    }

    [Test]
    public void AThrowBeforeTheDeclarationLeavesNoInitializedValueForTheNextCall()
    {
        var engine = new Engine();
        engine.Execute(@"
            function f(a) {
                if (a === 0) { throw new Error('early'); }
                let x = a;
                return x;
            }
            function probe(a) {
                var seen;
                try { seen = x; } catch (e) { seen = e.constructor.name; }
                let x = a;
                return seen;
            }
            function run() {
                var out = [];
                try { f(0); } catch (e) { out.push(e.message); }
                out.push(f(1));
                try { f(0); } catch (e) { out.push(e.message); }
                out.push(probe(2));
                return out.join(',');
            }");

        engine.Evaluate("run()").AsString().Should().Be("early,1,early,ReferenceError");
    }
}
