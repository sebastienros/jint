#nullable enable

using AwesomeAssertions.Execution;

namespace Jint.Tests.Runtime;

/// <summary>
/// A block's <c>await using</c> declarations in an async function and in an async generator, timed against the
/// microtask queue. A block disposes its resources as it ends
/// (<see href="https://tc39.es/ecma262/#sec-block-runtime-semantics-evaluation">Block evaluation</see>, step 6), and
/// <see href="https://tc39.es/ecma262/#sec-disposeresources">DisposeResources</see> awaits each async-dispose result
/// (step 4.e.ii.1, <c>Await(result.[[Value]])</c>) and once more for a <c>null</c> resource (steps 4.d and 5), which
/// suspends the function or the generator exactly as an <c>await</c> expression does. A dispose that throws or rejects
/// replaces the block's completion, or is aggregated over a throw completion in a <c>SuppressedError</c> (step 4.e.iii).
/// <para>
/// In an async generator Jint never suspended there: it waited for each result on the spot, running the caller's job
/// queue inside <c>next()</c>, so the rest of the body ran before the caller's next statement and an unrelated job
/// the caller had queued ran in the middle of the request (sebastienros/jint#4355). An async function was not
/// affected, and each row runs in both kinds side by side.
/// </para>
/// <para>
/// Each log is the body's own entries interleaved with a ladder of microtasks <c>t1</c>, <c>t2</c>, … that is
/// started just before the request; <c>called</c> is the statement after it. Every expected log is Node 24's.
/// </para>
/// </summary>
public class AsyncBlockDisposalTests
{
    // ForStatementDisposalTests runs its rows against the same helpers.
    internal const string Prelude = """
        function ladder(log, n) {
          let p = Promise.resolve();
          for (let i = 1; i <= n; i++) {
            const k = i;
            p = p.then(() => { log.push('t' + k); });
          }
          return p;
        }
        function show(v) {
          return v instanceof SuppressedError ? 'SuppressedError(' + show(v.error) + ' over ' + show(v.suppressed) + ')' : String(v);
        }
        function fmt(r) {
          return r !== null && typeof r === 'object' && 'done' in r ? '{' + show(r.value) + ',' + r.done + '}' : show(r);
        }
        // An async-disposable resource whose dispose method returns `result`, a sync-disposable one, one whose dispose
        // returns a promise that settles a microtask later, and two whose dispose rejects or throws.
        function ar(log, name, result) {
          return { [Symbol.asyncDispose]() { log.push('dispose ' + name); return result; } };
        }
        function sr(log, name) {
          return { [Symbol.dispose]() { log.push('dispose ' + name); } };
        }
        function later(log, name) {
          return { [Symbol.asyncDispose]() { log.push('dispose ' + name); return Promise.resolve().then(() => log.push('disposed ' + name)); } };
        }
        function rejecting(log, name, reason) {
          return { [Symbol.asyncDispose]() { log.push('dispose ' + name); return Promise.reject(reason); } };
        }
        function throwing(log, name, reason) {
          return { [Symbol.asyncDispose]() { log.push('dispose ' + name); throw reason; } };
        }
        // make builds the function or the generator, started says whether a generator is first run to its first
        // yield, and request makes the request whose answer is logged. The ladder is started just before it.
        async function probe(make, request, started) {
          const log = [];
          const it = make(log);
          if (started) {
            await it.next();
          }
          const done = ladder(log, 9);
          request(it, log).then(r => log.push('resolved ' + fmt(r)), e => log.push('rejected ' + show(e)));
          log.push('called');
          return done.then(() => log.join(', '));
        }

        """;

