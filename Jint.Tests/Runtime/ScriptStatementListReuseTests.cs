namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of the per-engine top-level (Program) statement-list cache: re-evaluating the
/// same prepared script on one engine reuses the top-level handler tree (and, through it, top-level
/// function-expression definitions and their warm inline caches) while every closure, environment and
/// per-run value stays strictly per evaluation. A first evaluation - the fresh-engine-per-op shape -
/// builds the tree fresh and caches nothing, so those hosts stay byte-identical to the historical path;
/// these tests exercise the RE-evaluation branch that the cache actually engages on.
/// </summary>
public class ScriptStatementListReuseTests
{
    // Each case is (source, expected string result). Re-evaluated many times on ONE engine: the result
    // must be identical every time, matching a fresh engine — the reused top-level tree must not carry
    // stale per-run state.
    public static TheoryData<string, string> Cases() => new()
    {
        { "var x = 1 + 1 === 2; '' + x;", "true" },
        { "var t = 0; for (var i = 0; i < 10; i++) { t += i; } '' + t;", "45" },
        { "var s = 0; { let y = 3; const z = y * 2; s = y + z; } '' + s;", "9" },
        { "var out = ''; for (var k of [1, 2, 3]) { out += k; } out;", "123" },
        { "var o = { a: 1, b: 2 }; var ks = ''; for (var p in o) { ks += p; } ks;", "ab" },
        { "var r; try { r = 1; throw 'e'; } catch (e) { r = 2; } finally { r += 10; } '' + r;", "12" },
        { "'use strict'; var q = (function () { return 42; })(); '' + q;", "42" },
        { "var m = [1, 2, 3].map(function (v) { return v * v; }).reduce(function (a, b) { return a + b; }, 0); '' + m;", "14" },
        { "function g(n) { var r = 0; for (var i = 0; i < n; i++) { r += i; } return r; } '' + g(100);", "4950" },
        { "var sw = ''; switch (2) { case 1: sw = 'one'; break; case 2: sw = 'two'; break; default: sw = 'd'; } sw;", "two" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void RepeatedEvaluationMatchesFreshEngine(string source, string expected)
    {
        var prepared = Engine.PrepareScript(source);

        // Baseline: a first evaluation on a fresh engine (the uncached path).
        Assert.Equal(expected, new Engine().Evaluate(prepared).ToString());

        // Re-evaluations on a single engine must produce the identical value every time.
        var engine = new Engine();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(expected, engine.Evaluate(prepared).ToString());
        }
    }

    [Fact]
    public void ReusedTopLevelFunctionExpressionRecapturesCurrentState()
    {
        // A top-level function expression's interpreter definition is reused across evaluations once the
        // top-level tree is cached, but each evaluation must still produce a closure over THIS run's
        // freshly re-declared global state — not a stale binding from an earlier run.
        var engine = new Engine();
        var prepared = Engine.PrepareScript("""
            var base = seed * 10;
            var add = function (n) { return base + n; };
            add(seed);
            """);

        engine.SetValue("seed", 1);
        Assert.Equal(11, engine.Evaluate(prepared).AsNumber()); // base=10, add(1)=11

        engine.SetValue("seed", 5);
        Assert.Equal(55, engine.Evaluate(prepared).AsNumber()); // base=50, add(5)=55

        engine.SetValue("seed", 9);
        Assert.Equal(99, engine.Evaluate(prepared).AsNumber()); // base=90, add(9)=99
    }

    [Fact]
    public void ReusedTopLevelIifeKeepsIndependentPerRunState()
    {
        // The dominant embedding shape (module-pattern library) is a single top-level IIFE. Caching the
        // top-level list reuses the IIFE's body tree across runs; each run must still get fresh internal
        // state (the counter starts at 0 every evaluation).
        var engine = new Engine();
        var prepared = Engine.PrepareScript("""
            (function () {
                var count = 0;
                function bump() { count += 1; return count; }
                bump(); bump(); bump();
                return count;
            })();
            """);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(3, engine.Evaluate(prepared).AsNumber());
        }
    }
}
