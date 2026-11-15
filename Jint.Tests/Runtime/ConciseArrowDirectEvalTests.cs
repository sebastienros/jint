#nullable enable

using Jint.Native.Function;
using Jint.Runtime.Interpreter;

namespace Jint.Tests.Runtime;

/// <summary>
/// An arrow whose concise body is itself a direct eval call (<c>x =&gt; eval(src)</c>) must keep its
/// parameters reachable from whatever the eval leaves behind, exactly as a block-bodied arrow or a
/// function expression does (sebastienros/jint#4366).
/// </summary>
/// <remarks>
/// <para>
/// The arrow closes over the running context's environment
/// (https://tc39.es/ecma262/#sec-runtime-semantics-instantiatearrowfunctionexpression), its call creates
/// a fresh environment holding the parameters (https://tc39.es/ecma262/#sec-functiondeclarationinstantiation)
/// and evaluates the concise body in it (https://tc39.es/ecma262/#sec-runtime-semantics-evaluateconcisebody),
/// and a direct eval runs its code with that environment as the outer one
/// (https://tc39.es/ecma262/#sec-performeval) — so a function the eval creates holds the call's environment
/// for as long as the function lives.
/// </para>
/// <para>
/// The static analysis that decides whether a call environment may be reused once the call returns walks a
/// body's <em>children</em>. A concise body is an expression, so when that expression is the eval call
/// itself the walk never saw it: the arrow was given a reusable fixed-slot environment, which is cleared on
/// return, and the eval'd closure then read a parameter that no longer existed. Expected values are Node 24's.
/// </para>
/// </remarks>
public class ConciseArrowDirectEvalTests
{
    // The concise body is the direct eval call itself.
    [TestCase("const f = (log => eval('(function () { return log; })'))(42); f()", "42")]
    [TestCase("const f = (log => eval('() => log'))(42); f()", "42")]
    [TestCase("const mk = log => eval('() => log'); const a = mk(1), b = mk(2); [a(), b()].join()", "1,2")]
    [TestCase("const mk = log => eval('() => log'); const a = mk(1); mk(2); mk(3); a()", "1")]
    [TestCase("const f = (log => eval('[() => log, v => log = v]'))(1); f[1](5); f[0]()", "5")]
    [TestCase("const g = x => eval(\"var r = typeof y; var y = x; r + ':' + y\"); [g(1), g(2)].join()", "undefined:1,undefined:2")]
    [TestCase("const g = x => eval('var y = x; () => y'); const a = g(1), b = g(2); [a(), b()].join()", "1,2")]
    [TestCase("const f = (({ log }) => eval('() => log'))({ log: 42 }); f()", "42")]
    [TestCase("const f = (([log]) => eval('() => log'))([42]); f()", "42")]
    [TestCase("const f = ((...log) => eval('() => log'))(42, 43); f().join()", "42,43")]
    [TestCase("const f = ((log, y = 1) => eval('() => log + y'))(42); f()", "43")]
    [TestCase("const o = { v: 41, m() { return (x => eval('() => this.v + x'))(1); } }; o.m()()", "42")]
    [TestCase("function outer() { return (x => eval('() => arguments[0] + x'))(1); } outer(41)()", "42")]

    // Strict: the eval gets a variable environment of its own, but the parameters are still the arrow's.
    [TestCase("'use strict'; const f = (log => eval('(function () { return log; })'))(42); f()", "42")]
    [TestCase("'use strict'; const mk = log => eval('() => log'); const a = mk(1), b = mk(2); [a(), b()].join()", "1,2")]
    [TestCase("'use strict'; const mk = log => eval('() => log'); const a = mk(1); mk(2); mk(3); a()", "1")]
    [TestCase("'use strict'; const f = (log => eval('[() => log, v => log = v]'))(1); f[1](5); f[0]()", "5")]
    [TestCase("'use strict'; const g = x => eval('var y = x; () => y'); const a = g(1), b = g(2); [a(), b()].join()", "1,2")]
    [TestCase("'use strict'; const f = (({ log }) => eval('() => log'))({ log: 42 }); f()", "42")]
    [TestCase("'use strict'; const f = ((...log) => eval('() => log'))(42, 43); f().join()", "42,43")]

    // Async: the body is evaluated the same way, its result settling a promise.
    [TestCase("(async log => eval('() => log'))(42).then(f => f())", "42")]
    [TestCase("(async log => eval('(function () { return log; })'))(42).then(f => f())", "42")]
    [TestCase("const mk = async log => eval('() => log'); Promise.all([mk(1), mk(2)]).then(([a, b]) => [a(), b()].join())", "1,2")]
    [TestCase("const g = async x => eval('var y = x; () => y'); Promise.all([g(1), g(2)]).then(([a, b]) => [a(), b()].join())", "1,2")]
    [TestCase("'use strict'; (async log => eval('() => log'))(42).then(f => f())", "42")]

    // The shapes that already worked, kept as the reference the rows above are measured against: the eval
    // call below the body's root, a block body, a function expression, an eval in a parameter default.
    [TestCase("const f = (log => { return eval('(function () { return log; })'); })(42); f()", "42")]
    [TestCase("const f = (function (log) { return eval('(function () { return log; })'); })(42); f()", "42")]
    [TestCase("const f = (log => [eval('() => log')])(42)[0]; f()", "42")]
    [TestCase("const f = (log => true ? eval('() => log') : 0)(42); f()", "42")]
    [TestCase("const f = (log => () => eval('log'))(42); f()", "42")]
    [TestCase("const f = ((log, g = eval('() => log')) => g)(42); f()", "42")]
    [TestCase("(async log => { return eval('() => log'); })(42).then(f => f())", "42")]

