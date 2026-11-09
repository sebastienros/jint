#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A call whose argument list suspends must resume in its arguments, with the function and the
/// <c>this</c> value it resolved before suspending, rather than evaluating its callee again
/// (sebastienros/jint#4314).
/// <para>
/// Jint resumes a suspended body by replaying the statement it suspended in. The argument list keeps the
/// arguments that already completed (<c>ExpressionCache</c>'s resume buffer), so a single call such as
/// <c>f(g(), await x)</c> was already right; what nothing kept was the callee. EvaluateCall resolves the
/// function and its this value once, before ArgumentListEvaluation
/// (https://tc39.es/ecma262/#sec-function-calls-runtime-semantics-evaluation), but the replay evaluated the
/// callee expression again: the inner call of <c>o.m(1).m(yield 0)</c> ran twice, a getter on the callee was
/// read twice, a callee or receiver reassigned inside the arguments was called in its new form, and in an
/// async generator the replayed callee re-awaited and never finished. Two shapes in the same handler did not
/// stop at a suspended argument list at all: a direct <c>eval</c> ran its code at the suspension, and
/// <c>super(...)</c> in an async arrow constructed the parent with the suspension placeholder and then threw
/// on the resume.
/// </para>
/// <para>
/// Every probe runs in a generator, an async function and an async generator (suspending at an
/// <c>await</c>, and at a <c>yield</c>), each <c>yield</c> resumed with its own operand, and returns
/// <c>&lt;report&gt;|&lt;log&gt;</c>, where the log is bracketed by <c>pre</c> and <c>post</c> so a body that
/// stops part-way shows. The expected logs are what Node 24 prints and what the spec's evaluation order
/// gives. The tests named <c>…Control</c> pass on the unfixed tree and bound the claim.
/// </para>
/// </summary>
public class SuspendedCallArgumentTests
{
    public enum Frame
    {
        Generator,
        Async,
        AsyncGeneratorAwait,
        AsyncGeneratorYield,
    }

    private const string Setup = """
        var log = [];
        var reads = 0;
        var o = { m(x) { log.push('m(' + x + ')'); return o; } };
        var g = { get m() { reads++; return function (x) { log.push('g(' + x + ')'); return this === g; }; } };
        function f(x) { log.push('f(' + x + ')'); return x; }
        function K(a, b) { log.push('K(' + a + ',' + b + ')'); }
        function fac() { log.push('fac'); return { K: K, o: o, g: g }; }
        function key() { log.push('key'); return 'm'; }
        function arr() { log.push('arr'); return ['a']; }
        var ok = { get K() { reads++; return K; } };
        """;

    /// <summary>
    /// Runs <paramref name="body"/> in <paramref name="frame"/>, with every <c>SUSPEND</c> token replaced by
    /// that frame's suspension keyword, and returns <c>"&lt;report&gt;|&lt;log&gt;"</c>. Both drivers stop
    /// after a fixed number of steps, so a body that never finishes fails the assertion rather than spinning.
    /// </summary>
    private static string Probe(Frame frame, string body, string report = "''")
    {
        var keyword = frame is Frame.Generator or Frame.AsyncGeneratorYield ? "yield" : "await";
        body = body.Replace("SUSPEND", keyword);

        if (frame == Frame.Generator)
        {
            return new Engine().Evaluate($$"""
                {{Setup}}
                function* body() {
                  log.push('pre');
                  {{body}}
                  log.push('post');
                  return {{report}};
                }
                var it = body();
                var res = it.next();
                for (var i = 0; !res.done && i < 8; i++) res = it.next(res.value);
                String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
                """).AsString();
        }

        if (frame == Frame.Async)
        {
            return new Engine().Evaluate($$"""
                {{Setup}}
                async function body() {
                  log.push('pre');
                  {{body}}
                  log.push('post');
                  return {{report}};
                }
                body().then(function (r) { return String(r) + '|' + log.join(','); });
                """).UnwrapIfPromise().AsString();
        }

        return new Engine().Evaluate($$"""
            {{Setup}}
            async function* body() {
              log.push('pre');
              {{body}}
              log.push('post');
              return {{report}};
            }
            async function drive() {
              var it = body();
              var res = await it.next();
              for (var i = 0; !res.done && i < 8; i++) res = await it.next(res.value);
              return String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
            }
            drive();
            """).UnwrapIfPromise().AsString();
    }

