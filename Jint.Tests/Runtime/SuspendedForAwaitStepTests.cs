#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// When a <c>for await</c> step suspends an async generator, the statements around the loop must find that
/// suspension when the body replays (sebastienros/jint#4504).
/// <para>
/// Jint resumes a generator by replaying its body, and each statement that can be part-way through
/// (<c>for</c>, <c>while</c>, <c>if</c>, <c>switch</c>, <c>try</c>) asks <c>GetSuspensionNode</c> whether the
/// suspension lies inside it. A <c>for await</c> awaits every step
/// (https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset,
/// step 8.b), and in an async function that await records the loop as the suspension point. In an async
/// generator it recorded nothing, so every statement around the loop took the resume for a fresh entry. An
/// enclosing <c>for</c> ran its init again. ForLoopEvaluation runs the init once and then hands over to
/// ForBodyEvaluation (https://tc39.es/ecma262/#sec-forbodyevaluation), which never runs it again, and CreatePerIterationEnvironment
/// (https://tc39.es/ecma262/#sec-createperiterationenvironment) carries each round's values into the next. So
/// the loop went back to its first value on every step and replayed its last round forever. A
/// <c>while</c>, an <c>if</c> or a <c>switch</c> ran its test again, after the body had already changed what
/// that test reads: it left the iteration or took the other branch. A <c>try</c> whose <c>catch</c> or
/// <c>finally</c> held the loop ran its block again.
/// </para>
/// <para>
/// The matrix puts each loop shape around each kind of suspension, in an async generator and, as controls, in
/// an async function and a sync generator. Every expected log is the one Node 26 prints for the same shape run
/// without any suspension, and Node prints that log for every row. On the unfixed tree, only async-generator
/// rows whose suspension is a <c>for await</c> failed. They all did, except for the <c>do</c>-<c>while</c> and
/// <c>try</c>-block shapes, which run nothing again on the way back in. <c>mark</c> throws once
/// the log holds 64 entries, and <c>tick</c>, in a header that suspends, after 256 tests or updates: a runaway
/// row fails with <c>threw runaway</c> rather than hanging the run. On the unfixed tree a test that awaited
/// spun through its own await without ever reaching the body, which only <c>tick</c> stops.
/// </para>
/// </summary>
public class SuspendedForAwaitStepTests
{
    private const string AsyncGenerator = "async generator";
    private const string AsyncFunction = "async function";
    private const string Generator = "generator";

    private static readonly string[] AsyncFrames = [AsyncGenerator, AsyncFunction];
    private static readonly string[] GeneratorFrames = [AsyncGenerator, Generator];

    private const string Prelude = """
        var log = [];
        var ticks = 0;
        function mark(s) { if (log.length >= 64) throw new Error('runaway'); log.push(s); }
        function tick() { if (++ticks > 256) throw new Error('runaway tick'); }
        function asyncIter(n) {
          return {
            [Symbol.asyncIterator]() {
              var i = 0;
              return {
                next() { return Promise.resolve({ value: i, done: i++ >= n }); },
                return() { return Promise.resolve({ done: true }); }
              };
            }
          };
        }
        """;

