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
    public void ConstraintConfiguredEngineStillEnforcesInsideLoops()
    {
        var engine = new Engine(options => options.MaxStatements(50));
        // constraint-configured contexts must keep per-statement accounting (tight loop is
        // gated off), so a runaway loop still trips MaxStatements
        Assert.Throws<Jint.Runtime.StatementsCountOverflowException>(() =>
            engine.Evaluate("(function () { var x = 0; for (var i = 0; i < 100000; i++) { x += 1; } })()"));
    }
}