    // ------------------------------------------------------------------ the reported shape

    /// <summary>
    /// The issue's own shape: the inner <c>o.m(1)</c> is the callee's object, and the replay ran it again.
    /// On the unfixed tree this reads <c>m(1),m(1),m(2),m(3)</c>, and the async generator never finishes,
    /// because the replayed callee re-awaits.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void TheInnerCallOfAChainRunsOnceWhenALaterArgumentSuspends(Frame frame)
    {
        Probe(frame, "o.m(1).m((SUSPEND 2)).m(3);")
            .Should().Be("|pre,m(1),m(2),m(3),post");
    }

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void TheInnerCallOfAChainInAnInitializerRunsOnce(Frame frame)
    {
        Probe(frame, "var r = o.m(1).m((SUSPEND 2));", "r === o")
            .Should().Be("true|pre,m(1),m(2),post");
    }

    /// <summary>
    /// The async generator from the issue's family that used to hang: the chain awaits in one link and
    /// yields in the next, and every replay of the yielding link's callee re-ran the awaiting one.
    /// </summary>
    [Test]
    public void AnAsyncGeneratorChainThatAwaitsAndThenYieldsFinishes()
    {
        new Engine().Evaluate("""
            var log = [];
            var o = { m(x) { log.push('m(' + x + ')'); return o; } };
            async function* ag() { o.m(1).m(await 'a').m(yield 'b'); log.push('end'); }
            async function drive() {
              var it = ag();
              var res = await it.next();
              for (var i = 0; !res.done && i < 8; i++) res = await it.next(res.value);
              return log.join(',') + (res.done ? '' : ' (not done after 8 steps)');
            }
            drive();
            """).UnwrapIfPromise().AsString()
            .Should().Be("m(1),m(a),m(b),end");
    }

    // ------------------------------------------------------------------ nested calls in arguments

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void AChainInsideAnotherCallsArgumentRunsEachCallOnce(Frame frame)
    {
        Probe(frame, "f(o.m(1).m((SUSPEND 2)) === o);")
            .Should().Be("|pre,m(1),m(2),f(true),post");
    }

    /// <summary>
    /// Every argument that completed before the suspension is kept by the argument list's own buffer; this
    /// bounds the claim to the callee.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ArgumentsBeforeTheSuspensionRunOnceControl(Frame frame)
    {
        Probe(frame, "f(f(f(1), (SUSPEND 2)), f(3));")
            .Should().Be("|pre,f(1),f(1),f(3),f(1),post");
    }

    /// <summary>
    /// Two suspensions in one argument list: the callee must survive the first resume and still be there
    /// for the second. On the unfixed tree the inner call runs three times.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void TheInnerCallRunsOnceAcrossTwoSuspensionsInOneArgumentList(Frame frame)
    {
        Probe(frame, "o.m(1).m((SUSPEND 'a'), (SUSPEND 'b'));")
            .Should().Be("|pre,m(1),m(a),post");
    }

    /// <summary>
    /// A callee that suspends itself still resumes through its own handler, and the argument list's
    /// suspension after it is resumed with the callee that resume produced.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ACalleeThatSuspendsAndThenAnArgumentThatSuspendsRunEachCallOnce(Frame frame)
    {
        Probe(frame, "(SUSPEND o).m(1).m((SUSPEND 2));")
            .Should().Be("|pre,m(1),m(2),post");
    }

    // ------------------------------------------------------------------ member reads on the callee

    /// <summary>
    /// The member-call lane: a plain identifier receiver with a literal name. The getter is the callee's
    /// member read, and it ran once more per resume.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void AGetterOnTheCalleeIsReadOnce(Frame frame)
    {
        Probe(frame, "var r = g.m((SUSPEND 5));", "reads + ':' + r")
            .Should().Be("1:true|pre,g(5),post");
    }

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void AComputedCalleeKeyAndItsGetterRunOnce(Frame frame)
    {
        Probe(frame, "var r = g[key()]((SUSPEND 5));", "reads + ':' + r")
            .Should().Be("1:true|pre,key,g(5),post");
    }

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void AGetterReachedThroughACallIsReadOnce(Frame frame)
    {
        Probe(frame, "var r = fac().g.m((SUSPEND 5));", "reads + ':' + r")
            .Should().Be("1:true|pre,fac,g(5),post");
    }

