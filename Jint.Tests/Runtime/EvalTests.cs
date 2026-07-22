using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Semantics of eval bodies with the slot-backed strict-eval environment: repeated evals of the
/// same source share a cached statement tree and may reuse a pooled environment, which must stay
/// unobservable from script.
/// </summary>
public class EvalTests
{
    private static Engine NewStrictEngine() => new(static options => options.Strict());

    [Fact]
    public void RepeatedSameSourceEvalStartsFromFreshBindings()
    {
        var engine = NewStrictEngine();
        engine.Execute("var leaked = false; var src = 'if (probe !== undefined) { leaked = true; } var probe = 42;';");

        // several rounds so the source passes two-touch promotion and later rounds rent the pooled env
        for (var i = 0; i < 5; i++)
        {
            engine.Execute("eval(src);");
        }

        engine.Evaluate("leaked").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ClosuresCreatedInEvalKeepIndependentEnvironments()
    {
        var engine = NewStrictEngine();
        engine.Execute("var counter = 0; var getters = []; var src = 'var x = ++counter; getters.push(function() { return x; });';");

        for (var i = 0; i < 4; i++)
        {
            engine.Execute("eval(src);");
        }

        engine.Evaluate("getters.map(function (g) { return g(); }).join(',')").AsString().Should().Be("1,2,3,4");
    }

    [Fact]
    public void ReentrantSameSourceEvalKeepsCallLocalsSeparate()
    {
        var engine = NewStrictEngine();
        engine.Execute("""
            var n = 0;
            var results = [];
            var src = 'var local = n; n = n + 1; if (n < 3) { run(); } results.push(local);';
            function run() { eval(src); }
            """);

        engine.Execute("run();");
        engine.Evaluate("results.join(',')").AsString().Should().Be("2,1,0");

        // second round reuses the promoted cache entry (and possibly a pooled environment)
        engine.Execute("n = 0; results = []; run();");
        engine.Evaluate("results.join(',')").AsString().Should().Be("2,1,0");
    }

    [Fact]
    public void FunctionDeclarationsInStrictEvalAreHoistedAndCallable()
    {
        var engine = NewStrictEngine();

        engine.Evaluate("eval('function h() { return 7; } h();')").AsNumber().Should().Be(7);

        // var and function declaration sharing a name occupy one binding, function wins at instantiation
        engine.Evaluate("eval('var f; function f() { return 1; } f();')").AsNumber().Should().Be(1);

        // hoisting: callable before the declaration's position, sees later var assignment at call time
        engine.Evaluate("eval('var r = early(); function early() { return v; } var v = 5; r === undefined ? early() : -1;')").AsNumber().Should().Be(5);
    }

    [Fact]
    public void ConstInEvalThrowsOnAssignment()
    {
        var engine = NewStrictEngine();

        var ex = Invoking(() => engine.Execute("eval('const c = 1; c = 2;');")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.InstanceofOperator(engine.Intrinsics.TypeError).Should().BeTrue();
    }

    [Fact]
    public void LetInEvalThrowsInTemporalDeadZone()
    {
        var engine = NewStrictEngine();

        var ex = Invoking(() => engine.Execute("eval('t; let t = 1;');")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.InstanceofOperator(engine.Intrinsics.ReferenceError).Should().BeTrue();
    }

    [Fact]
    public void StrictEvalWithManyBindingsFallsBackCorrectly()
    {
        var engine = NewStrictEngine();

        // 20 unique names exceed the fixed-slot cap, exercising the dictionary-backed path
        var declarations = string.Join(" ", Enumerable.Range(0, 20).Select(i => $"var v{i} = {i};"));
        var sum = string.Join(" + ", Enumerable.Range(0, 20).Select(i => $"v{i}"));
        engine.Execute($"var src = '{declarations} total = {sum};'; var total = -1;");

        for (var i = 0; i < 3; i++)
        {
            engine.Execute("eval(src);");
        }

        engine.Evaluate("total").AsNumber().Should().Be(190);
    }

    [Fact]
    public void SloppyDirectEvalStillLeaksVarsToCaller()
    {
        var engine = new Engine();
        engine.Execute("eval('var sloppyLeak = 5;');");

        engine.Evaluate("sloppyLeak").AsNumber().Should().Be(5);
    }

    [Fact]
    public void IndirectEvalWithUseStrictKeepsVarsLocal()
    {
        var engine = new Engine();
        engine.Execute("var ivResult; (0, eval)(\"'use strict'; var iv = 3; ivResult = iv;\");");

        engine.Evaluate("ivResult").AsNumber().Should().Be(3);
        engine.Evaluate("typeof iv").AsString().Should().Be("undefined");
    }

    [Fact]
    public void CachedEvalRespectsBlockShadowing()
    {
        // the same cached eval body runs under different scope chains; the per-node slot
        // caches must not resolve past a block that shadows the name
        var engine = NewStrictEngine();
        engine.Execute("""
            var log = [];
            function h() {
                var x = 'h';
                eval("x += '!'");
                eval("x += '!'");
                { let x = 'q'; log.push(eval("x += '!'")); }
                log.push(x);
            }
            h();
            """);

        engine.Evaluate("log.join(',')").AsString().Should().Be("q!,h!!");
    }

    [Fact]
    public void CachedEvalRespectsConstShadowing()
    {
        var engine = NewStrictEngine();

        var ex = Invoking(() => engine.Execute("""
            function f() {
                var x = 'h';
                eval("x += '!'");
                eval("x += '!'");
                { const x = 'q'; eval("x += '!'"); }
            }
            f();
            """)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Error.InstanceofOperator(engine.Intrinsics.TypeError).Should().BeTrue();
    }

    [Fact]
    public void NestedSelfEvalKeepsActivationsSeparate()
    {
        // each nested eval of the same source gets its own environment; the shared statement
        // tree's caches must not read or write an outer activation's bindings
        var engine = NewStrictEngine();
        engine.Execute("""
            var results = [];
            var depth = 0;
            var src = "var x = 'a'; x += 'b'; if (depth++ < 3) { eval(src); } results.push(x);";
            eval(src);
            """);

        engine.Evaluate("results.join(',')").AsString().Should().Be("ab,ab,ab,ab");
    }

    [Fact]
    public void SloppyEvalVarInjectionShadowsOuterBinding()
    {
        // sloppy direct eval injects `s` into mid's environment on the second call; both the
        // compound write and the subsequent read must see the injected binding, not outer's
        var engine = new Engine();
        engine.Execute("""
            var results = [];
            function outer() {
                var s = 'A';
                { function mid(code) { eval(code); s += '!'; results.push(s); } }
                mid('');
                mid("var s = 'B';");
                results.push('outerS=' + s);
            }
            outer();
            """);

        engine.Evaluate("results.join(',')").AsString().Should().Be("A!,B!,outerS=A!");
    }

    [Fact]
    public void EvalCanRedeclareExistingNonConfigurableGlobals()
    {
        // https://tc39.es/ecma262/#sec-candeclareglobalvar: an existing global property is
        // var-declarable regardless of its attributes
        var engine = new Engine();
        engine.Execute("eval('var undefined;'); eval('var NaN;'); eval('var Infinity;');");

        engine.Evaluate("typeof undefined").AsString().Should().Be("undefined");
    }

    [Fact]
    public void RepeatedEvalObservesCurrentOuterState()
    {
        var engine = NewStrictEngine();
        engine.Execute("var outer = 0; var seen = []; var src = 'var snapshot = outer; seen.push(snapshot);';");

        for (var i = 1; i <= 4; i++)
        {
            engine.Execute($"outer = {i}; eval(src);");
        }

        engine.Evaluate("seen.join(',')").AsString().Should().Be("1,2,3,4");
    }
}
