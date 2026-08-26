#nullable enable

using Jint.Native.Function;
using Jint.Runtime.Interpreter;

namespace Jint.Tests.Runtime;

/// <summary>
/// A direct eval resolves <c>arguments</c> lexically, so it reaches the enclosing function's
/// arguments object even though the token appears nowhere in that function's source — in strict
/// mode as much as in sloppy mode, the strict eval getting a variable environment of its own but
/// not a fresh <c>arguments</c>.
///
/// Which matters because the decision to materialize the arguments object at all is a syntactic
/// scan for the token, and that scan cannot see into an eval string. The direct eval call site is
/// the signal it has to take instead, and these tests are that signal's regression net: the
/// matrix below is checked against node, the gate assertions pin what the signal costs, and the
/// no-eval rows pin that it costs nothing to everything else.
/// </summary>
public class DirectEvalArgumentsTests
{
    private static JintFunctionDefinition.State StateOf(Engine engine, string name)
    {
        var function = (ScriptFunction) engine.GetValue(name);
        return function._functionDefinition!.Initialize();
    }

    // Sloppy mode: nothing but a direct eval names the arguments object.
    [TestCase("function f() { return eval('arguments').length; } f(1, 2)", "2")]
    [TestCase("function f() { let x = 1; return eval('arguments').length + x; } f(1, 2)", "3")]
    [TestCase("function f() { var x = 1; return eval('arguments').length + x; } f(1, 2)", "3")]
    [TestCase("function f() { let x = 1; const y = 2; return eval('arguments').length + x + y; } f(1, 2)", "5")]
    [TestCase("function f() { { return eval('arguments').length; } } f(1, 2)", "2")]
    // An arrow has no arguments object of its own, so its direct eval names the enclosing one.
    [TestCase("function f() { const g = () => eval('arguments').length; return g(); } f(1, 2)", "2")]
    [TestCase("function f() { const g = () => () => eval('arguments').length; return g()(); } f(1, 2)", "2")]
    // A parameter default is evaluated after the arguments object exists.
    [TestCase("function f(a = eval('arguments').length) { return a; } f()", "0")]
    // The eval call site is an argument of another call — the scan has to walk into it.
    [TestCase("function id(x) { return x; } function f() { return id(eval('arguments').length); } f(1, 2)", "2")]
    // Sloppy gets the mapped flavour: the parameter and arguments[0] are the same binding.
    [TestCase("function f(a) { a = 42; return eval('arguments[0]'); } f(1)", "42")]
    [TestCase("function f() { return eval('var q = arguments.length; q'); } f(1, 2)", "2")]
    [TestCase("function f() { return typeof eval('arguments.callee'); } f(1)", "function")]
    [TestCase("function f() { var n = 0; for (var i = 0; i < 3; i++) { n += eval('arguments').length; } return n; } f(1, 2)", "6")]
    [TestCase("var o = { m() { return eval('arguments').length; } }; o.m(1, 2)", "2")]
    [TestCase("function* f() { yield eval('arguments').length; } f(1, 2).next().value", "2")]
    // A non-simple parameter list forces the unmapped flavour even in sloppy mode.
    [TestCase("function f(...xs) { return eval('arguments').length; } f(1, 2)", "2")]

