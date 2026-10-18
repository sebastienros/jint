#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A destructuring <em>assignment</em> whose right-hand side suspends — <c>({ a } = await p)</c>,
/// <c>[a] = await p</c> — must leave the frame the way every other consumer of the suspension placeholder
/// does, so that the resume continues at that statement instead of replaying the body.
/// <para>
/// <c>DestructuringPatternAssignmentExpression.EvaluateInternal</c> hands whatever the suspended right-hand
/// side returned to <c>ProcessPatterns</c> as though it were the awaited value. An <c>await</c> suspends by
/// returning a plain <see cref="Jint.Native.JsValue.Undefined"/>, and both pattern lanes open with
/// <c>ToObject</c>, which raises a <c>TypeError</c> while the frame is suspended. In an async function
/// <c>AsyncBlockStart</c> swallows the throw — the state is still <c>SuspendedAwait</c> — but it unwinds
/// through <c>JintStatementList.Execute</c> before the resume position is recorded, so the resume replays
/// the body from statement 0: every un-awaited side effect before the assignment runs once more per
/// suspension point, and inside a loop it is the iteration that repeats. A re-entrancy guard turns the
/// replay into a truncated body. A destructuring <em>declaration</em> (<c>const { a } = await p</c>), a
/// plain assignment and a member-expression target are all correct; the pattern as an assignment
/// <em>target</em> is the whole trigger.
/// </para>
/// <para>
/// A <c>yield</c> hands back its own operand instead, so the generator lane does not throw: it
/// destructures the <em>yielded</em> value, writing the targets and running the operand's getters during
/// the suspension, before <c>next()</c> has been given the value they are supposed to receive — and when
/// that operand cannot be destructured at all, the <c>TypeError</c> comes straight out of <c>next()</c>.
/// </para>
/// <para>
/// Each probe pushes <c>pre</c> before the suspension and <c>post</c> after it and asserts the log is
/// exactly <c>pre,post</c>, the way <see cref="SuspendedOptionalChainTests"/> does for the
/// member-expression consumers of the same placeholder (sebastienros/jint#4086). The tests named
/// <c>…Control</c> pass on the unfixed tree and are here to bound the claim, not to evidence it.
/// </para>
/// </summary>
public class SuspendedDestructuringAssignmentTests
{
    /// <summary>
    /// Runs <paramref name="body"/> as the body of an async function bracketed by the <c>pre</c>/<c>post</c>
    /// markers and returns <c>"&lt;report&gt;|&lt;log&gt;"</c>.
    /// </summary>
    private static string AsyncProbe(string setup, string body, string report) => new Engine().Evaluate($$"""
        var log = [];
        {{setup}}
        async function m() {
          log.push('pre');
          {{body}}
          log.push('post');
          return {{report}};
        }
        m().then(function (r) { return String(r) + '|' + log.join(','); });
        """).UnwrapIfPromise().AsString();

    /// <summary>
    /// The synchronous-generator twin of <see cref="AsyncProbe"/>: the suspension is a <c>yield</c> and
    /// <paramref name="sent"/> is what <c>next()</c> resumes it with, standing in for the awaited value.
    /// </summary>
    private static string GeneratorProbe(string setup, string body, string report, string sent) => new Engine().Evaluate($$"""
        var log = [];
        {{setup}}
        function* g() {
          log.push('pre');
          {{body}}
          log.push('post');
          return {{report}};
        }
        var it = g();
        it.next();
        var res = it.next({{sent}});
        String(res.value) + '|' + log.join(',');
        """).AsString();

    // ------------------------------------------------------------------ the reported shapes

