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

        result.Should().Be("01234:4950:3:3");
    }

    [Fact]
    public void ScriptTopLevelLoopKeepsCompletionValue()
    {
        var engine = new Engine();
        // at script top level completion values are observable: the for statement's value is the
        // last body statement's value, so the tight loop must not engage and eat it
        var result = engine.Evaluate("var s = ''; for (var i = 0; i < 3; i++) { s += i; }").AsString();

        result.Should().Be("012");
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

        result.Should().Be("true:0");
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

        result.Should().Be("60:012");
    }

    [Fact]
    public void DeadMarkedTopLevelLoopRunsTightWithTrailingValue()
    {
        var engine = new Engine();
        // the trailing expression statement makes the for's completion value dead at script top
        // level, unlocking the tight path there; the script value comes from the trailing read
        var result = engine.Evaluate("var s = ''; for (var i = 0; i < 3; i++) { s += i; } s;").AsString();
        result.Should().Be("012");

        var evalResult = engine.Evaluate("eval(\"var t = 0; for (var i = 0; i < 5; i++) { t += i; } t;\")").AsNumber();
        evalResult.Should().Be(10);
    }

    [Fact]
    public void LabeledBreakOutOfBlockKeepsEarlierCompletionValue()
    {
        var engine = new Engine();
        // the block's list is an Inherit list: the labeled break (an Empty-valued abrupt
        // completion) jumps over the trailing 'b', so dead-value marking must NOT apply there —
        // UpdateEmpty surfaces the loop's accumulated 'v' as the script value
        var result = engine.Evaluate("foo: { for (var i = 0; ; i++) { 'v'; break foo; } 'b'; }").AsString();
        result.Should().Be("v");
    }

    [Fact]
    public void ConditionalTrailingStatementProducesSpecCompletion()
    {
        var engine = new Engine();
        // an untaken if completes with undefined (UpdateEmpty(stmt, undefined) per spec), which
        // overwrites the loop's value regardless of dead-marking; pins that the conservative
        // analysis (which does not count if statements as always-valued) changes nothing here
        var result = engine.Evaluate("for (var i = 0; i < 1; i++) { 'v'; } if (false) 'b';");
        result.IsUndefined().Should().BeTrue();
    }

    [Fact]
    public void ConstraintConfiguredEngineStillEnforcesInsideLoops()
    {
        var engine = new Engine(options => options.MaxStatements(50));
        // constraint-configured contexts must keep per-statement accounting (tight loop is
        // gated off), so a runaway loop still trips MaxStatements
        Invoking(() =>
            engine.Evaluate("(function () { var x = 0; for (var i = 0; i < 100000; i++) { x += 1; } })()")).Should().ThrowExactly<Jint.Runtime.StatementsCountOverflowException>();
    }

    [Fact]
    public void IfChainBodyMatchesGenericPathExactly()
    {
        var engine = new Engine();
        // same computation twice: the first shape is tight-eligible (if/else chain + var decls),
        // the second carries a never-taken break that structurally disqualifies it, forcing the
        // generic path — both must agree
        var result = engine.Evaluate("""
            (function () {
                function tightShape() {
                    var counts = [0, 0, 0, 0, 0];
                    for (var x = 0; x < 200; x++) {
                        var z = x ^ 3;
                        if (z % 2 == 0) counts[0]++;
                        else if (z % 3 == 0) counts[1]++;
                        else if (z % 5 == 0) counts[2]++;
                        else if (z % 7 == 0) counts[3]++;
                        else counts[4]++;
                        var v = counts.length;
                    }
                    return counts.join(',');
                }
                function genericShape() {
                    var counts = [0, 0, 0, 0, 0];
                    for (var x = 0; x < 200; x++) {
                        var z = x ^ 3;
                        if (z % 2 == 0) counts[0]++;
                        else if (z % 3 == 0) counts[1]++;
                        else if (z % 5 == 0) counts[2]++;
                        else if (z % 7 == 0) counts[3]++;
                        else counts[4]++;
                        var v = counts.length;
                        if (x > 100000) break;
                    }
                    return counts.join(',');
                }
                var a = tightShape();
                var b = genericShape();
                return (a === b) + ':' + a;
            })()
            """).AsString();

        result.Should().StartWith("true:");
    }

    [Fact]
    public void IfChainBodyRunsTightAtTopLevelWithTrailingStatement()
    {
        var engine = new Engine();
        // trailing expression statement kills the loop's completion value → tight path engages
        // at script top level; the loop calls closures exactly like the stopwatch benchmark
        var result = engine.Evaluate("""
            var n = 0;
            var o = { inc: function () { n++; }, dec: function () { n--; } };
            for (var x = 0; x < 100; x++) {
                var z = x ^ 5;
                if (z % 2 == 0) o.inc();
                else if (z % 3 == 0) o.dec();
                var v = o.inc;
            }
            n;
            """).AsNumber();

        var expected = 0;
        for (var x = 0; x < 100; x++)
        {
            var z = x ^ 5;
            if (z % 2 == 0) expected++;
            else if (z % 3 == 0) expected--;
        }

        result.Should().Be(expected);
    }

    [Fact]
    public void ThrowInsideTakenBranchPropagatesWithState()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var i = 0, ran = 0;
                try {
                    for (i = 0; i < 10; i++) {
                        var z = i ^ 1;
                        if (z % 2 == 0) ran++;
                        else mustNotExist();
                    }
                } catch (e) {
                    return (e instanceof ReferenceError) + ':' + i + ':' + ran;
                }
                return 'no-throw';
            })()
            """).AsString();

        // i=0 → z=1 → else branch throws before any increment
        result.Should().Be("true:0:0");
    }

    [Fact]
    public void NestedBranchBlockStopsAfterThrowingStatement()
    {
        var engine = new Engine();
        // inside a multi-statement branch block, statements after a throwing one must not run
        var result = engine.Evaluate("""
            (function () {
                var a = 0, b = 0;
                try {
                    for (var i = 0; i < 3; i++) {
                        if (true) { a++; mustNotExist(); b++; }
                    }
                } catch (e) {
                    return a + ':' + b;
                }
                return 'no-throw';
            })()
            """).AsString();

        result.Should().Be("1:0");
    }

    [Fact]
    public void BreakContinueReturnBodiesKeepExactSemantics()
    {
        var engine = new Engine();
        // break/continue/return statements structurally disqualify the tight path; semantics
        // must be untouched
        var result = engine.Evaluate("""
            (function () {
                var s = '';
                for (var i = 0; i < 5; i++) {
                    if (i === 2) continue;
                    if (i === 4) break;
                    s += i;
                }
                function returner() {
                    for (var j = 0; j < 10; j++) {
                        if (j === 3) return 'r' + j;
                    }
                    return 'end';
                }
                return s + ':' + i + ':' + returner();
            })()
            """).AsString();

        result.Should().Be("013:4:r3");
    }

    [Fact]
    public void FlattenedConstBodyKeepsPerIterationTdz()
    {
        var engine = new Engine();
        // let-header + const-body loops flatten into the pooled loop env and stay tight-eligible;
        // the body slots must be re-TDZ'd each iteration, so a read before the const initializes
        // throws — also on iterations after the first (stale value from iteration 1 must not leak)
        var untaken = engine.Evaluate("""
            (function () {
                let total = 0;
                for (let i = 0; i < 3; i++) {
                    if (false) z;
                    const z = i * 2;
                    total += z;
                }
                return total;
            })()
            """).AsNumber();
        untaken.Should().Be(6);

        var secondIteration = engine.Evaluate("""
            (function () {
                try {
                    for (let i = 0; i < 3; i++) {
                        if (i === 1) z;
                        const z = i;
                    }
                } catch (e) {
                    return (e instanceof ReferenceError) + ':tdz';
                }
                return 'no-throw';
            })()
            """).AsString();
        secondIteration.Should().Be("true:tdz");
    }

    [Fact]
    public void ConstraintConfiguredEngineStillEnforcesInsideIfChainLoops()
    {
        var engine = new Engine(options => options.MaxStatements(50));
        Invoking(() =>
            engine.Evaluate("(function () { var x = 0; for (var i = 0; i < 100000; i++) { if (i % 2 == 0) x += 1; else x -= 1; } })()")).Should().ThrowExactly<Jint.Runtime.StatementsCountOverflowException>();
    }

    [Fact]
    public void UsingDeclarationBodyDisposesPerIteration()
    {
        var engine = new Engine();
        // using declarations are excluded from the tight shape; per-iteration dispose ordering
        // must be untouched
        var result = engine.Evaluate("""
            (function () {
                var log = [];
                for (var i = 0; i < 2; i++) {
                    using r = { [Symbol.dispose]() { log.push('d' + i); } };
                    log.push('u' + i);
                }
                return log.join(',');
            })()
            """).AsString();

        result.Should().Be("u0,d0,u1,d1");
    }
}