    // Strict mode: the eval gets its own variable environment, but not a fresh arguments object.
    [TestCase("function f() { 'use strict'; return eval('arguments').length; } f(1, 2)", "2")]
    [TestCase("function f() { 'use strict'; let x = 1; return eval('arguments').length + x; } f(1, 2)", "3")]
    [TestCase("function f() { 'use strict'; var x = 1; return eval('arguments').length + x; } f(1, 2)", "3")]
    [TestCase("function f() { 'use strict'; let x = 1; const y = 2; return eval('arguments').length + x + y; } f(1, 2)", "5")]
    [TestCase("function f() { 'use strict'; { return eval('arguments').length; } } f(1, 2)", "2")]
    [TestCase("function f() { 'use strict'; const g = () => eval('arguments').length; return g(); } f(1, 2)", "2")]
    [TestCase("function f() { 'use strict'; return eval('var q = arguments.length; q'); } f(1, 2)", "2")]
    [TestCase("function f() { 'use strict'; var n = 0; for (var i = 0; i < 3; i++) { n += eval('arguments').length; } return n; } f(1, 2)", "6")]
    // Strict gets the unmapped flavour: writing the parameter leaves arguments[0] alone.
    [TestCase("function f(a) { 'use strict'; a = 42; return eval('arguments[0]'); } f(1)", "1")]
    // A class body is strict code.
    [TestCase("class C { m() { return eval('arguments').length; } } new C().m(1, 2)", "2")]
    [TestCase("class C { static s() { return eval('arguments').length; } } C.s(1, 2)", "2")]
    [TestCase("'use strict'; function f() { let x = 1; return eval('arguments').length + x; } f(1, 2)", "3")]
    [TestCase("'use strict'; function f(a = eval('arguments').length) { return a; } f()", "0")]
    [TestCase("'use strict'; function f(a, b) { let x = 1; const y = 2; var z = 3; return [a, b, x, y, z, eval('arguments').length].join(','); } f(8, 9)", "8,9,1,2,3,2")]

    // Shadowed: the name resolves to the shadowing binding, and no arguments object is involved.
    [TestCase("function f(arguments) { return eval('arguments'); } f(7)", "7")]
    [TestCase("function f() { function arguments() { } return typeof eval('arguments'); } f(1, 2)", "function")]
    [TestCase("function f() { let arguments = 5; return eval('arguments'); } f(1, 2)", "5")]
    [TestCase("function f() { var arguments = 5; return eval('arguments'); } f(1, 2)", "5")]

    // A nested function has an arguments object of its own, and that is the one its eval names.
    [TestCase("function f() { return (function () { return eval('arguments').length; })(1, 2, 3); } f()", "3")]
    [TestCase("function f() { function g() { return eval('arguments').length; } return g(1, 2, 3); } f()", "3")]
    [TestCase("function f() { 'use strict'; return (function () { return eval('arguments').length; })(1, 2, 3); } f()", "3")]