    // ------------------------------------------------------------------ this and the function value

    /// <summary>
    /// The receiver is evaluated before the arguments, so a reassignment inside them must not change the
    /// function called or its this value; the replay used to call the reassigned object's method instead.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void TheThisValueResolvedBeforeTheArgumentsIsTheOneCalled(Frame frame)
    {
        Probe(
                frame,
                """
                var t = { id: 1, m(x) { return this.id + ':' + x; } };
                var r = t.m((t = { id: 2, m() { return 'other'; } }, (SUSPEND 5)));
                """,
                "r")
            .Should().Be("1:5|pre,post");
    }

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void TheFunctionResolvedBeforeTheArgumentsIsTheOneCalled(Frame frame)
    {
        Probe(
                frame,
                """
                function a(x) { return 'a' + x; }
                function b(x) { return 'b' + x; }
                var fn = a;
                var r = fn((fn = b, (SUSPEND 5)));
                """,
                "r")
            .Should().Be("a5|pre,post");
    }

    /// <summary>
    /// A <c>super.m(…)</c> callee reads its member off the parent's prototype and calls it with the derived
    /// instance as <c>this</c>; a private method is read off the receiver. The getter on the parent ran
    /// once more per resume.
    /// </summary>
    [TestCase("*run", "yield")]
    [TestCase("async run", "await")]
    public void ASuperMemberCalleeIsReadOnceAndKeepsItsThisValue(string method, string keyword)
    {
        var script = $$"""
            var log = [];
            var reads = 0;
            class P { get m() { reads++; return function (x) { log.push('P.m(' + x + ')'); return this; }; } }
            class Q extends P {
              #p(x) { log.push('#p(' + x + ')'); return this; }
              {{method}}() {
                var a = super.m(({{keyword}} 5)) === this;
                var b = this.#p(({{keyword}} 6)) === this;
                return reads + ':' + a + ':' + b + '|' + log.join(',');
              }
            }
            var r = new Q().run();
            if (typeof r.next === 'function') {
              var res = r.next();
              for (var i = 0; !res.done && i < 8; i++) res = r.next(res.value);
              r = res.value;
            }
            r;
            """;

        var result = new Engine().Evaluate(script);
        result.UnwrapIfPromise().AsString().Should().Be("1:true:true|P.m(5),#p(6)");
    }

    /// <summary>
    /// The this value of an identifier callee found on a <c>with</c> object is that object.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void AWithObjectCalleeKeepsItsThisValueControl(Frame frame)
    {
        Probe(
                frame,
                """
                var w = { wm(x) { return (this === w) + ':' + x; } };
                var r;
                with (w) { r = wm((SUSPEND 5)); }
                """,
                "r")
            .Should().Be("true:5|pre,post");
    }

    // ------------------------------------------------------------------ optional calls

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void OptionalChainsRunTheirInnerCallsOnce(Frame frame)
    {
        Probe(frame, "o?.m(1).m((SUSPEND 'x')); o.m(2)?.m((SUSPEND 'y')); o.m(3).m?.((SUSPEND 'z'));")
            .Should().Be("|pre,m(1),m(x),m(2),m(y),m(3),m(z),post");
    }

    /// <summary>
    /// A short-circuited call evaluates none of its arguments, so it never reaches the suspension in them.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void AShortCircuitedCallEvaluatesNoArgumentControl(Frame frame)
    {
        Probe(frame, "var n = null; var r = n?.m(f(1), (SUSPEND 2)); var q = o.nope?.(f(3), (SUSPEND 4));", "r + ':' + q")
            .Should().Be("undefined:undefined|pre,post");
    }

