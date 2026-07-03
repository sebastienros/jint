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

        Assert.False(engine.Evaluate("leaked").AsBoolean());
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

        Assert.Equal("1,2,3,4", engine.Evaluate("getters.map(function (g) { return g(); }).join(',')").AsString());
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
        Assert.Equal("2,1,0", engine.Evaluate("results.join(',')").AsString());

        // second round reuses the promoted cache entry (and possibly a pooled environment)
        engine.Execute("n = 0; results = []; run();");
        Assert.Equal("2,1,0", engine.Evaluate("results.join(',')").AsString());
    }

    [Fact]
    public void FunctionDeclarationsInStrictEvalAreHoistedAndCallable()
    {
        var engine = NewStrictEngine();

        Assert.Equal(7, engine.Evaluate("eval('function h() { return 7; } h();')").AsNumber());

        // var and function declaration sharing a name occupy one binding, function wins at instantiation
        Assert.Equal(1, engine.Evaluate("eval('var f; function f() { return 1; } f();')").AsNumber());

        // hoisting: callable before the declaration's position, sees later var assignment at call time
        Assert.Equal(5, engine.Evaluate("eval('var r = early(); function early() { return v; } var v = 5; r === undefined ? early() : -1;')").AsNumber());
    }

    [Fact]
    public void ConstInEvalThrowsOnAssignment()
    {
        var engine = NewStrictEngine();

        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute("eval('const c = 1; c = 2;');"));
        Assert.True(ex.Error.InstanceofOperator(engine.Intrinsics.TypeError));
    }

    [Fact]
    public void LetInEvalThrowsInTemporalDeadZone()
    {
        var engine = NewStrictEngine();

        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute("eval('t; let t = 1;');"));
        Assert.True(ex.Error.InstanceofOperator(engine.Intrinsics.ReferenceError));
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

        Assert.Equal(190, engine.Evaluate("total").AsNumber());
    }

    [Fact]
    public void SloppyDirectEvalStillLeaksVarsToCaller()
    {
        var engine = new Engine();
        engine.Execute("eval('var sloppyLeak = 5;');");

        Assert.Equal(5, engine.Evaluate("sloppyLeak").AsNumber());
    }

    [Fact]
    public void IndirectEvalWithUseStrictKeepsVarsLocal()
    {
        var engine = new Engine();
        engine.Execute("var ivResult; (0, eval)(\"'use strict'; var iv = 3; ivResult = iv;\");");

        Assert.Equal(3, engine.Evaluate("ivResult").AsNumber());
        Assert.Equal("undefined", engine.Evaluate("typeof iv").AsString());
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

        Assert.Equal("1,2,3,4", engine.Evaluate("seen.join(',')").AsString());
    }
}