    /// <summary>
    /// The loop shapes, with <c>SUSPEND</c> standing for the suspension, the log Node prints, and the frames a
    /// shape can run in when its header suspends by itself (<see langword="null"/> means all three).
    /// </summary>
    private static readonly (string Name, string Source, string Expected, string[]? Frames)[] Shapes =
    [
        ("letUpdate", "for (let r = 0; r < 3; r++) { mark('r' + r); SUSPEND; mark('e' + r); }", "r0,e0,r1,e1,r2,e2,end", null),
        ("varUpdate", "for (var r = 0; r < 3; r++) { mark('r' + r); SUSPEND; mark('e' + r); }", "r0,e0,r1,e1,r2,e2,end", null),
        ("expressionInit", "var r; for (r = 0; r < 3; r++) { mark('r' + r); SUSPEND; mark('e' + r); }", "r0,e0,r1,e1,r2,e2,end", null),
        ("letNoUpdate", "for (let r = 0; r < 3;) { mark('r' + r); SUSPEND; r++; }", "r0,r1,r2,end", null),
        ("varNoUpdate", "for (var r = 0; r < 3;) { mark('r' + r); SUSPEND; r++; }", "r0,r1,r2,end", null),
        ("letNoTest", "for (let r = 0; ; r++) { mark('r' + r); SUSPEND; if (r === 2) break; }", "r0,r1,r2,end", null),
        // A closure in the body turns off the reused iteration environment, so every round copies its bindings
        // into a fresh one (CreatePerIterationEnvironment).
        ("letClosures", "var fns = []; for (let r = 0; r < 3; r++) { fns.push(() => r); SUSPEND; mark('r' + r); } mark(fns.map(f => f()).join(''));", "r0,r1,r2,012,end", null),
        ("letContinue", "for (let r = 0; r < 4; r++) { mark('r' + r); SUSPEND; if (r % 2) continue; mark('e' + r); }", "r0,e0,r1,r2,e2,r3,end", null),
        ("varBreak", "for (var r = 0; r < 9; r++) { mark('r' + r); SUSPEND; if (r === 2) break; mark('e' + r); }", "r0,e0,r1,e1,r2,end", null),
        ("labelledContinue", "outer: for (let r = 0; r < 3; r++) { for (let k = 0; k < 2; k++) { mark(r + ':' + k); SUSPEND; if (k === 0) continue outer; } }", "0:0,1:0,2:0,end", null),
        ("labelledBreak", "outer: for (let r = 0; r < 9; r++) { for (var k = 0; k < 2; k++) { mark(r + ':' + k); SUSPEND; if (r === 1 && k === 1) break outer; } }", "0:0,0:1,1:0,1:1,end", null),
        ("nested", "for (let a = 0; a < 2; a++) for (var b = 0; b < 2; b++) { mark(a + '' + b); SUSPEND; }", "00,01,10,11,end", null),
        ("testAwaits", "for (let r = 0; (tick(), await null, r < 3); r++) { mark('r' + r); SUSPEND; }", "r0,r1,r2,end", AsyncFrames),
        ("updateAwaits", "for (let r = 0; r < 3; r = (tick(), await null, r + 1)) { mark('r' + r); SUSPEND; }", "r0,r1,r2,end", AsyncFrames),
        ("testYields", "for (let r = 0; (tick(), yield 't', r < 3); r++) { mark('r' + r); SUSPEND; }", "r0,r1,r2,end", GeneratorFrames),
        ("updateYields", "for (let r = 0; r < 3; r = (tick(), yield 'u', r + 1)) { mark('r' + r); SUSPEND; }", "r0,r1,r2,end", GeneratorFrames),
        // The other statements that ask where the suspension is, each with a test the body changes first.
        ("whileFalsified", "let go = true; while (go) { go = false; mark('w'); SUSPEND; mark('after'); }", "w,after,end", null),
        ("ifFalsified", "let flag = true; if (flag) { flag = false; SUSPEND; mark('then'); } else { mark('else'); }", "then,end", null),
        ("switchChanged", "let k = 1; switch (k) { case 1: k = 2; SUSPEND; mark('one'); break; case 2: mark('two'); }", "one,end", null),
        ("doWhile", "let n = 0; do { n++; mark('d' + n); SUSPEND; } while (n < 2);", "d1,d2,end", null),
        ("catchBlock", "try { mark('t'); throw new Error('x'); } catch (e) { SUSPEND; mark('c' + e.message); } finally { mark('f'); }", "t,cx,f,end", null),
        ("finallyBlock", "try { mark('t'); } finally { SUSPEND; mark('f'); }", "t,f,end", null),
        ("tryBlock", "let tries = 0; try { tries++; SUSPEND; mark('t' + tries); } catch (e) { mark('c'); }", "t1,end", null),
    ];

    /// <summary>
    /// The suspensions, and the frames each is valid in. The five <c>for await</c> forms are the issue's:
    /// over an array and over an async iterator, an inner loop that yields, one that a caught throw ends, and
    /// one that breaks.
    /// </summary>
    private static readonly (string Name, string Source, string[] Frames)[] Suspensions =
    [
        ("forAwaitArray", "for await (const x of [1]) { }", AsyncFrames),
        ("forAwaitIterator", "for await (const x of asyncIter(2)) { }", AsyncFrames),
        ("forAwaitYield", "for await (const x of [1, 2]) { yield x; }", [AsyncGenerator]),
        ("forAwaitCaughtThrow", "try { for await (const x of [1, 2]) { throw 'stop'; } } catch (e) { }", AsyncFrames),
        ("forAwaitBreak", "for await (const x of asyncIter(3)) { break; }", AsyncFrames),
        ("await", "await null", AsyncFrames),
        ("yield", "yield 1", GeneratorFrames),
        ("yieldStar", "yield* [1, 2]", GeneratorFrames),
    ];