    // ------------------------------------------------------------------ new

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ANewCalleeIsEvaluatedOnce(Frame frame)
    {
        Probe(frame, "new (fac().K)(1, (SUSPEND 'x')); new ok.K(2, (SUSPEND 'y'));", "reads")
            .Should().Be("1|pre,fac,K(1,x),K(2,y),post");
    }

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void TheConstructorResolvedBeforeTheArgumentsIsTheOneConstructed(Frame frame)
    {
        Probe(
                frame,
                """
                function A(x) { this.v = 'A' + x; }
                function B(x) { this.v = 'B' + x; }
                var C = A;
                var r = new C((C = B, (SUSPEND 5))).v;
                """,
                "r")
            .Should().Be("A5|pre,post");
    }

    // ------------------------------------------------------------------ spread arguments

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void SpreadArgumentsRunTheInnerCallOnce(Frame frame)
    {
        Probe(frame, "o.m(1).m(...arr(), (SUSPEND 'x')); o.m(2).m(...[(SUSPEND 'y')]); o.m(3).m(...(SUSPEND ['z']));")
            .Should().Be("|pre,m(1),arr,m(a),m(2),m(y),m(3),m(z),post");
    }

    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ANewWithSpreadArgumentsEvaluatesItsCalleeOnce(Frame frame)
    {
        Probe(frame, "new (fac().K)(...arr(), (SUSPEND 'x'));")
            .Should().Be("|pre,fac,arr,K(a,x),post");
    }

    // ------------------------------------------------------------------ eval and super

    /// <summary>
    /// A direct eval ran its code at the suspension, with the placeholder standing in for the suspended
    /// argument, and then again on the resume.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ADirectEvalWithASuspendingArgumentRunsOnce(Frame frame)
    {
        Probe(frame, "eval(\"log.push('e')\", (SUSPEND 1)); eval((SUSPEND \"log.push('y')\"));")
            .Should().Be("|pre,e,y,post");
    }

    /// <summary>
    /// <c>super(...)</c> is legal in an arrow function inside a derived constructor, and an async arrow can
    /// suspend in its arguments. It constructed the parent with the placeholder at the suspension, and the
    /// resume then called super a second time and threw <c>ReferenceError</c>.
    /// </summary>
    [Test]
    public void ASuperCallInAnAsyncArrowConstructsOnce()
    {
        new Engine().Evaluate("""
            var log = [];
            class P { constructor(...a) { log.push('P(' + a + ')'); } }
            var done;
            class Q extends P {
              constructor() {
                done = (async () => { super(log.push('arg'), await 'x'); log.push('this=' + (this instanceof Q)); })();
                return {};
              }
            }
            new Q();
            done.then(function () { return log.join(','); }, function (e) { return log.join(',') + ' threw ' + e.name; });
            """).UnwrapIfPromise().AsString()
            .Should().Be("arg,P(1,x),this=true");
    }

    /// <summary>
    /// The parent constructor is looked up before the arguments, so re-pointing the derived class's
    /// prototype inside them does not change which one is constructed.
    /// </summary>
    [Test]
    public void ASuperCallConstructsTheParentResolvedBeforeItsArguments()
    {
        new Engine().Evaluate("""
            var log = [];
            class P { constructor() { log.push('P'); } }
            class R { constructor() { log.push('R'); } }
            var done;
            class Q extends P {
              constructor() {
                done = (async () => { super((Object.setPrototypeOf(Q, R), await 1)); })();
                return {};
              }
            }
            new Q();
            done.then(function () { return log.join(','); }, function (e) { return log.join(',') + ' threw ' + e.name; });
            """).UnwrapIfPromise().AsString()
            .Should().Be("P");
    }

    // ------------------------------------------------------------------ abrupt resumptions

    [Test]
    public void AThrowResumptionInAnArgumentRunsTheInnerCallOnce()
    {
        new Engine().Evaluate("""
            var log = [];
            var o = { m(x) { log.push('m(' + x + ')'); return o; } };
            var it = (function* () { try { o.m(1).m(yield 0).m(3); } catch (e) { log.push('caught ' + e); } })();
            it.next();
            it.throw('x');
            log.join(',');
            """).AsString()
            .Should().Be("m(1),caught x");
    }

