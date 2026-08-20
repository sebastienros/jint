#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A computed key that has already been evaluated must not be evaluated a second time when the
/// expression it belongs to suspends on an <c>await</c> or a <c>yield</c> and is replayed.
/// <para>
/// Both shapes park their already-resolved state in the suspendable's
/// <c>SuspendDataDictionary</c>: a simple assignment parks the resolved left-hand
/// <c>Reference</c> (<c>AssignmentSuspendData.Lref</c>, the slot the compound forms
/// <c>+=</c>/<c>-=</c>/… have always used), and an object literal parks the converted property key
/// of the property whose <em>value</em> suspended (<c>ObjectExpressionSuspendData.PendingKey</c>).
/// Without them <c>o[f()] = await g()</c> and <c>({ [f()]: await g() })</c> call <c>f</c> twice, and
/// the assignment lands on whatever key the second call produced.
/// </para>
/// </summary>
public class SuspendedComputedKeyTests
{
    /// <summary>
    /// The key function returns a different name on every call, so a replay that re-runs it is
    /// visible in the landing key as well as in the counter.
    /// </summary>
    private const string CountingKey = "var calls = 0; function f() { return 'k' + (++calls); }";

    private static string Run(string script) => new Engine().Evaluate(script).AsString();

    private static string RunAsync(string script) => new Engine().Evaluate(script).UnwrapIfPromise().AsString();

    // ------------------------------------------------------------------ simple assignment

    [Fact]
    public void ComputedAssignmentKeyRunsOnceAcrossAnAwait()
    {
        RunAsync($$"""
            {{CountingKey}}
            var o = {};
            (async function () {
              o[f()] = await Promise.resolve(1);
              return calls + '|' + JSON.stringify(Object.keys(o)) + '|' + o.k1;
            })();
            """).Should().Be("1|[\"k1\"]|1");
    }

    [Fact]
    public void ComputedAssignmentKeyRunsOnceAcrossAYield()
    {
        Run($$"""
            {{CountingKey}}
            var o = {};
            function* g() { o[f()] = yield 1; }
            var it = g();
            it.next();
            it.next(42);
            calls + '|' + JSON.stringify(Object.keys(o)) + '|' + o.k1;
            """).Should().Be("1|[\"k1\"]|42");
    }

    [Fact]
    public void ComputedAssignmentBaseRunsOnceAcrossAnAwait()
    {
        // The base of the member expression is side-effecting too; the parked Reference holds it,
        // so neither half of `base()[key()]` is re-run.
        RunAsync("""
            var baseCalls = 0, keyCalls = 0;
            var o = {};
            function b() { baseCalls++; return o; }
            function k() { keyCalls++; return 'p'; }
            (async function () {
              b()[k()] = await Promise.resolve(3);
              return baseCalls + '|' + keyCalls + '|' + o.p;
            })();
            """).Should().Be("1|1|3");
    }

    [Fact]
    public void ComputedAssignmentIndexSideEffectAppliesOnceAcrossAnAwait()
    {
        RunAsync("""
            var i = 0;
            var a = [0, 0, 0];
            (async function () {
              a[i++] = await Promise.resolve(9);
              return i + '|' + a.join(',');
            })();
            """).Should().Be("1|9,0,0");
    }

    [Fact]
    public void ComputedAssignmentKeyRunsOnceWhenTheAwaitedPromiseRejects()
    {
        RunAsync($$"""
            {{CountingKey}}
            var o = {};
            (async function () {
              try { o[f()] = await Promise.reject(new Error('x')); }
              catch (e) { }
              return calls + '|' + JSON.stringify(Object.keys(o));
            })();
            """).Should().Be("1|[]");
    }

    // ------------------------------------------------------------------ compound assignment (guard)

    [Fact]
    public void CompoundComputedAssignmentKeyRunsOnceAcrossAnAwait()
    {
        RunAsync($$"""
            {{CountingKey}}
            var o = { k1: 1 };
            (async function () {
              o[f()] += await Promise.resolve(5);
              return calls + '|' + JSON.stringify(Object.keys(o)) + '|' + o.k1;
            })();
            """).Should().Be("1|[\"k1\"]|6");
    }

    [Fact]
    public void CompoundComputedAssignmentKeyRunsOnceAcrossAYield()
    {
        Run($$"""
            {{CountingKey}}
            var o = { k1: 1 };
            function* g() { o[f()] += yield 1; }
            var it = g();
            it.next();
            it.next(5);
            calls + '|' + JSON.stringify(Object.keys(o)) + '|' + o.k1;
            """).Should().Be("1|[\"k1\"]|6");
    }

    // ------------------------------------------------------------------ object literal

    [Fact]
    public void ComputedObjectLiteralKeyRunsOnceAcrossAnAwait()
    {
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var r = { a: 0, [f()]: await Promise.resolve(7) };
              return calls + '|' + JSON.stringify(r);
            })();
            """).Should().Be("1|{\"a\":0,\"k1\":7}");
    }

    [Fact]
    public void ComputedObjectLiteralKeyRunsOnceAcrossAYield()
    {
        Run($$"""
            {{CountingKey}}
            var r;
            function* g() { r = { a: 0, [f()]: yield 1 }; }
            var it = g();
            it.next();
            it.next(9);
            calls + '|' + JSON.stringify(r);
            """).Should().Be("1|{\"a\":0,\"k1\":9}");
    }

    [Fact]
    public void EveryComputedObjectLiteralKeyRunsOnceWhenSeveralPropertiesSuspend()
    {
        // Each property parks its own key at its own index, so the slot must be consumed by the
        // property that parked it and never carried into the next one.
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var r = { [f()]: await Promise.resolve(1), [f()]: await Promise.resolve(2), [f()]: 3 };
              return calls + '|' + JSON.stringify(r);
            })();
            """).Should().Be("3|{\"k1\":1,\"k2\":2,\"k3\":3}");
    }

    [Fact]
    public void ComputedObjectLiteralKeyAfterANonSuspendingPropertyRunsOnce()
    {
        Run($$"""
            {{CountingKey}}
            var r;
            function* g() { r = { [f()]: 1, [f()]: yield 2 }; }
            var it = g();
            it.next();
            it.next(8);
            calls + '|' + JSON.stringify(r);
            """).Should().Be("2|{\"k1\":1,\"k2\":8}");
    }
}