    // An indirect eval runs at global scope, where there is no arguments object to reach.
    [TestCase("function f() { const e = eval; return e('typeof arguments'); } f(1, 2)", "undefined")]
    public void ADirectEvalReachesTheEnclosingFunctionsArgumentsObject(string source, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).ToString().Should().Be(expected);
    }

    [Test]
    public void AnIndirectEvalStillFindsNoArgumentsObject()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            function f() {
                const e = eval;
                try { e('arguments'); return 'no throw'; } catch (err) { return err.name; }
            }
            f(1, 2);
            """);

        result.ToString().Should().Be("ReferenceError");
    }

    [Test]
    public void AWarmedCallSiteKeepsSeeingItsOwnArguments()
    {
        var engine = new Engine();
        engine.Execute("function f() { 'use strict'; return eval('arguments').length; }");

        // The handler-tree caches engage on the second evaluation, and the arguments object is
        // rebuilt per call — a warmed site must not answer with the previous call's object.
        engine.Evaluate("f(1, 2)").ToString().Should().Be("2");
        engine.Evaluate("f(1, 2, 3)").ToString().Should().Be("3");
        engine.Evaluate("f()").ToString().Should().Be("0");
    }

    [Test]
    public void AnAsyncFunctionsDirectEvalReachesItsArguments()
    {
        var engine = new Engine();

        engine.Evaluate("(async function f() { return eval('arguments').length; })(1, 2)")
            .UnwrapIfPromise()
            .ToString()
            .Should()
            .Be("2");
    }

    // A generator or async function's argument array can be a pooled one, and its arguments object
    // outlives the suspension — which is what RequiresInputArgumentsOwnership copies for, and the
    // direct eval is now what asks for the copy.
    [TestCase("function* f() { yield 0; return eval('arguments').length; } var it = f(1, 2, 3, 4); it.next(); it.next().value", "4")]
    [TestCase("function* f() { 'use strict'; yield 0; return eval('arguments').length; } var it = f(1, 2, 3, 4, 5); it.next(); it.next().value", "5")]
    // Sloppy mapping is two-way: the eval writes through the arguments object into the parameter.
    [TestCase("function f(a) { eval('arguments[0] = 9'); return a; } f(1)", "9")]
    [TestCase("function f(a) { 'use strict'; eval('arguments[0] = 9'); return a; } f(1)", "1")]
    // Several frames of one function are live at once, each with its own arguments object.
    [TestCase("function f(n) { 'use strict'; if (n === 0) { return eval('arguments').length; } return f(n - 1) + eval('arguments[0]'); } f(3)", "7")]
    // A warm call site reusing its environment and its pooled argument array, 200 times over.
    [TestCase("function f(a, b) { 'use strict'; return eval('arguments').length + a + b; } var t = 0; for (var i = 0; i < 200; i++) { t += f(1, 2); } t", "1000")]
    // The arguments object escapes the call that made it.
    [TestCase("function f(a) { 'use strict'; return eval('arguments'); } var a1 = f(1, 2), a2 = f(3); [a1.length, a1[0], a2.length, a2[0]].join(',')", "2,1,1,3")]
    // The Function constructor's one-shot instances share one definition and one cached environment.
    [TestCase("var f = new Function(\"return eval('arguments').length;\"); f(1, 2) + ',' + f(1, 2, 3)", "2,3")]
    [TestCase("function f(o) { with (o) { return eval('arguments').length; } } f({ x: 1 }, 2)", "2")]
    [TestCase("function f(a, b = eval('arguments').length + a) { return b; } f(10)", "11")]
    public void TheArgumentsObjectSurvivesWhatTheCallLaneDoesToIt(string source, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).ToString().Should().Be(expected);
    }

    [Test]
    public void AnAsyncArrowReachesTheEnclosingFunctionsArguments()
    {
        var engine = new Engine();

        engine.Evaluate("function f() { const g = async () => eval('arguments').length; return g(); } f(1, 2)")
            .UnwrapIfPromise()
            .ToString()
            .Should()
            .Be("2");
    }

    [Test]
    public void AStrictEngineIsNoDifferentFromAStrictFunction()
    {
        var engine = new Engine(static options => options.Strict = true);

        engine.Evaluate("function f(a) { a = 42; return eval('arguments[0]') + ',' + eval('arguments').length; } f(1, 2)")
            .ToString()
            .Should()
            .Be("1,2");
    }

    [Test]
    public void ADirectEvalMakesTheArgumentsObjectNeeded()
    {
        var engine = new Engine();
        engine.Execute("""
            function sloppy() { return eval('1'); }
            function strict() { 'use strict'; return eval('1'); }
            """);

        var sloppy = StateOf(engine, "sloppy");
        sloppy.ArgumentsObjectNeeded.Should().BeTrue();
        sloppy.NeedsEvalContext.Should().BeTrue();

        // The strict half is the one the syntactic scan used to let through: a strict function
        // needs no eval context, so nothing else was left to notice the eval.
        var strict = StateOf(engine, "strict");
        strict.ArgumentsObjectNeeded.Should().BeTrue();
        strict.NeedsEvalContext.Should().BeFalse();
    }

    [Test]
    public void ADirectEvalCostsTheFastInstantiationArms()
    {
        var engine = new Engine();
        engine.Execute("""
            function withEval(a) { 'use strict'; let x = 1; return eval('x') + a; }
            function withoutEval(a) { 'use strict'; let x = 1; return x + a; }
            """);

        var withEval = StateOf(engine, "withEval");
        withEval.ArgumentsObjectNeeded.Should().BeTrue();
        withEval.UseFixedSlots.Should().BeFalse();
        withEval.CanUseFastFDI.Should().BeFalse();
        withEval.SupportsRegisterCall.Should().BeFalse();

        // ...and costs them to nothing else. This is the hot path: a function with no direct eval
        // keeps every gate it had.
        var withoutEval = StateOf(engine, "withoutEval");
        withoutEval.ArgumentsObjectNeeded.Should().BeFalse();
        withoutEval.UseFixedSlots.Should().BeTrue();
        withoutEval.CanUseFastFDI.Should().BeTrue();
        withoutEval.SupportsRegisterCall.Should().BeTrue();
    }

    [Test]
    public void AnEvalFreeFunctionKeepsTheEmptyInstantiationArm()
    {
        var engine = new Engine();
        engine.Execute("""
            function leaf() { 'use strict'; return 1; }
            function evalLeaf() { 'use strict'; return eval('1'); }
            """);

        var leaf = StateOf(engine, "leaf");
        leaf.CanUseEmptyFDI.Should().BeTrue();
        leaf.SupportsLeafCall.Should().BeTrue();

        var evalLeaf = StateOf(engine, "evalLeaf");
        evalLeaf.CanUseEmptyFDI.Should().BeFalse();
        // Already false before the arguments object joined the gate: a direct eval can create a
        // closure over the frame, so the environment was never elidable.
        evalLeaf.SupportsLeafCall.Should().BeFalse();
    }

    [Test]
    public void ADebuggerStatementDoesNotMakeTheArgumentsObjectNeeded()
    {
        var engine = new Engine();
        engine.Execute("""
            function sloppy() { debugger; return 1; }
            function strict() { 'use strict'; debugger; return 1; }
            """);

        // The eval context and the arguments object are decided by one walk, but a debugger
        // statement is evidence for only the first of them.
        var sloppy = StateOf(engine, "sloppy");
        sloppy.NeedsEvalContext.Should().BeTrue();
        sloppy.ArgumentsObjectNeeded.Should().BeFalse();

        var strict = StateOf(engine, "strict");
        strict.NeedsEvalContext.Should().BeFalse();
        strict.ArgumentsObjectNeeded.Should().BeFalse();
        strict.CanUseEmptyFDI.Should().BeTrue();
    }

    [Test]
    public void AShadowedArgumentsNameDoesNotMakeTheArgumentsObjectNeeded()
    {
        var engine = new Engine();
        engine.Execute("""
            function byParameter(arguments) { return eval('arguments'); }
            function byDeclaration() { function arguments() { } return eval('arguments'); }
            function byLexical() { let arguments = 5; return eval('arguments'); }
            var byArrow = () => eval('1');
            """);

        StateOf(engine, "byParameter").ArgumentsObjectNeeded.Should().BeFalse();
        StateOf(engine, "byDeclaration").ArgumentsObjectNeeded.Should().BeFalse();
        StateOf(engine, "byLexical").ArgumentsObjectNeeded.Should().BeFalse();
        StateOf(engine, "byArrow").ArgumentsObjectNeeded.Should().BeFalse();
    }

    [Test]
    public void AnEvalInsideANestedFunctionIsThatFunctionsBusiness()
    {
        var engine = new Engine();
        engine.Execute("""
            function outerOfExpression() { 'use strict'; var g = function () { return eval('1'); }; return g; }
            function outerOfDeclaration() { 'use strict'; function g() { return eval('1'); } return g; }
            function outerOfArrow() { 'use strict'; var g = () => eval('1'); return g; }
            """);

        // A nested function has an arguments object of its own, so its direct eval says nothing
        // about the enclosing function...
        StateOf(engine, "outerOfExpression").ArgumentsObjectNeeded.Should().BeFalse();
        StateOf(engine, "outerOfDeclaration").ArgumentsObjectNeeded.Should().BeFalse();

        // ...but an arrow has none, so its eval names the enclosing function's.
        StateOf(engine, "outerOfArrow").ArgumentsObjectNeeded.Should().BeTrue();
    }
}
