#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A frame that has suspended on an <c>await</c> or a <c>yield</c> must not dereference anything the
/// suspension produced, and the resume position it saved must survive an exception in flight through it.
/// <para>
/// <c>await</c> and <c>yield</c> suspend by returning a plain <see cref="Jint.Native.JsValue.Undefined"/>,
/// and the enclosing member link turns that into a sentinel <c>Reference(undefined, undefined)</c>. Every
/// consumer of that sentinel is supposed to bail on <c>EvaluationContext.IsSuspended()</c> before reading
/// it; the ones that did not read <c>undefined.undefined</c>, which raises a <c>TypeError</c> *while the
/// frame is suspended*. That throw is swallowed by <c>AsyncBlockStart</c> — the state is still
/// <c>SuspendedAwait</c> — but on its way out it wiped the statement-list resume position, so the resume
/// replayed the body from statement 0: one extra run of every un-awaited side effect per suspension point,
/// and a re-entrancy guard (<c>if (inited) return;</c>) silently truncating everything after it
/// (sebastienros/jint#4086). In a generator nothing swallows it and the <c>TypeError</c> comes straight
/// out of <c>next()</c>.
/// </para>
/// <para>
/// Each probe here pushes <c>"pre"</c> before the suspension and <c>"post"</c> after it and asserts the
/// log is exactly <c>pre,post</c>: a replayed body shows up as a second <c>pre</c>, one per suspension
/// point, which is the only symptom the swallowed throw leaves behind. The tests named
/// <c>…Control</c> pass on the unfixed tree and are here to bound the claim, not to evidence it.
/// </para>
/// </summary>
public class SuspendedOptionalChainTests
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

    // ------------------------------------------------------------------ the reported repro

    private const string ReportedChain = """
        const h = { M: async function () { return { P: async function () { return { DATA: 0.05 }; } }; } };
        """;

    /// <summary>
    /// The issue's own script. The guard makes the replay fatal rather than merely duplicated: the second
    /// pass takes the early return, so the assignment never happens and everything after it is skipped.
    /// </summary>
    [Test]
    public void TheReportedChainCompletesOnceBehindAReEntrancyGuard()
    {
        new Engine().Evaluate($$"""
            var log = [];
            {{ReportedChain}}
            const s = { f: false };
            async function m() {
                if (s.f) { log.push("guard"); return; }
                s.f = true;
                log.push("pre");
                s.r = (await (await h?.M())?.P())?.DATA;
                log.push("post");
            }
            m().then(function () { return 'r=' + String(s.r) + ' log=' + log.join(','); });
            """).UnwrapIfPromise().AsString().Should().Be("r=0.05 log=pre,post");
    }

    /// <summary>
    /// Without the guard the body still produces the right answer — the completed-await memo makes the
    /// replayed <c>await</c>s return instantly — so the whole defect is visible only in the un-awaited
    /// side effects: one extra <c>pre</c> per suspension point, three of them in this chain.
    /// </summary>
    [Test]
    public void TheReportedChainRunsItsUnawaitedSideEffectsOnce()
    {
        AsyncProbe(ReportedChain, "var r = (await (await h?.M())?.P())?.DATA;", "r")
            .Should().Be("0.05|pre,post");
    }

    // ------------------------------------------------------------------ the shapes that reach the unguarded lane

    /// <summary>
    /// No optional chain anywhere: the guarded fast lane in <c>JintMemberExpression.GetValue</c> also
    /// requires a literal string property, so any computed read of an awaited value falls to the same
    /// unguarded lane.
    /// </summary>
    [Test]
    public void AComputedIndexReadOfAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var p = Promise.resolve(['zero', 'one']);", "var r = (await p)[0];", "r")
            .Should().Be("zero|pre,post");
    }

    /// <summary>
    /// The key being a string changes nothing — it is the key being an <em>expression</em> rather than a
    /// literal that disarms the fast lane's guard.
    /// </summary>
    [Test]
    public void AComputedNameReadOfAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var p = Promise.resolve({ x: 7 }); var k = 'x';", "var r = (await p)[k];", "r")
            .Should().Be("7|pre,post");
    }

    [Test]
    public void AnOptionalReadOfAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var p = Promise.resolve({ x: 7 });", "var r = (await p)?.x;", "r")
            .Should().Be("7|pre,post");
    }

    /// <summary>
    /// The `?.(` link of the reported chain on its own: the call expression's callee lane already carries
    /// its suspension check, so this half was never broken. Control.
    /// </summary>
    [Test]
    public void AnOptionalCallOnAnAwaitedReceiverIsAControl()
    {
        AsyncProbe(ReportedChain, "var r = (await h?.M())?.P();", "typeof r")
            .Should().Be("object|pre,post");
    }

    /// <summary>
    /// The suspension is in the argument rather than in the callee, so the call expression's own
    /// suspension checks already cover it. Control.
    /// </summary>
    [Test]
    public void AnOptionalCallWithAnAwaitedArgumentIsAControl()
    {
        AsyncProbe("var h = { M: function (v) { return v + 1; } }; var p = Promise.resolve(1);", "var r = h?.M?.(await p);", "r")
            .Should().Be("2|pre,post");
    }

    /// <summary>
    /// Plain <c>.</c> member access on an awaited value takes the guarded fast lane and has always been
    /// correct. Control: the fix must not be credited with it.
    /// </summary>
    [Test]
    public void APlainDotChainIsAControl()
    {
        AsyncProbe(ReportedChain, "var r = (await (await h.M()).P()).DATA;", "r")
            .Should().Be("0.05|pre,post");
    }

    // ------------------------------------------------------------------ nothing may be read with the sentinel

    /// <summary>
    /// The complementary half: when the <em>property</em> side suspends, the link hands on a
    /// <c>Reference(base, undefined)</c> and the unguarded lane completed the read — an observable
    /// <c>base[undefined]</c> probe, which a Proxy or a getter sees.
    /// </summary>
    [Test]
    public void AnAwaitedKeyNeverProbesTheBaseWithTheSuspensionSentinel()
    {
        AsyncProbe(
            "var seen = []; var t = new Proxy({ x: 42 }, { get: function (o, k) { seen.push(String(k)); return o[k]; } }); var kp = Promise.resolve('x');",
            "var r = t[await kp];",
            "r + '/' + seen.join(',')").Should().Be("42/x|pre,post");
    }

    [Test]
    public void AnAwaitedKeyOnAnOptionalReadNeverProbesTheBaseWithTheSuspensionSentinel()
    {
        AsyncProbe(
            "var seen = []; var t = new Proxy({ x: 42 }, { get: function (o, k) { seen.push(String(k)); return o[k]; } }); var kp = Promise.resolve('x');",
            "var r = t?.[await kp];",
            "r + '/' + seen.join(',')").Should().Be("42/x|pre,post");
    }

    /// <summary>
    /// The same sentinel reached a simple assignment, which parks the resolved left-hand
    /// <c>Reference</c> so a side-effecting key is not re-run. Parking an unfinished one made the resume
    /// assign to the key <c>"undefined"</c> and never to the real one — a wrong answer rather than a
    /// duplicated side effect.
    /// </summary>
    [Test]
    public void AnAwaitedKeyOnAnAssignmentTargetLandsOnTheRealKey()
    {
        AsyncProbe("var o = {}; var kp = Promise.resolve('x');", "o[await kp] = 1;", "JSON.stringify(o)")
            .Should().Be("{\"x\":1}|pre,post");
    }

    // ------------------------------------------------------------------ the other consumers of the sentinel

    [Test]
    public void AnUpdateOfAMemberOfAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var o = { x: 1 }; var p = Promise.resolve(o);", "(await p).x++;", "o.x")
            .Should().Be("2|pre,post");
    }

    [Test]
    public void TypeOfAMemberOfAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var p = Promise.resolve({ x: 1 });", "var t = typeof (await p).x;", "t")
            .Should().Be("number|pre,post");
    }

    [Test]
    public void DeletingAMemberOfAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var o = { x: 1 }; var p = Promise.resolve(o);", "delete (await p).x;", "'x' in o")
            .Should().Be("false|pre,post");
    }

    [Test]
    public void ACompoundAssignmentToAMemberOfAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var o = { x: 1 }; var p = Promise.resolve(o);", "(await p).x += 1;", "o.x")
            .Should().Be("2|pre,post");
    }

    /// <summary>
    /// This one did not merely replay: the sentinel <c>Reference</c> was parked as the assignment's
    /// left-hand side and the resume tried to write through it, so the host saw the swallowed
    /// <c>TypeError</c> after all — as a rejection of the async function's promise.
    /// </summary>
    [Test]
    public void AnAssignmentToAMemberOfAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var o = { x: 1 }; var p = Promise.resolve(o);", "(await p).x = 9;", "o.x")
            .Should().Be("9|pre,post");
    }

    [Test]
    public void AnObjectPatternTargetingAMemberOfAnAwaitedValueRunsItsSideEffectsOnce()
    {
        AsyncProbe("var o = {}; var p = Promise.resolve(o);", "({ q: (await p).a } = { q: 5 });", "JSON.stringify(o)")
            .Should().Be("{\"a\":5}|pre,post");
    }

    // ------------------------------------------------------------------ the generator twin

    /// <summary>
    /// Generators share <c>ISuspendable</c> and its <c>SuspendData</c> with async functions, so they hit
    /// the same unguarded lanes — but nothing swallows the <c>TypeError</c> raised while the frame is
    /// suspended, so it came straight out of <c>next()</c> instead of turning into a replay.
    /// </summary>
    [Test]
    public void AnOptionalComputedReadOfAYieldedValueCompletes()
    {
        GeneratorProbe("", "var r = (yield 1)?.[0];", "r", "['zero']").Should().Be("zero|pre,post");
    }

    [Test]
    public void AComputedReadOfAYieldedValueCompletes()
    {
        GeneratorProbe("", "var r = (yield 1)[0];", "r", "['zero']").Should().Be("zero|pre,post");
    }

    [Test]
    public void AnOptionalReadOfAYieldedValueCompletes()
    {
        GeneratorProbe("", "var r = (yield 1)?.x;", "r", "{ x: 5 }").Should().Be("5|pre,post");
    }

    [Test]
    public void AnUpdateOfAMemberOfAYieldedValueCompletes()
    {
        GeneratorProbe("var o = { x: 1 };", "(yield 1).x++;", "o.x", "o").Should().Be("2|pre,post");
    }

    [Test]
    public void TypeOfAMemberOfAYieldedValueCompletes()
    {
        GeneratorProbe("", "var t = typeof (yield 1).x;", "t", "{ x: 1 }").Should().Be("number|pre,post");
    }

    [Test]
    public void DeletingAMemberOfAYieldedValueCompletes()
    {
        GeneratorProbe("var o = { x: 1 };", "delete (yield 1).x;", "'x' in o", "o").Should().Be("false|pre,post");
    }

    [Test]
    public void ACompoundAssignmentToAMemberOfAYieldedValueCompletes()
    {
        GeneratorProbe("var o = { x: 1 };", "(yield 1).x += 1;", "o.x", "o").Should().Be("2|pre,post");
    }

    [Test]
    public void AnAssignmentToAMemberOfAYieldedValueCompletes()
    {
        GeneratorProbe("var o = { x: 1 };", "(yield 1).x = 9;", "o.x", "o").Should().Be("9|pre,post");
    }

    /// <summary>
    /// Plain <c>.</c> on a yielded value takes the guarded fast lane. Control.
    /// </summary>
    [Test]
    public void APlainDotReadOfAYieldedValueIsAControl()
    {
        GeneratorProbe("", "var r = (yield 1).x;", "r", "{ x: 3 }").Should().Be("3|pre,post");
    }

    /// <summary>
    /// An async generator suspends through the same machinery again, with the async half's swallowing
    /// behaviour, so it replays rather than throws.
    /// </summary>
    [Test]
    public void AComputedReadOfAnAwaitedValueInAnAsyncGeneratorRunsItsSideEffectsOnce()
    {
        new Engine().Evaluate("""
            var log = [];
            async function* ag() {
              log.push('pre');
              var v = (await Promise.resolve(['z']))[0];
              log.push('post');
              yield v;
            }
            ag().next().then(function (r) { return String(r.value) + '|' + log.join(','); });
            """).UnwrapIfPromise().AsString().Should().Be("z|pre,post");
    }
}
