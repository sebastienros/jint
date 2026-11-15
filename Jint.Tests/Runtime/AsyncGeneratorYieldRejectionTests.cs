#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// An async generator's <c>yield</c> of a promise that rejects. <see href="https://tc39.es/ecma262/#sec-yield">Yield</see>
/// step 2 is <c>? AsyncGeneratorYield(? Await(value))</c>: the operand is awaited before anything is yielded, so a
/// rejection is a throw completion of the <c>yield</c> expression, in the generator body, exactly as an <c>await</c>
/// of the same promise would be. A <c>catch</c> or <c>finally</c> around the <c>yield</c> sees it and the generator
/// carries on; only a throw nothing in the body catches completes the generator and rejects the request.
/// <para>
/// Jint completed the generator and rejected the pending request instead, so no <c>catch</c>, <c>finally</c>, iterator
/// close or resource disposal around the <c>yield</c> ever ran (sebastienros/jint#4369).
/// </para>
/// <para>
/// Each request is logged as it settles, and every expected log is Node 26's. Every row of
/// <see cref="AYieldThrowsWhereAnAwaitWould"/> runs twice, with the operand yielded and with it awaited, against one
/// expected log, which is what Node answers for both.
/// </para>
/// </summary>
public class AsyncGeneratorYieldRejectionTests
{
    private const string Prelude = """
        function fmt(r) {
          return '{' + (r.value instanceof Promise ? 'a promise' : String(r.value)) + ',' + r.done + '}';
        }
        function logged(log, request) {
          return request.then(r => { log.push('resolved ' + fmt(r)); }, e => { log.push('rejected ' + e); });
        }
        function closable(log) {
          return { [Symbol.iterator]() { return { next() { return { value: 'v', done: false }; }, return() { log.push('iter return'); return {}; } }; } };
        }
        // Makes each request once the one before it has settled.
        async function inTurn(makeGen, requests) {
          const log = [];
          const it = makeGen(log);
          for (const request of requests) {
            await logged(log, request(it));
          }
          return log.join(', ');
        }
        // Makes every request at once, so the later ones queue behind the first.
        async function atOnce(makeGen, requests) {
          const log = [];
          const it = makeGen(log);
          await Promise.all(requests.map(request => logged(log, request(it))));
          return log.join(', ');
        }
        // Interleaves the first next() with a ladder of microtasks t1..tn started just before it.
        async function timed(makeGen, n) {
          const log = [];
          const it = makeGen(log);
          let p = Promise.resolve();
          for (let i = 1; i <= n; i++) {
            const k = i;
            p = p.then(() => { log.push('t' + k); });
          }
          logged(log, it.next());
          log.push('called');
          await p;
          return log.join(', ');
        }

        """;