    /// <summary>
    /// A body run as an async function and as an async generator, each from the start: its log as a function, then as
    /// a generator. The two differ only in the answer, a value or an iterator result, bar <c>AReturnInsideTheBlock</c>.
    /// </summary>
    public static TestCases<string, string, string, string> InBothKinds => new()
    {
        {
            // The issue's own example.
            "TheIssuesExample",
            "{ await using r = ar(log, 'r'); } log.push('after');",
            "dispose r, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose r, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The issue's worse variant: t1 is a job the caller queued before the request, and it must not run inside
            // the request.
            "ADisposeReturningAPromise",
            "{ await using r = later(log, 'r'); } log.push('after');",
            "dispose r, called, t1, disposed r, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose r, called, t1, disposed r, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AMultiStatementBlock",
            "{ await using r = ar(log, 'r'); log.push('body'); } log.push('after');",
            "body, dispose r, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose r, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // Last declared, first disposed, and every result is awaited before the next dispose method runs.
            "ThreeResourcesInReverseOrder",
            "{ await using a = ar(log, 'a'), b = later(log, 'b'); await using c = ar(log, 'c'); log.push('body'); } log.push('after');",
            "body, dispose c, called, t1, dispose b, t2, disposed b, t3, dispose a, t4, after, t5, resolved undefined, t6, t7, t8, t9",
            "body, dispose c, called, t1, dispose b, t2, disposed b, t3, dispose a, t4, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            "AnInnerBlocksDisposeThenAnAwait",
            "{ await using a = ar(log, 'a'); { await using b = later(log, 'b'); log.push('inner'); } log.push('between'); await null; log.push('after await'); } log.push('after');",
            "inner, dispose b, called, t1, disposed b, t2, between, t3, after await, dispose a, t4, after, t5, resolved undefined, t6, t7, t8, t9",
            "inner, dispose b, called, t1, disposed b, t2, between, t3, after await, dispose a, t4, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            "NestedBlocks",
            "{ await using a = ar(log, 'a'); { await using b = ar(log, 'b'); } log.push('between'); } log.push('after');",
            "dispose b, called, t1, between, dispose a, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose b, called, t1, between, dispose a, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AForLoopBody",
            "for (let i = 0; i < 2; i++) { await using r = ar(log, 'r' + i); log.push('body ' + i); } log.push('after');",
            "body 0, dispose r0, called, t1, body 1, dispose r1, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body 0, dispose r0, called, t1, body 1, dispose r1, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // The continue completion waits out the dispose.
            "AForLoopBodyThatContinues",
            "for (let i = 0; i < 2; i++) { await using r = ar(log, 'r' + i); if (i === 0) continue; log.push('body ' + i); } log.push('after');",
            "dispose r0, called, t1, body 1, dispose r1, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose r0, called, t1, body 1, dispose r1, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Resuming at the dispose does not run the loop's test again.
            "AWhileLoopBodyDoesNotRunTheTestAgain",
            "let i = 0; while (log.push('test ' + i), i < 2) { await using r = ar(log, 'r' + i); i++; } log.push('after');",
            "test 0, dispose r0, called, t1, test 1, dispose r1, t2, test 2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "test 0, dispose r0, called, t1, test 1, dispose r1, t2, test 2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ADoWhileLoopBody",
            "let i = 0; do { await using r = ar(log, 'r' + i); i++; } while (log.push('test ' + i), i < 2); log.push('after');",
            "dispose r0, called, t1, test 1, dispose r1, t2, test 2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose r0, called, t1, test 1, dispose r1, t2, test 2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AForOfLoopBody",
            "for (const x of [1, 2]) { await using r = ar(log, 'r' + x); } log.push('after');",
            "dispose r1, called, t1, dispose r2, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose r1, called, t1, dispose r2, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AForAwaitOfLoopBody",
            "for await (const x of [1]) { await using r = ar(log, 'r' + x); log.push('body ' + x); } log.push('after');",
            "called, t1, t2, body 1, dispose r1, t3, t4, t5, after, t6, resolved undefined, t7, t8, t9",
            "called, t1, t2, body 1, dispose r1, t3, t4, t5, after, t6, resolved {undefined,true}, t7, t8, t9"
        },
        {
            // The update's await belongs to the next iteration, and a resumption at the dispose does not answer it.
            "AnAwaitInTheLoopsUpdate",
            "for (let i = 0; i < 3; i = await Promise.resolve(i + 1)) { await using r = ar(log, 'r' + i); log.push('body ' + i); } log.push('after');",
            "body 0, dispose r0, called, t1, t2, body 1, dispose r1, t3, t4, body 2, dispose r2, t5, t6, after, t7, resolved undefined, t8, t9",
            "body 0, dispose r0, called, t1, t2, body 1, dispose r1, t3, t4, body 2, dispose r2, t5, t6, after, t7, resolved {undefined,true}, t8, t9"
        },
        {
            "AnAwaitInTheLoopsTest",
            "let i = 0; while (await Promise.resolve(i < 2)) { await using r = ar(log, 'r' + i); log.push('body ' + i); i++; } log.push('after');",
            "called, t1, body 0, dispose r0, t2, t3, body 1, dispose r1, t4, t5, after, t6, resolved undefined, t7, t8, t9",
            "called, t1, body 0, dispose r0, t2, t3, body 1, dispose r1, t4, t5, after, t6, resolved {undefined,true}, t7, t8, t9"
        },
        {
            "AnAwaitInTheBodyOfALoop",
            "for (let i = 0; i < 2; i++) { await using r = ar(log, 'r' + i); log.push('got ' + await Promise.resolve(i)); } log.push('after');",
            "called, t1, got 0, dispose r0, t2, t3, got 1, dispose r1, t4, after, t5, resolved undefined, t6, t7, t8, t9",
            "called, t1, got 0, dispose r0, t2, t3, got 1, dispose r1, t4, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            "ABreakOutOfAWhileLoop",
            "while (true) { await using r = ar(log, 'r'); break; } log.push('after');",
            "dispose r, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose r, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ALabelledBreakOutOfTheBlock",
            "outer: { await using r = ar(log, 'r'); log.push('body'); break outer; } log.push('after');",
            "body, dispose r, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose r, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "AnIfConsequentDoesNotRunTheTestAgain",
            "if (log.push('test'), true) { await using r = ar(log, 'r'); } else { log.push('UNREACHED'); } log.push('after');",
            "test, dispose r, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "test, dispose r, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ASwitchCaseBlock",
            "switch (1) { case 1: { await using r = ar(log, 'r'); } log.push('case'); } log.push('after');",
            "dispose r, called, t1, case, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose r, called, t1, case, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "InsideATryWithAFinally",
            "try { { await using r = ar(log, 'r'); } } finally { log.push('finally'); } log.push('after');",
            "dispose r, called, t1, finally, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose r, called, t1, finally, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "InsideACatchBlock",
            "try { throw 'E'; } catch (e) { await using r = ar(log, 'r'); log.push('caught ' + e); } log.push('after');",
            "caught E, dispose r, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "caught E, dispose r, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "InsideAFinallyBlock",
            "try { log.push('try'); } finally { { await using r = ar(log, 'r'); } log.push('finally'); } log.push('after');",
            "try, dispose r, called, t1, finally, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "try, dispose r, called, t1, finally, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // Resuming at the dispose does not run the body, or its await, again.
            "TheBodysAwaitsAreNotRunAgain",
            "{ await using r = ar(log, 'r'); log.push('before await'); await null; log.push('after await'); } log.push('after');",
            "before await, called, t1, after await, dispose r, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "before await, called, t1, after await, dispose r, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AnAsyncDisposeMethod",
            "{ await using r = { async [Symbol.asyncDispose]() { log.push('dispose start'); await null; log.push('dispose end'); } }; } log.push('after');",
            "dispose start, called, t1, dispose end, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose start, called, t1, dispose end, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // A generator's `return 5;` awaits its operand first (sebastienros/jint#4332), an async function's does not.
            "AReturnInsideTheBlock",
            "{ await using r = ar(log, 'r'); return 5; }",
            "dispose r, called, t1, t2, resolved 5, t3, t4, t5, t6, t7, t8, t9",
            "called, t1, dispose r, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // A dispose method that throws has no result to await (step 4.e.ii applies to a normal completion only).
            "ADisposeThatThrows",
            "try { { await using r = throwing(log, 'r', 'D'); log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose r, caught D, after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose r, caught D, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ADisposeThatRejects",
            "try { { await using r = rejecting(log, 'r', 'D'); log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose r, called, t1, caught D, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose r, called, t1, caught D, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ADisposeThatRejectsUncaught",
            "{ await using r = rejecting(log, 'r', 'D'); } log.push('UNREACHED');",
            "dispose r, called, t1, t2, rejected D, t3, t4, t5, t6, t7, t8, t9",
            "dispose r, called, t1, t2, rejected D, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "TheBodyThrowsAndTheDisposeRejects",
            "try { { await using r = rejecting(log, 'r', 'D'); throw 'B'; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose r, called, t1, caught SuppressedError(D over B), after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose r, called, t1, caught SuppressedError(D over B), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "TwoDisposesReject",
            "try { { await using a = rejecting(log, 'a', 'A'), b = rejecting(log, 'b', 'B'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose b, called, t1, dispose a, t2, caught SuppressedError(A over B), after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose b, called, t1, dispose a, t2, caught SuppressedError(A over B), after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ADisposeRejectsAfterAnotherThrows",
            "try { { await using a = rejecting(log, 'a', 'A'), b = throwing(log, 'b', 'B'); log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose b, dispose a, called, t1, caught SuppressedError(A over B), after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose b, dispose a, called, t1, caught SuppressedError(A over B), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // A sync dispose after an awaited one runs a microtask later, and is not awaited itself.
            "SyncAndAsyncResourcesMixed",
            "{ using s1 = sr(log, 's1'); await using a = ar(log, 'a'); using s2 = sr(log, 's2'); log.push('body'); } log.push('after');",
            "body, dispose s2, dispose a, called, t1, dispose s1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose s2, dispose a, called, t1, dispose s1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 4.d: a null async resource owes an await before the next sync dispose.
            "ANullAsyncResourceBeforeASyncOne",
            "{ using s = sr(log, 's'); await using n = null; } log.push('after');",
            "called, t1, dispose s, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "called, t1, dispose s, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 5: ... or at the end.
            "ANullAsyncResourceAfterASyncOne",
            "{ await using n = null; using s = sr(log, 's'); } log.push('after');",
            "dispose s, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose s, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "OnlyANullAsyncResource",
            "{ await using n = undefined; } log.push('after');",
            "called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 4.d sets only needsAwait back, so the second null owes an await of its own.
            "TwoNullsAroundSyncResources",
            "{ using s1 = sr(log, 's1'); await using n1 = null; using s2 = sr(log, 's2'); await using n2 = null; } log.push('after');",
            "called, t1, dispose s2, t2, dispose s1, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "called, t1, dispose s2, t2, dispose s1, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Nothing to await: the block ends inside the request.
            "OnlySyncResources",
            "{ using s = sr(log, 's'); } log.push('after');",
            "dispose s, after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9",
            "dispose s, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The body's own resources are disposed once the body has completed, as they were before.
            "AtTheTopLevelOfTheBody",
            "await using r = ar(log, 'r'); { await using b = ar(log, 'b'); } log.push('body');",
            "dispose b, called, t1, body, dispose r, t2, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose b, called, t1, body, dispose r, t2, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> GeneratorFromTheStart => new()
    {
        {
            "AYieldAfterTheBlock",
            "{ await using r = ar(log, 'r'); } yield 1; log.push('UNREACHED');",
            "it.next()",
            "dispose r, called, t1, t2, t3, resolved {1,false}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Await's PromiseResolve reads the result's constructor, and its throw is the await's throw completion.
            "APromiseWhoseConstructorGetterThrows",
            "try { { await using r = { [Symbol.asyncDispose]() { log.push('dispose r'); const p = Promise.resolve(1); Object.defineProperty(p, 'constructor', { get() { log.push('get constructor'); throw 'C'; } }); return p; } }; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next()",
            "dispose r, get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The generator is executing while the dispose awaits, so a second next() queues behind the first.
            "ASecondNextQueuesBehindTheDispose",
            "{ await using r = later(log, 'r'); } yield 1;",
            "(it.next().then(r => log.push('first ' + fmt(r))), it.next())",
            "dispose r, called, t1, disposed r, t2, t3, t4, first {1,false}, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> GeneratorAfterTheFirstYield => new()
    {
        {
            "NextAtAYieldInTheBlock",
            "{ await using r = ar(log, 'r'); yield 1; log.push('resumed'); } log.push('after');",
            "it.next()",
            "resumed, dispose r, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "NextAtAYieldAfterTheBlockInALoop",
            "for (let i = 0; i < 2; i++) { { await using r = ar(log, 'r' + i); } yield i; } log.push('after');",
            "it.next()",
            "dispose r0, dispose r1, called, t1, t2, t3, resolved {1,false}, t4, t5, t6, t7, t8, t9"
        },
        {
            "NextAtAYieldInALoopWithAnAwaitInItsUpdate",
            "for (let i = 0; i < 2; i = await Promise.resolve(i + 1)) { await using r = ar(log, 'r' + i); yield i; } log.push('after');",
            "it.next()",
            "dispose r0, called, t1, t2, t3, t4, resolved {1,false}, t5, t6, t7, t8, t9"
        },
        {
            // return() is awaited at the yield (sebastienros/jint#4315), then its return completion leaves the block,
            // which awaits its dispose before the generator completes.
            "ReturnAtAYieldInTheBlock",
            "{ await using r = ar(log, 'r'); yield 1; log.push('UNREACHED'); } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose r, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInTheBlockDisposingALaterPromise",
            "{ await using r = later(log, 'r'); yield 1; } log.push('UNREACHED');",
            "it.return(Promise.resolve(5))",
            "called, t1, dispose r, t2, disposed r, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldNestedInTheBlock",
            "{ await using r = ar(log, 'r'); log.push('got ' + (yield 1)); } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose r, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInALoopBody",
            "for (let i = 0; ; i++) { await using r = ar(log, 'r' + i); yield i; }",
            "it.return(5)",
            "called, t1, dispose r0, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInATryInTheBlock",
            "{ await using r = ar(log, 'r'); try { yield 1; } finally { log.push('finally'); } }",
            "it.return(5)",
            "called, t1, finally, dispose r, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInTheBlockInATry",
            "try { { await using r = ar(log, 'r'); yield 1; } } finally { log.push('finally'); }",
            "it.return(5)",
            "called, t1, dispose r, t2, finally, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // A dispose that rejects replaces the return completion.
            "ReturnAtAYieldWhenTheDisposeRejects",
            "{ await using r = rejecting(log, 'r', 'D'); yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose r, t2, t3, rejected D, t4, t5, t6, t7, t8, t9"
        },
        {
            // ... with a throw a catch around the block sees: the return() request is no longer in force.
            "ReturnAtAYieldWhenTheDisposeRejectsCaught",
            "try { { await using r = rejecting(log, 'r', 'D'); yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose r, t2, caught D, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldWhenTheDisposeThrowsCaught",
            "try { { await using r = throwing(log, 'r', 'D'); yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose r, caught D, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The statement list around the block tests the return() request after the block, so it must not find it.
            "ReturnAtAYieldWhenTheDisposeRejectsBeforeAnotherStatement",
            "try { { await using r = rejecting(log, 'r', 'D'); yield 1; } log.push('UNREACHED'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose r, t2, caught D, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldWhenTheDisposeThrowsBeforeAnotherStatement",
            "try { { await using r = throwing(log, 'r', 'D'); yield 1; } log.push('UNREACHED'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose r, caught D, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInTheBlock",
            "{ await using r = ar(log, 'r'); yield 1; log.push('UNREACHED'); } log.push('UNREACHED');",
            "it.throw('E')",
            "dispose r, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInTheBlockCaught",
            "try { { await using r = ar(log, 'r'); yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose r, called, t1, caught E, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldWhenTheDisposeRejects",
            "try { { await using r = rejecting(log, 'r', 'D'); yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose r, called, t1, caught SuppressedError(D over E), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInALoopBody",
            "for (let i = 0; ; i++) { await using r = ar(log, 'r' + i); yield i; }",
            "it.throw('E')",
            "dispose r0, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9"
        },
    };

    [TestCaseSource(nameof(InBothKinds)), CancelAfter(10000)]
    public void ABlockDisposesItsResources(string name, string body, string asAFunction, string asAGenerator)
    {
        using var _ = new AssertionScope();
        Run("probe(log => (async function () { " + body + " }), it => it(), false);")
            .Should().Be(asAFunction, name + ", in an async function");
        Run("probe(log => (async function* () { " + body + " })(), it => it.next(), false);")
            .Should().Be(asAGenerator, name + ", in an async generator");
    }

    [TestCaseSource(nameof(GeneratorFromTheStart)), CancelAfter(10000)]
    public void ABlockInAnAsyncGeneratorReachedByTheFirstRequest(string name, string body, string request, string expected)
    {
        Run("probe(log => (async function* () { " + body + " })(), (it, log) => " + request + ", false);").Should().Be(expected, name);
    }

    [TestCaseSource(nameof(GeneratorAfterTheFirstYield)), CancelAfter(10000)]
    public void ABlockInAnAsyncGeneratorResumedAtAYield(string name, string body, string request, string expected)
    {
        Run("probe(log => (async function* () { " + body + " })(), (it, log) => " + request + ", true);").Should().Be(expected, name);
    }

    // A try block holding only the disposing block, which runs it itself, and one whose statement list runs it.
    [TestCase("{ using r = { [Symbol.dispose]() { log.push('dispose r'); throw 'D'; } }; yield 1; }")]
    [TestCase("{ using r = { [Symbol.dispose]() { log.push('dispose r'); throw 'D'; } }; yield 1; } log.push('UNREACHED');")]
    [CancelAfter(10000)]
    public void AReturnResumptionInASyncGeneratorGivesWayToTheThrowOfItsDispose(string tryBlock)
    {
        // The dispose's throw replaces the block's return completion (DisposeResources, step 4.e.iii), so the catch
        // around the block sees it and the generator carries on: the return() request is no longer in force.
        const string Script = """
            (() => {
              const log = [];
              function* g() {
                try { TRY_BLOCK } catch (e) { log.push('caught ' + e); }
                log.push('after');
              }
              const it = g();
              it.next();
              const r = it.return(5);
              log.push('{' + r.value + ',' + r.done + '}');
              return log.join(', ');
            })();
            """;

        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        engine.Evaluate(Script.Replace("TRY_BLOCK", tryBlock)).AsString().Should().Be("dispose r, caught D, after, {undefined,true}");
    }

    private static string Run(string script)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        return engine.Evaluate(Prelude + script).UnwrapIfPromise().AsString();
    }
}