    public static TheoryData<string, string, string, string> EveryShapeAroundEverySuspension
    {
        get
        {
            var rows = new TheoryData<string, string, string, string>();
            foreach (var shape in Shapes)
            {
                foreach (var suspension in Suspensions)
                {
                    foreach (var frame in suspension.Frames)
                    {
                        if (shape.Frames is null || Array.IndexOf(shape.Frames, frame) >= 0)
                        {
                            rows.Add(shape.Name, suspension.Name, frame, shape.Expected);
                        }
                    }
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> followed by <c>mark('end')</c> in a function of <paramref name="frame"/>'s kind,
    /// drives it to completion, and returns the log. A throw out of the body is logged as <c>threw …</c>.
    /// </summary>
    private static string Run(string frame, string body)
    {
        var driver = frame switch
        {
            AsyncGenerator => $$"""
                async function* subject() { {{body}} mark('end'); }
                (async () => {
                  try { for await (const _ of subject()) { } } catch (e) { log.push('threw ' + e.message); }
                  return log.join(',');
                })()
                """,
            AsyncFunction => $$"""
                async function subject() { {{body}} mark('end'); }
                subject().then(() => log.join(','), e => log.join(',') + ',threw ' + e.message)
                """,
            _ => $$"""
                function* subject() { {{body}} mark('end'); }
                (function () {
                  try { for (const _ of subject()) { } } catch (e) { log.push('threw ' + e.message); }
                  return log.join(',');
                })()
                """,
        };

        return new Engine().Evaluate(Prelude + "\n" + driver).UnwrapIfPromise().AsString();
    }

    private static string ShapeSource(string name)
    {
        foreach (var shape in Shapes)
        {
            if (string.Equals(shape.Name, name, StringComparison.Ordinal))
            {
                return shape.Source;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(name), name, "no such shape");
    }

    private static string SuspensionSource(string name)
    {
        foreach (var suspension in Suspensions)
        {
            if (string.Equals(suspension.Name, name, StringComparison.Ordinal))
            {
                return suspension.Source;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(name), name, "no such suspension");
    }

    [Theory]
    [MemberData(nameof(EveryShapeAroundEverySuspension))]
    public void TheStatementAroundTheSuspensionResumesWhereItWas(string shape, string suspension, string frame, string expected)
    {
        Run(frame, ShapeSource(shape).Replace("SUSPEND", SuspensionSource(suspension))).Should().Be(expected);
    }

    /// <summary>
    /// The issue's own program, with a guard on the round count added.
    /// </summary>
    [Fact]
    public void TheReportedLoopRunsTwoRoundsAndFinishes()
    {
        new Engine().Evaluate("""
            const log = [];
            let rounds = 0;
            async function* g() {
              for (let round = 0; round < 2; round++) {
                if (++rounds > 8) throw new Error('runaway');
                log.push('round ' + round);
                for await (const x of [1]) { }
              }
            }
            (async () => {
              try { for await (const _ of g()) { } } catch (e) { log.push(e.message); }
              return log.join(', ');
            })()
            """).UnwrapIfPromise().AsString().Should().Be("round 0, round 1");
    }

    /// <summary>
    /// The consumer receives each value the inner loop yields exactly once, in order, and the generator then
    /// finishes.
    /// </summary>
    [Fact]
    public void TheValuesAnInnerForAwaitYieldsReachTheConsumerOnceEach()
    {
        new Engine().Evaluate("""
            async function* g() {
              for (let r = 0; r < 3; r++) {
                for await (const x of [r * 10, r * 10 + 1]) { yield x; }
              }
              yield 'end';
            }
            (async () => {
              const seen = [];
              for await (const v of g()) { seen.push(v); if (seen.length > 16) { seen.push('runaway'); break; } }
              return seen.join(',');
            })()
            """).UnwrapIfPromise().AsString().Should().Be("0,1,10,11,20,21,end");
    }

    /// <summary>
    /// The suspension point a step recorded is cleared once the step resumes. A <c>yield*</c> records its
    /// suspension point somewhere else, behind that one, so a stale step would stand in for the delegation:
    /// the <c>while</c> would not see that the delegation is inside it, would run its test again, and would
    /// leave before <c>after</c>.
    /// </summary>
    [Fact]
    public void AStepThatResumedDoesNotHideALaterDelegation()
    {
        Run(AsyncGenerator, """
            for await (const x of asyncIter(1)) { mark('x' + x); }
            let go = true;
            while (go) { go = false; mark('w'); yield* [1, 2]; mark('after'); }
            """).Should().Be("x0,w,after,end");
    }

    /// <summary>
    /// The same for a step whose promise rejects: the throw out of the loop clears the suspension point too.
    /// </summary>
    [Fact]
    public void AStepThatRejectedDoesNotHideALaterDelegation()
    {
        Run(AsyncGenerator, """
            const rejecting = { [Symbol.asyncIterator]() { return { next() { return Promise.reject(new Error('step')); } }; } };
            try { for await (const x of rejecting) { } } catch (e) { mark(e.message); }
            let go = true;
            while (go) { go = false; mark('w'); yield* [1, 2]; mark('after'); }
            """).Should().Be("step,w,after,end");
    }
}