    private static string Run(string body, string drive)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        var script = Prelude + "const g = log => (async function* () { " + body + " })();\n" + drive + ";";
        return engine.Evaluate(script).UnwrapIfPromise().AsString();
    }

    private const string TwoNexts = "inTurn(g, [it => it.next(), it => it.next()])";
    private const string ThreeNexts = "inTurn(g, [it => it.next(), it => it.next(), it => it.next()])";
    private const string FourNexts = "inTurn(g, [it => it.next(), it => it.next(), it => it.next(), it => it.next()])";

    // name, body with KW where the rejected operand is yielded or awaited, how the generator is driven, expected log
    private static readonly (string Name, string Body, string Drive, string Expected)[] Scenarios =
    [
        (
            // The issue's own example.
            "TryCatchFinally",
            "try { log.push('got ' + (KW Promise.reject('X'))); } catch (e) { log.push('caught ' + e); } finally { log.push('finally'); } yield 'after';",
            TwoNexts,
            "caught X, finally, resolved {after,false}, resolved {undefined,true}"
        ),
        (
            "TryFinally",
            "try { KW Promise.reject('X'); log.push('UNREACHED'); } finally { log.push('finally'); } yield 'UNREACHED';",
            TwoNexts,
            "finally, rejected X, resolved {undefined,true}"
        ),
        (
            // The finally's own yield answers the request; the throw completion resumes once the finally ends.
            "TryFinallyThatYields",
            "try { KW Promise.reject('X'); log.push('UNREACHED'); } finally { log.push('finally'); yield 'from finally'; log.push('finally resumed'); } yield 'UNREACHED';",
            ThreeNexts,
            "finally, resolved {from finally,false}, finally resumed, rejected X, resolved {undefined,true}"
        ),
        (
            "NestedTry",
            "try { try { KW Promise.reject('X'); } finally { log.push('inner finally'); } log.push('UNREACHED'); } catch (e) { log.push('outer caught ' + e); } finally { log.push('outer finally'); } yield 'after';",
            TwoNexts,
            "inner finally, outer caught X, outer finally, resolved {after,false}, resolved {undefined,true}"
        ),
        (
            "CatchThatRethrows",
            "try { try { KW Promise.reject('X'); } catch (e) { log.push('inner caught ' + e); throw 'Y'; } } catch (e) { log.push('outer caught ' + e); } yield 'after';",
            TwoNexts,
            "inner caught X, outer caught Y, resolved {after,false}, resolved {undefined,true}"
        ),
        (
            "CatchThatAwaits",
            "try { KW Promise.reject('X'); } catch (e) { log.push(await Promise.resolve('awaited ' + e)); } yield 'after';",
            TwoNexts,
            "awaited X, resolved {after,false}, resolved {undefined,true}"
        ),
        (
            // Await's PromiseResolve wraps a thenable, whose then runs in a job of its own.
            "RejectingThenable",
            "try { KW ({ then(resolve, reject) { log.push('then'); reject('T'); } }); } catch (e) { log.push('caught ' + e); } yield 'after';",
            TwoNexts,
            "then, caught T, resolved {after,false}, resolved {undefined,true}"
        ),
        (
            "ThenableThatRejectsLater",
            "try { KW ({ then(resolve, reject) { log.push('then'); Promise.resolve().then(() => reject('T')); } }); } catch (e) { log.push('caught ' + e); } yield 'after';",
            TwoNexts,
            "then, caught T, resolved {after,false}, resolved {undefined,true}"
        ),
        (
            "ThenableWhoseThenThrows",
            "try { KW ({ then() { throw 'T'; } }); } catch (e) { log.push('caught ' + e); } yield 'after';",
            TwoNexts,
            "caught T, resolved {after,false}, resolved {undefined,true}"
        ),
        (
            // A caught rejection leaves the generator running: the loop goes on, and its next yield answers the request.
            "RejectionsBetweenFurtherYieldsInALoop",
            "for (let i = 0; i < 4; i++) { try { log.push('got ' + (i % 2 ? KW Promise.reject('R' + i) : yield i)); } catch (e) { log.push('caught ' + e); } } return 'end';",
            "inTurn(g, [it => it.next('a'), it => it.next('b'), it => it.next('c'), it => it.next('d')])",
            "resolved {0,false}, got b, caught R1, resolved {2,false}, got c, caught R3, resolved {end,true}, resolved {undefined,true}"
        ),
        (
            "ReturnAfterTheCaughtRejection",
            "try { KW Promise.reject('X'); } catch (e) { log.push('caught ' + e); } try { yield 'after'; log.push('UNREACHED'); } finally { log.push('finally'); } log.push('UNREACHED');",
            "inTurn(g, [it => it.next(), it => it.return('R'), it => it.next()])",
            "caught X, resolved {after,false}, finally, resolved {R,true}, resolved {undefined,true}"
        ),
        (
            // Made while the operand is still being awaited, the return() queues behind the next() the rejection
            // belongs to, and reaches the generator only at its next yield.
            "ReturnQueuedBehindTheRejection",
            "try { KW Promise.reject('X'); } catch (e) { log.push('caught ' + e); } try { yield 'after'; log.push('UNREACHED'); } finally { log.push('finally'); } log.push('UNREACHED');",
            "atOnce(g, [it => it.next(), it => it.return('R'), it => it.next()])",
            "caught X, resolved {after,false}, finally, resolved {R,true}, resolved {undefined,true}"
        ),
        (
            "ThrowQueuedBehindTheRejection",
            "try { KW Promise.reject('X'); } catch (e) { log.push('caught ' + e); } try { yield 'after'; log.push('UNREACHED'); } finally { log.push('finally'); } log.push('UNREACHED');",
            "atOnce(g, [it => it.next(), it => it.throw('Z'), it => it.next()])",
            "caught X, finally, resolved {after,false}, rejected Z, resolved {undefined,true}"
        ),
        (
            "NextQueuedBehindTheRejection",
            "try { KW Promise.reject('X'); } catch (e) { log.push('caught ' + e); } log.push('got ' + (yield 'after'));",
            "atOnce(g, [it => it.next(), it => it.next('n2'), it => it.next()])",
            "caught X, got n2, resolved {after,false}, resolved {undefined,true}, resolved {undefined,true}"
        ),
        (
            // The rejection reaches a catch inside a finally that a return() is running, and the return goes on.
            "CaughtInAFinallyARequestedReturnRuns",
            "try { yield 1; } finally { try { KW Promise.reject('F'); } catch (e) { log.push('caught ' + e); } log.push('finally done'); } log.push('UNREACHED');",
            "inTurn(g, [it => it.next(), it => it.return('R'), it => it.next()])",
            "resolved {1,false}, caught F, finally done, resolved {R,true}, resolved {undefined,true}"
        ),
        (
            "UncaughtInAFinallyARequestedReturnRuns",
            "try { yield 1; } finally { KW Promise.reject('F'); log.push('UNREACHED'); } log.push('UNREACHED');",
            "inTurn(g, [it => it.next(), it => it.return('R'), it => it.next()])",
            "resolved {1,false}, rejected F, resolved {undefined,true}"
        ),
        (
            // What test262's yield-promise-reject-next*.js pin: with nothing to catch it, the throw completes the
            // generator and rejects the request.
            "Uncaught",
            "KW Promise.reject('X'); yield 'UNREACHED';",
            TwoNexts,
            "rejected X, resolved {undefined,true}"
        ),
        (
            // The generator completes with two more requests queued, and answers both.
            "UncaughtWithRequestsQueuedBehindIt",
            "KW Promise.reject('X'); yield 'UNREACHED';",
            "atOnce(g, [it => it.next(), it => it.next(), it => it.next()])",
            "rejected X, resolved {undefined,true}, resolved {undefined,true}"
        ),
        (
            "UncaughtClosesAnIteratorTheBodyIsLoopingOver",
            "for (const v of closable(log)) { KW Promise.reject('X'); log.push('UNREACHED'); }",
            TwoNexts,
            "iter return, rejected X, resolved {undefined,true}"
        ),
        (
            "UncaughtDisposesTheBlocksResources",
            "{ await using r = { [Symbol.asyncDispose]() { log.push('dispose'); } }; KW Promise.reject('X'); log.push('UNREACHED'); }",
            TwoNexts,
            "dispose, rejected X, resolved {undefined,true}"
        ),
        (
            "UncaughtDisposesTheBodysResources",
            "using r = { [Symbol.dispose]() { log.push('dispose'); } }; KW Promise.reject('X'); log.push('UNREACHED');",
            TwoNexts,
            "dispose, rejected X, resolved {undefined,true}"
        ),
    ];

    public static TestCases<string, string, string, string> YieldedAndAwaited
    {
        get
        {
            var cases = new TestCases<string, string, string, string>();
            foreach (var (name, body, drive, expected) in Scenarios)
            {
                cases.Add(name + " (yield)", body.Replace("KW", "yield"), drive, expected);
                cases.Add(name + " (await)", body.Replace("KW", "await"), drive, expected);
            }

            return cases;
        }
    }

    [TestCaseSource(nameof(YieldedAndAwaited)), CancelAfter(10000)]
    public void AYieldThrowsWhereAnAwaitWould(string name, string body, string drive, string expected)
    {
        Run(body, drive).Should().Be(expected, name);
    }

    /// <summary>
    /// A <c>yield*</c> awaits each answer of its delegate (step 7.a.vi of
    /// https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation), so a rejected one is a
    /// throw at the <c>yield*</c>; and a delegate that is itself an async generator throws its own rejected
    /// <c>yield</c> at that <c>yield</c>, inside the delegate.
    /// </summary>
    public static TestCases<string, string, string, string> Delegations => new()
    {
        {
            "ANextThatRejects",
            "const inner = { [Symbol.asyncIterator]() { return this; }, next() { log.push('inner next'); return Promise.reject('N'); }, return() { log.push('UNREACHED return'); return {}; } }; try { yield* inner; } catch (e) { log.push('caught ' + e); } yield 'after';",
            ThreeNexts,
            "inner next, caught N, resolved {after,false}, resolved {undefined,true}, resolved {undefined,true}"
        },
        {
            "ANextThatRejectsAfterAnAnswer",
            "let n = 0; const inner = { [Symbol.asyncIterator]() { return this; }, next() { n++; log.push('inner next ' + n); return n === 1 ? Promise.resolve({ value: 'i1', done: false }) : Promise.reject('N'); } }; try { yield* inner; } catch (e) { log.push('caught ' + e); } yield 'after';",
            FourNexts,
            "inner next 1, resolved {i1,false}, inner next 2, caught N, resolved {after,false}, resolved {undefined,true}, resolved {undefined,true}"
        },
        {
            "ASyncIterableOfARejectedPromise",
            "try { yield* [Promise.reject('S')]; } catch (e) { log.push('caught ' + e); } yield 'after';",
            ThreeNexts,
            "caught S, resolved {after,false}, resolved {undefined,true}, resolved {undefined,true}"
        },
        {
            "AnAsyncGeneratorThatCatchesItsOwnRejectedYield",
            "const inner = (async function* () { try { yield Promise.reject('I'); } catch (e) { log.push('inner caught ' + e); } yield 'inner after'; return 'inner end'; })(); log.push('got ' + (yield* inner)); yield 'after';",
            FourNexts,
            "inner caught I, resolved {inner after,false}, got inner end, resolved {after,false}, resolved {undefined,true}, resolved {undefined,true}"
        },
        {
            "AnAsyncGeneratorWhoseRejectedYieldOnlyAFinallySees",
            "const inner = (async function* () { try { yield Promise.reject('I'); } finally { log.push('inner finally'); } })(); try { yield* inner; } catch (e) { log.push('outer caught ' + e); } yield 'after';",
            ThreeNexts,
            "inner finally, outer caught I, resolved {after,false}, resolved {undefined,true}, resolved {undefined,true}"
        },
    };

    [TestCaseSource(nameof(Delegations)), CancelAfter(10000)]
    public void AYieldStarThrowsADelegatesRejection(string name, string body, string drive, string expected)
    {
        Run(body, drive).Should().Be(expected, name);
    }

    /// <summary>
    /// Requests made while the generator is busy queue, and once it completes,
    /// https://tc39.es/ecma262/#sec-asyncgeneratordrainqueue answers every one of them in turn: a <c>next()</c> with
    /// <c>{ undefined, done }</c>, a <c>throw()</c> with its rejection, and a <c>return()</c> once its value is awaited.
    /// Jint answered the first and left the rest pending for good.
    /// </summary>
    public static TestCases<string, string, string, string> QueuedOnACompletingGenerator => new()
    {
        {
            "AfterAnAwait",
            "await null;",
            "atOnce(g, [it => it.next(), it => it.throw('a'), it => it.next(), it => it.return('r'), it => it.next(), it => it.throw('b')])",
            "resolved {undefined,true}, rejected a, resolved {undefined,true}, resolved {r,true}, resolved {undefined,true}, rejected b"
        },
        {
            "AfterAYield",
            "yield 1;",
            "atOnce(g, [it => it.next(), it => it.next(), it => it.throw('a'), it => it.next()])",
            "resolved {1,false}, resolved {undefined,true}, rejected a, resolved {undefined,true}"
        },
    };

    [TestCaseSource(nameof(QueuedOnACompletingGenerator)), CancelAfter(10000)]
    public void ACompletedGeneratorAnswersEveryQueuedRequest(string name, string body, string drive, string expected)
    {
        Run(body, drive).Should().Be(expected, name);
    }

    /// <summary>
    /// The throw resumes the body in the reaction job of the operand's await, the microtask an <c>await</c> of it
    /// resumes in, and the request is answered by the generator's next <c>yield</c>.
    /// </summary>
    public static TestCases<string, string, string> Timing => new()
    {
        {
            "Rejected",
            "try { yield Promise.reject('X'); } catch (e) { log.push('caught ' + e); } yield 'after';",
            "called, t1, caught X, t2, t3, resolved {after,false}, t4, t5, t6, t7"
        },
        {
            "RejectedAndAwaited",
            "try { await Promise.reject('X'); } catch (e) { log.push('caught ' + e); } yield 'after';",
            "called, t1, caught X, t2, t3, resolved {after,false}, t4, t5, t6, t7"
        },
        {
            // The control: a fulfilled operand answers the request with its value.
            "Fulfilled",
            "try { log.push('got ' + (yield Promise.resolve('V'))); } catch (e) { log.push('caught ' + e); } yield 'after';",
            "called, t1, t2, resolved {V,false}, t3, t4, t5, t6, t7"
        },
        {
            "RejectingThenable",
            "try { yield { then(resolve, reject) { reject('T'); } }; } catch (e) { log.push('caught ' + e); } yield 'after';",
            "called, t1, t2, caught T, t3, t4, resolved {after,false}, t5, t6, t7"
        },
        {
            "RejectedLater",
            "const p = Promise.resolve().then(() => { throw 'X'; }); try { yield p; } catch (e) { log.push('caught ' + e); } yield 'after';",
            "called, t1, t2, caught X, t3, t4, resolved {after,false}, t5, t6, t7"
        },
    };

    [TestCaseSource(nameof(Timing)), CancelAfter(10000)]
    public void TheThrowResumesTheBodyOnTheMicrotaskAnAwaitWould(string name, string body, string expected)
    {
        Run(body, "timed(g, 7)").Should().Be(expected, name);
    }
}
