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
/// <para>
/// The complementary case is a suspension <em>inside</em> the key, <c>({ [f() + await g()]: 1 })</c>,
/// which nothing above can park because the key never finished producing a value. What the key's own
/// sub-expressions park — a binary operator's left operand, an addition chain's accumulator, an
/// argument buffer — is keyed on the handler instance that parked it, so the key expression has to be
/// the same handler on the replay as it was on the way in. Every key position now takes its handler
/// from the engine's per-node cache instead of building a throwaway one per evaluation
/// (<c>Engine.GetOrBuildPropertyKeyExpression</c>).
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    // ------------------------------------------------- suspension INSIDE the key expression

    [Test]
    public void AComputedObjectLiteralKeyThatSuspendsMidwayRunsItsSideEffectsOnce()
    {
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var r = { [f() + await Promise.resolve('!')]: 1 };
              return calls + '|' + JSON.stringify(r);
            })();
            """).Should().Be("1|{\"k1!\":1}");
    }

    [Test]
    public void AComputedObjectLiteralKeyThatSuspendsMidwayOnAYieldRunsItsSideEffectsOnce()
    {
        Run($$"""
            {{CountingKey}}
            var r;
            function* g() { r = { [f() + (yield 1)]: 5 }; }
            var it = g();
            it.next();
            it.next('!');
            calls + '|' + JSON.stringify(r);
            """).Should().Be("1|{\"k1!\":5}");
    }

    [Test]
    public void AComputedKeyThatIsItselfTheAwaitRunsOnce()
    {
        // Already correct before the handler-identity fix — an await finds its settled value by AST node
        // — but it is the degenerate case of the shape above, so pin it.
        RunAsync("""
            var calls = 0;
            function g() { calls++; return Promise.resolve('k'); }
            (async function () {
              var r = { [await g()]: 1 };
              return calls + '|' + JSON.stringify(r);
            })();
            """).Should().Be("1|{\"k\":1}");
    }

    [Test]
    public void AnEarlierComputedKeySuspendingMidwayDoesNotShiftTheLaterKeys()
    {
        // Re-running the first key would consume the counter the second key then reads, so the whole
        // literal came out keyed one step along: { k2: 1, k3: 2 } instead of { k1: 1, k2: 2 }.
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var r = { [f() + await Promise.resolve('')]: 1, [f()]: 2 };
              return calls + '|' + JSON.stringify(r);
            })();
            """).Should().Be("2|{\"k1\":1,\"k2\":2}");
    }

    [Test]
    public void AComputedMethodKeyThatSuspendsMidwayDefinesOnlyTheKeyItResolvesTo()
    {
        // A method is defined from inside MethodDefinitionEvaluation, before the object literal's own
        // suspension check runs, so the placeholder undefined the suspended key produced used to be
        // converted and defined as a property literally named "undefined" — on the very object the
        // resume carries forward, which therefore could never take it back.
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var r = { [f() + await Promise.resolve('!')]() { return 7; } };
              return calls + '|' + JSON.stringify(Object.keys(r)) + '|' + r['k1!']();
            })();
            """).Should().Be("1|[\"k1!\"]|7");
    }

    [Test]
    public void AComputedAccessorKeyThatSuspendsMidwayRunsItsSideEffectsOnce()
    {
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var r = { get [f() + await Promise.resolve('!')]() { return 7; } };
              return calls + '|' + JSON.stringify(Object.keys(r)) + '|' + r['k1!'];
            })();
            """).Should().Be("1|[\"k1!\"]|7");
    }

    [Test]
    public void TheSameComputedKeySuspendingOnEveryIterationRunsItsSideEffectsOncePerIteration()
    {
        // The key handler is now shared by every evaluation of the node, so what one iteration parks
        // must be consumed and cleared by that same iteration and never inherited by the next.
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var r = [];
              for (var i = 0; i < 3; i++) {
                r.push({ [f() + await Promise.resolve(i)]: i });
              }
              return calls + '|' + JSON.stringify(r);
            })();
            """).Should().Be("3|[{\"k10\":0},{\"k21\":1},{\"k32\":2}]");
    }

    [Test]
    public void TwoGeneratorsSuspendedInsideTheSameComputedKeyKeepTheirOwnParkedState()
    {
        // One handler, two suspendables: the parked state lives in each generator's own suspend-data
        // dictionary, so sharing the handler must not let one instance read the other's.
        Run($$"""
            {{CountingKey}}
            var r = [];
            function* g(tag) { r.push({ [f() + tag + (yield 1)]: tag }); }
            var a = g('a'), b = g('b');
            a.next(); b.next();
            a.next('1'); b.next('2');
            calls + '|' + JSON.stringify(r);
            """).Should().Be("2|[{\"k1a1\":\"a\"},{\"k2b2\":\"b\"}]");
    }

    // ------------------------------------------------------------------ class bodies

    [Test]
    public void AComputedClassMethodKeyThatSuspendsMidwayRunsItsSideEffectsOnce()
    {
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var C = class { [f() + await Promise.resolve('!')]() { return 7; } };
              return calls + '|' + JSON.stringify(Object.getOwnPropertyNames(C.prototype));
            })();
            """).Should().Be("1|[\"constructor\",\"k1!\"]");
    }

    [Test]
    public void AComputedStaticClassMethodKeyThatSuspendsMidwayRunsItsSideEffectsOnce()
    {
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var C = class { static [f() + await Promise.resolve('!')]() { return 7; } };
              return calls + '|' + C['k1!']();
            })();
            """).Should().Be("1|7");
    }

    [Test]
    public void AComputedClassFieldKeyThatSuspendsMidwayRunsItsSideEffectsOnce()
    {
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var C = class { [f() + await Promise.resolve('!')] = 7; };
              return calls + '|' + JSON.stringify(new C());
            })();
            """).Should().Be("1|{\"k1!\":7}");
    }

    [Test]
    public void AComputedClassMethodKeyThatSuspendsMidwayOnAYieldRunsItsSideEffectsOnce()
    {
        Run($$"""
            {{CountingKey}}
            var C;
            function* g() { C = class { [f() + (yield 1)]() { return 7; } }; }
            var it = g();
            it.next();
            it.next('!');
            calls + '|' + JSON.stringify(Object.getOwnPropertyNames(C.prototype));
            """).Should().Be("1|[\"constructor\",\"k1!\"]");
    }

    // ------------------------------------------------------------------ destructuring

    [Test]
    public void AComputedDestructuringKeyThatSuspendsMidwayRunsItsSideEffectsOnce()
    {
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var v;
              ({ [f() + await Promise.resolve('!')]: v } = { 'k1!': 11 });
              return calls + '|' + v;
            })();
            """).Should().Be("1|11");
    }

    [Test]
    public void AComputedKeyInADestructuringDeclarationThatSuspendsMidwayRunsItsSideEffectsOnce()
    {
        RunAsync($$"""
            {{CountingKey}}
            (async function () {
              var { [f() + await Promise.resolve('!')]: v } = { 'k1!': 11 };
              return calls + '|' + v;
            })();
            """).Should().Be("1|11");
    }
}