    [Test]
    public void AnObjectPatternAssignedFromAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var p = Promise.resolve({ a: 1 }); var a = null;", "({ a } = await p);", "a")
            .Should().Be("1|pre,post");
    }

    [Test]
    public void AnArrayPatternAssignedFromAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var p = Promise.resolve([2]); var b = null;", "[b] = await p;", "b")
            .Should().Be("2|pre,post");
    }

    /// <summary>
    /// Two suspension points in one body: the unfixed tree replays once per point, so the log reads
    /// <c>pre,pre,pre,post</c> — the completed-await memo makes the replayed <c>await</c>s return instantly,
    /// which is why the answer is still right and the whole defect is visible only in the side effects.
    /// </summary>
    [Test]
    public void TwoDestructuringAssignmentsInOneBodyRunTheirSideEffectsOnce()
    {
        AsyncProbe(
            "var p = Promise.resolve({ a: 1 }); var q = Promise.resolve([2]); var a = null; var b = null;",
            "({ a } = await p); [b] = await q;",
            "a + '/' + b").Should().Be("1/2|pre,post");
    }

    /// <summary>
    /// Inside a loop the iteration is what repeats: the loop statement knows the suspension lies inside
    /// it and skips its initializer on the replay, but the body's own statement list recorded nothing and
    /// starts over.
    /// </summary>
    [Test]
    public void ADestructuringAssignmentInsideALoopRunsEachIterationOnce()
    {
        AsyncProbe(
            "var a = null; var sum = 0;",
            "for (var i = 0; i < 3; i++) { log.push('it' + i); ({ a } = await Promise.resolve({ a: i })); sum += a; }",
            "sum").Should().Be("3|pre,it0,it1,it2,post");
    }

    [Test]
    public void ADestructuringAssignmentAsAnIfConsequentRunsItsSideEffectsOnce()
    {
        AsyncProbe("var p = Promise.resolve({ a: 1 }); var a = null; var flag = true;", "if (flag) ({ a } = await p);", "a")
            .Should().Be("1|pre,post");
    }

    /// <summary>
    /// A primitive operand takes <c>JintAwaitExpression</c>'s shortcut lane, which skips the wrapper
    /// promise but suspends through the same sentinel.
    /// </summary>
    [Test]
    public void AnObjectPatternAssignedFromAnAwaitedPrimitiveRunsItsSideEffectsOnce()
    {
        AsyncProbe("var n = null;", "({ length: n } = await 'abc');", "n")
            .Should().Be("3|pre,post");
    }

    /// <summary>
    /// The assignment's own value is its right-hand side, so it can sit inside a larger expression; the
    /// resume has to re-run the pattern and hand that value on.
    /// </summary>
    [Test]
    public void ADestructuringAssignmentNestedInALargerExpressionRunsItsSideEffectsOnce()
    {
        AsyncProbe("var p = Promise.resolve({ a: 1 }); var a = null;", "var r = ({ a } = await p).a + a;", "r")
            .Should().Be("2|pre,post");
    }

    /// <summary>
    /// The shape that made the defect fatal rather than merely duplicated: the replay takes the guard's
    /// early return, so the assignment never happens and everything after it is skipped.
    /// </summary>
    [Test]
    public void ADestructuringAssignmentBehindAReEntrancyGuardCompletes()
    {
        new Engine().Evaluate("""
            var log = [];
            var s = { started: false };
            var a = null;
            async function m() {
                if (s.started) { log.push('guard'); return; }
                s.started = true;
                log.push('pre');
                ({ a } = await Promise.resolve({ a: 1 }));
                log.push('post');
            }
            m().then(function () { return 'a=' + String(a) + ' log=' + log.join(','); });
            """).UnwrapIfPromise().AsString().Should().Be("a=1 log=pre,post");
    }

    // ------------------------------------------------------------------ the shapes that were always right

    /// <summary>
    /// A destructuring <em>declaration</em> evaluates its initializer in <c>JintVariableDeclaration</c>,
    /// which bails on the sentinel before binding. Control.
    /// </summary>
    [Test]
    public void ADestructuringDeclarationFromAnAwaitedValueIsAControl()
    {
        AsyncProbe("var p = Promise.resolve({ a: 1 });", "const { a } = await p;", "a")
            .Should().Be("1|pre,post");
    }

    [Test]
    public void APlainAssignmentFromAnAwaitedValueIsAControl()
    {
        AsyncProbe("var p = Promise.resolve(1); var a = null;", "a = await p;", "a")
            .Should().Be("1|pre,post");
    }

    [Test]
    public void AMemberTargetAssignedFromAnAwaitedValueIsAControl()
    {
        AsyncProbe("var p = Promise.resolve(1); var o = {};", "o.a = await p;", "o.a")
            .Should().Be("1|pre,post");
    }

    /// <summary>
    /// The suspension is inside the pattern rather than on the right-hand side: the default-value branch
    /// of <c>ProcessPatterns</c> already carries its check. Control.
    /// </summary>
    [Test]
    public void AnAwaitInAPatternDefaultIsAControl()
    {
        AsyncProbe("var p = Promise.resolve(4); var a = null;", "({ a = await p } = {});", "a")
            .Should().Be("4|pre,post");
    }

    // ------------------------------------------------------------------ what the resume re-runs

    /// <summary>
    /// The fix hands the placeholder back and lets the resume re-run the whole expression, so the
    /// question it invites is whether work done on the right-hand side <em>before</em> the suspension
    /// runs twice. It does not: a sequence parks the operand index it suspended at and resumes there, and
    /// a call cannot run until its awaited argument exists.
    /// </summary>
    [Test]
    public void ASideEffectBeforeTheAwaitOnTheSameRightHandSideRunsOnce()
    {
        AsyncProbe("var p = Promise.resolve({ a: 1 }); var a = null; var calls = 0;", "({ a } = (calls++, await p));", "a + '/' + calls")
            .Should().Be("1/1|pre,post");
    }

    [Test]
    public void ACallAroundTheAwaitOnTheRightHandSideRunsOnce()
    {
        AsyncProbe(
            "var p = Promise.resolve(3); var b = null; var calls = 0; function g(v) { calls++; return { b: v }; }",
            "({ b } = g(await p));",
            "b + '/' + calls").Should().Be("3/1|pre,post");
    }

    // ------------------------------------------------------------------ the generator twins

    /// <summary>
    /// The placeholder a <c>yield</c> hands back is its own operand, so nothing throws here; the lane
    /// destructures the <em>yielded</em> value instead. Observed between the two <c>next()</c> calls:
    /// the target must still hold what it held before, and the operand's getter must not have run.
    /// </summary>
    [Test]
    public void AnObjectPatternAssignedFromAYieldedValueTouchesNothingUntilTheResume()
    {
        new Engine().Evaluate("""
            var log = [];
            var seen = [];
            var src = { get a() { seen.push('get'); return 1; } };
            var a = 'untouched';
            function* g() {
              log.push('pre');
              ({ a } = yield src);
              log.push('post');
              return a;
            }
            var it = g();
            var first = it.next();
            var mid = 'yielded=' + String(first.value === src) + ' a=' + String(a) + ' seen=' + seen.join(',');
            var res = it.next({ a: 5 });
            mid + ' | ' + String(res.value) + ' a=' + String(a) + ' seen=' + seen.join(',') + '|' + log.join(',');
            """).AsString().Should().Be("yielded=true a=untouched seen= | 5 a=5 seen=|pre,post");
    }

    [Test]
    public void AnArrayPatternAssignedFromAYieldedValueTouchesNothingUntilTheResume()
    {
        new Engine().Evaluate("""
            var log = [];
            var b = 'untouched';
            function* g() {
              log.push('pre');
              [b] = yield [9];
              log.push('post');
              return b;
            }
            var it = g();
            it.next();
            var mid = 'b=' + String(b);
            var res = it.next([6]);
            mid + ' | ' + String(res.value) + ' b=' + String(b) + '|' + log.join(',');
            """).AsString().Should().Be("b=untouched | 6 b=6|pre,post");
    }

    /// <summary>
    /// When the yielded operand cannot be destructured at all, the generator lane's symptom is the
    /// <c>TypeError</c> itself: nothing swallows one raised while the frame is suspended, so it comes
    /// straight out of <c>next()</c> instead of turning into a replay.
    /// </summary>
    [Test]
    public void AnArrayPatternAssignedFromAYieldedPrimitiveCompletes()
    {
        GeneratorProbe("var b = null;", "[b] = yield 1;", "b", "[6]").Should().Be("6|pre,post");
    }

    /// <summary>
    /// An async generator suspends through the same machinery again, with the async half's swallowing
    /// behaviour, so it replays rather than throws.
    /// </summary>
    [Test]
    public void AnObjectPatternAssignedFromAnAwaitedValueInAnAsyncGeneratorRunsItsSideEffectsOnce()
    {
        new Engine().Evaluate("""
            var log = [];
            var a = null;
            async function* ag() {
              log.push('pre');
              ({ a } = await Promise.resolve({ a: 1 }));
              log.push('post');
              yield a;
            }
            ag().next().then(function (r) { return String(r.value) + '|' + log.join(','); });
            """).UnwrapIfPromise().AsString().Should().Be("1|pre,post");
    }
}
