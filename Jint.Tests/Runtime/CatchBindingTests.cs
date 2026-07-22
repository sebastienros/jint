namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the behavior of the pooled fixed-slot catch environment used for a non-escaping single-identifier
/// <c>catch (e)</c>. The optimization must be invisible: every entry sees its own caught value, mutation and
/// re-throw work, recursion through the same catch is isolated, and a closure that captures the catch binding
/// falls back to a per-entry environment.
/// </summary>
public class CatchBindingTests
{
    [Fact]
    public void CaughtValueAndMutationWork()
    {
        var engine = new Engine();
        engine.Evaluate("try { throw new Error('m1'); } catch (e) { e.message; }").AsString().Should().Be("m1");
        engine.Evaluate("try { throw 10; } catch (e) { e = e + 5; e; }").AsNumber().Should().Be(15);
        engine.Evaluate("try { throw undefined; } catch (e) { e === undefined; }").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RethrowReusesValue()
    {
        var engine = new Engine();
        var result = engine.Evaluate("try { try { throw 'inner'; } catch (e) { throw e; } } catch (e2) { e2; }").AsString();
        result.Should().Be("inner");
    }

    [Fact]
    public void CatchBindingShadowsOuterAndDoesNotLeak()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
var e = 'outer';
var caught = (function () { try { throw 'shadow'; } catch (e) { return e; } })();
caught + '|' + e;").AsString();
        result.Should().Be("shadow|outer");
    }

    [Fact]
    public void SequentialCatchesAreIndependent()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
var seq = [];
for (var i = 0; i < 3; i++) { try { throw i * 2; } catch (e) { seq.push(e); } }
seq.join(',');").AsString();
        result.Should().Be("0,2,4");
    }

    [Fact]
    public void RecursionThroughSameCatchIsIsolated()
    {
        var engine = new Engine();
        // Each recursive frame catches its own value; the pooled env must rent a fresh instance while a
        // previous frame's catch is still active (rent-null re-entrancy).
        var result = engine.Evaluate(@"
function rec(n) {
  try { throw n; } catch (e) {
    if (e > 0) { return e + rec(e - 1); }
    return 0;
  }
}
rec(3);").AsNumber();
        result.Should().Be(6); // 3 + 2 + 1 + 0
    }

    [Fact]
    public void ClosureCapturingCatchBindingIsNotPooled()
    {
        var engine = new Engine();
        // A closure captures the catch binding, so each entry must keep a distinct environment.
        var result = engine.Evaluate(@"
var closures = [];
for (var i = 0; i < 3; i++) {
  try { throw 'v' + i; } catch (e) { closures.push(function () { return e; }); }
}
closures[0]() + ',' + closures[1]() + ',' + closures[2]();").AsString();
        result.Should().Be("v0,v1,v2");
    }

    [Fact]
    public void CapturedCatchBindingSurvivesLaterPooledCatches()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"
var saved;
try { throw 'keepme'; } catch (err) { saved = function () { return err; }; }
for (var j = 0; j < 5; j++) { try { throw j; } catch (e) { } }
saved();").AsString();
        result.Should().Be("keepme");
    }

    [Fact]
    public void CatchBlockWithLexicalDeclarationWorks()
    {
        var engine = new Engine();
        var result = engine.Evaluate("try { throw 'x'; } catch (e) { let y = e + '!'; y; }").AsString();
        result.Should().Be("x!");
    }
}
