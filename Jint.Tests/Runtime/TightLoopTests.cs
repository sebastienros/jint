namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of the for-statement tight loop (JintForStatement.TightForBody): it engages
/// for expression-statements-only bodies in frames where completion values are dead, and must be
/// indistinguishable from the generic path.
/// </summary>
public class TightLoopTests
{
    [Fact]
    public void FunctionLocalLoopComputesThroughTightPath()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var s = '';
                for (var i = 0; i < 5; i++) { s += i; }
                var total = 0;
                for (var j = 0; j < 100; j++) total += j;
                for (var k = 0; k < 3; k++) { }
                for (var m = 0; m < 3; ) { m += 1; }
                return s + ':' + total + ':' + k + ':' + m;
            })()
            """).AsString();

        Assert.Equal("01234:4950:3:3", result);
    }

    [Fact]
    public void ScriptTopLevelLoopKeepsCompletionValue()
    {
        var engine = new Engine();
        // at script top level completion values are observable: the for statement's value is the
        // last body statement's value, so the tight loop must not engage and eat it
        var result = engine.Evaluate("var s = ''; for (var i = 0; i < 3; i++) { s += i; }").AsString();

        Assert.Equal("012", result);
    }

    [Fact]
    public void ThrowInsideTightLoopPropagatesWithState()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var i = 0;
                try {
                    for (i = 0; i < 10; i++) { mustNotExist(); }
                } catch (e) {
                    return (e instanceof ReferenceError) + ':' + i;
                }
                return 'no-throw';
            })()
            """).AsString();

        Assert.Equal("true:0", result);
    }

    [Fact]
    public void LetLoopWithReusableEnvironmentStaysCorrect()
    {
        var engine = new Engine();
        // let-header loops with a non-capturing body reuse the iteration environment and remain
        // tight-loop eligible; capturing bodies are excluded by the reuse analysis
        var result = engine.Evaluate("""
            (function () {
                let total = 0;
                for (let i = 0; i < 4; i++) { total += i * 10; }
                const fns = [];
                for (let j = 0; j < 3; j++) { fns.push(() => j); }
                return total + ':' + fns.map(f => f()).join('');
            })()
            """).AsString();

        Assert.Equal("60:012", result);
    }

    [Fact]
    public void DeadMarkedTopLevelLoopRunsTightWithTrailingValue()
    {
        var engine = new Engine();
        // the trailing expression statement makes the for's completion value dead at script top
        // level, unlocking the tight path there; the script value comes from the trailing read
        var result = engine.Evaluate("var s = ''; for (var i = 0; i < 3; i++) { s += i; } s;").AsString();
        Assert.Equal("012", result);

        var evalResult = engine.Evaluate("eval(\"var t = 0; for (var i = 0; i < 5; i++) { t += i; } t;\")").AsNumber();
        Assert.Equal(10, evalResult);
    }

    [Fact]
    public void LabeledBreakOutOfBlockKeepsEarlierCompletionValue()
    {
        var engine = new Engine();
        // the block's list is an Inherit list: the labeled break (an Empty-valued abrupt
        // completion) jumps over the trailing 'b', so dead-value marking must NOT apply there —
        // UpdateEmpty surfaces the loop's accumulated 'v' as the script value
        var result = engine.Evaluate("foo: { for (var i = 0; ; i++) { 'v'; break foo; } 'b'; }").AsString();
        Assert.Equal("v", result);
    }

    [Fact]
    public void ConditionalTrailingStatementProducesSpecCompletion()
    {
        var engine = new Engine();
        // an untaken if completes with undefined (UpdateEmpty(stmt, undefined) per spec), which
        // overwrites the loop's value regardless of dead-marking; pins that the conservative
        // analysis (which does not count if statements as always-valued) changes nothing here
        var result = engine.Evaluate("for (var i = 0; i < 1; i++) { 'v'; } if (false) 'b';");
        Assert.True(result.IsUndefined());
    }

    [Fact]
    public void ConstraintConfiguredEngineStillEnforcesInsideLoops()
    {
        var engine = new Engine(options => options.MaxStatements(50));
        // constraint-configured contexts must keep per-statement accounting (tight loop is
        // gated off), so a runaway loop still trips MaxStatements
        Assert.Throws<Jint.Runtime.StatementsCountOverflowException>(() =>
            engine.Evaluate("(function () { var x = 0; for (var i = 0; i < 100000; i++) { x += 1; } })()"));
    }
}