    // An indirect eval runs at global scope, so it never sees the parameter at all.
    [TestCase("globalThis.log = 'global'; const f = (log => (0, eval)('(function () { return log; })'))(42); f()", "global")]
    [TestCase("globalThis.log = 'global'; const e = eval; const f = (log => e('(function () { return log; })'))(42); f()", "global")]
    public void WhatTheEvalLeavesBehindStillSeesTheArrowsParameters(string source, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).UnwrapIfPromise().ToString().Should().Be(expected);
    }

    [Test]
    public void AWarmedEngineKeepsEveryCallsParameters()
    {
        var engine = new Engine();
        engine.Execute("var mk = log => eval('() => log'); var made = [];");

        // The handler-tree caches engage on the second evaluation of a script, and an environment
        // reused across calls would hand every earlier closure the latest call's value — or none.
        for (var i = 0; i < 5; i++)
        {
            engine.Execute($"made.push(mk({i}));");
        }

        engine.Evaluate("made.map(g => g()).join()").ToString().Should().Be("0,1,2,3,4");
    }

    // In sloppy code the concise body that is a direct eval is analysed exactly as the block body returning it.
    [TestCase("var f = log => eval('log');", "var f = log => { return eval('log'); };")]
    [TestCase("var f = async log => eval('log');", "var f = async log => { return eval('log'); };")]
    [TestCase("var f = ({ log }) => eval('log');", "var f = ({ log }) => { return eval('log'); };")]
    [TestCase("var f = (a, b = 1) => eval('a + b');", "var f = (a, b = 1) => { return eval('a + b'); };")]
    public void ASloppyConciseDirectEvalBodyIsAnalysedAsItsBlockBodiedTwin(string concise, string block)
    {
        var conciseState = StateOf(concise);
        var blockState = StateOf(block);

        conciseState.NeedsEvalContext.Should().BeTrue();
        conciseState.EnvironmentMayEscape.Should().BeTrue();
        conciseState.NeedsEvalContext.Should().Be(blockState.NeedsEvalContext);
        conciseState.EnvironmentMayEscape.Should().Be(blockState.EnvironmentMayEscape);
        conciseState.UseFixedSlots.Should().Be(blockState.UseFixedSlots);
        conciseState.SupportsRegisterCall.Should().Be(blockState.SupportsRegisterCall);
    }

    // Strict code is not compared with its block twin: a concise body carries no strictness flag of its own
    // (the parser records it on a FunctionBody), so the analysis treats it as sloppy code and keeps it off
    // the fixed-slot lane even where the block twin is on it. What matters is that the environment escapes.
    [TestCase("'use strict'; var f = log => eval('log');")]
    [TestCase("'use strict'; var f = async log => eval('log');")]
    public void AStrictConciseDirectEvalBodyKeepsItsEnvironment(string source)
    {
        StateOf(source).EnvironmentMayEscape.Should().BeTrue();
    }

    // The fixed-slot escape analysis on its own, whatever reaches it: a direct eval as the whole concise
    // body can name every slot, and leave a closure over them behind.
    [TestCase("(log => eval('log'))", true)]
    [TestCase("(log => eval('(function () { return log; })'))", true)]
    [TestCase("(log => [eval('log')])", true)]
    [TestCase("(log => () => eval('0'))", true)]
    [TestCase("(log => (0, eval)('log'))", false)]
    [TestCase("(log => e('log'))", false)]
    [TestCase("(log => log + 1)", false)]
    public void TheFixedSlotEscapeAnalysisSeesADirectEvalAtTheRootOfAConciseBody(string source, bool expected)
    {
        var script = new Parser().ParseScript(source);
        var arrow = (ArrowFunctionExpression) ((ExpressionStatement) script.Body[0]).Expression;

        JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscapeWithReferences(arrow, ["log"])
            .Should().Be(expected);
    }

    // What the analysis costs everything else: an arrow with no direct eval at its root keeps the reusable
    // fixed-slot environment and the register lane, the indirect forms included — `(0, eval)(…)` and an
    // aliased `eval` are not a direct eval and cannot reach the arrow's bindings.
    [TestCase("var f = x => x + 1;")]
    [TestCase("'use strict'; var f = x => x + 1;")]
    [TestCase("var f = log => (0, eval)('log');")]
    [TestCase("var e = eval; var f = log => e('log');")]
    [TestCase("var f = x => evaluate(x); function evaluate(v) { return v; }")]
    public void AConciseBodyWithoutADirectEvalKeepsItsReusableEnvironment(string source)
    {
        var state = StateOf(source);

        state.UseFixedSlots.Should().BeTrue();
        state.NeedsEvalContext.Should().BeFalse();
        state.EnvironmentMayEscape.Should().BeFalse();
        state.SupportsRegisterCall.Should().BeTrue();
    }

    private static JintFunctionDefinition.State StateOf(string source)
    {
        var engine = new Engine();
        engine.Execute(source);
        var function = (ScriptFunction) engine.GetValue("f");
        return function._functionDefinition!.Initialize();
    }
}