    [Test]
    public void AReturnResumptionInAnArgumentRunsTheInnerCallOnce()
    {
        new Engine().Evaluate("""
            var log = [];
            var o = { m(x) { log.push('m(' + x + ')'); return o; } };
            var it = (function* () { try { o.m(1).m(yield 0).m(3); } finally { log.push('finally'); } })();
            it.next();
            var r = it.return(7);
            log.join(',') + ' ' + JSON.stringify(r);
            """).AsString()
            .Should().Be("m(1),finally {\"value\":7,\"done\":true}");
    }

    [Test]
    public void ARejectionInAnArgumentRunsTheInnerCallOnce()
    {
        new Engine().Evaluate("""
            var log = [];
            var o = { m(x) { log.push('m(' + x + ')'); return o; } };
            (async function () {
              try { o.m(1).m(await Promise.reject('x')).m(3); } catch (e) { log.push('caught ' + e); }
              return log.join(',');
            })();
            """).UnwrapIfPromise().AsString()
            .Should().Be("m(1),caught x");
    }

    /// <summary>
    /// The site is evaluated again, in the same frame, after a throw left it through a suspended argument
    /// list: whatever it kept for the resume must be gone by then, or the next evaluation calls the last
    /// one's function.
    /// </summary>
    [Test]
    public void ACallLeftByAThrowResumptionStartsAfreshOnItsNextEvaluation()
    {
        new Engine().Evaluate("""
            var log = [];
            var o = { m(x) { log.push('m(' + x + ')'); return o; } };
            var it = (function* () {
              for (var i = 0; i < 3; i++) {
                try { o.m('a' + i).m(yield i); } catch (e) { log.push('c' + e); }
              }
            })();
            it.next();
            it.throw(1);
            it.next('v');
            it.next('w');
            log.join(',');
            """).AsString()
            .Should().Be("m(a0),c1,m(a1),m(v),m(a2),m(w)");
    }

    /// <summary>
    /// The async twin, with the callee suspending as well, so that the next evaluation of the site resumes
    /// in its callee while whatever a stale record held would still be lying around.
    /// </summary>
    [Test]
    public void ACallLeftByARejectionStartsAfreshOnItsNextEvaluation()
    {
        new Engine().Evaluate("""
            var log = [];
            var o = { m(x) { log.push('m(' + x + ')'); return o; } };
            (async function () {
              for (var i = 0; i < 3; i++) {
                try { (await o).m('a' + i).m(await (i == 1 ? Promise.reject(i) : i)); } catch (e) { log.push('c' + e); }
              }
              return log.join(',');
            })();
            """).UnwrapIfPromise().AsString()
            .Should().Be("m(a0),m(0),m(a1),c1,m(a2),m(2)");
    }

    // ------------------------------------------------------------------ the shape that was already right

    /// <summary>
    /// A tagged template already parks its tag function and this value; it is here as the precedent.
    /// </summary>
    [TestCase(Frame.Generator)]
    [TestCase(Frame.Async)]
    [TestCase(Frame.AsyncGeneratorAwait)]
    [TestCase(Frame.AsyncGeneratorYield)]
    public void ATaggedTemplateRunsItsTagExpressionOnceControl(Frame frame)
    {
        Probe(frame, "fac().o.m`${(SUSPEND 1)}`;")
            .Should().Be("|pre,fac,m(,),post");
    }

    /// <summary>
    /// A call whose arguments can suspend but whose evaluation does not suspend this time takes the same
    /// path as one that does, and produces the same answer as a call that cannot.
    /// </summary>
    [Test]
    public void ACallThatCouldSuspendButDoesNotIsUnaffectedControl()
    {
        new Engine().Evaluate("""
            var log = [];
            var o = { m(x) { log.push('m(' + x + ')'); return o; } };
            function* gen(skip) { if (skip) { o.m(1).m(2); } else { o.m(1).m(yield 0); } }
            var it = gen(true);
            it.next();
            function* gen2() { var r = 0; for (var i = 0; i < 3; i++) r += Math.max(i, i > 1 ? yield i : 0); return r; }
            var it2 = gen2(), res = it2.next();
            while (!res.done) res = it2.next(res.value);
            log.join(',') + '|' + res.value;
            """).AsString()
            .Should().Be("m(1),m(2)|3");
    }
}
