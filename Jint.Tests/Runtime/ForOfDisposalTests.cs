#nullable enable

using AwesomeAssertions.Execution;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.Runtime;

/// <summary>
/// A <c>for (using x of …)</c> or <c>for (await using x of …)</c> loop, and the same heads in a <c>for await</c> loop,
/// dispose each iteration's resource once that iteration's body has completed
/// (<see href="https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset">ForIn/OfBodyEvaluation</see>,
/// step 8.k), through <see href="https://tc39.es/ecma262/#sec-disposeresources">DisposeResources</see>: each
/// async-dispose result is awaited, which suspends the async function or the async generator exactly as an
/// <c>await</c> expression does (<see href="https://tc39.es/ecma262/#await">Await</see>), and a dispose that throws or
/// rejects replaces the iteration's completion, or is wrapped in a <c>SuppressedError</c> over the body's own throw.
/// <para>
/// In an async generator Jint never suspended there: it waited for each result on the spot, running the caller's job
/// queue inside the request, so the rest of the body ran before the caller's next statement — and a dispose that
/// settles a microtask later timed the request out (sebastienros/jint#4358). In every kind of function, a body that
/// suspended at an <c>await</c> or a <c>yield</c> had its resource disposed at the suspension, while the body went on
/// using it, and a <c>continue</c> naming an enclosing loop's label went on with this loop once a dispose had
/// suspended it.
/// </para>
/// <para>
/// The rows use <see cref="AsyncBlockDisposalTests"/>' helpers, and the async ones its microtask ladder. Every expected
/// log is Node 26's.
/// </para>
/// </summary>
public class ForOfDisposalTests
{
    /// <summary>
    /// A body run as an async function and as an async generator, each from the start: its log as a function, then as
    /// a generator. The two differ only in the answer, a value or an iterator result, bar <c>AReturnStatement</c>.
    /// </summary>
    public static TestCases<string, string, string, string> InBothKinds => new()
    {
        {
            // The issue's own example.
            "TheIssuesExample",
            "for (await using x of [ar(log, 'x')]) { log.push('body'); } log.push('after');",
            "body, dispose x, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "TwoIterations",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { log.push('body'); } log.push('after');",
            "body, dispose x1, called, t1, body, dispose x2, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, body, dispose x2, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // A job the caller queued before the request must not run inside it. Jint's generator waited for this
            // result inside the request, where it could never settle, and the request timed out.
            "ADisposeReturningAPromise",
            "for (await using x of [later(log, 'x1'), later(log, 'x2')]) { log.push('body'); } log.push('after');",
            "body, dispose x1, called, t1, disposed x1, t2, body, dispose x2, t3, disposed x2, t4, after, t5, resolved undefined, t6, t7, t8, t9",
            "body, dispose x1, called, t1, disposed x1, t2, body, dispose x2, t3, disposed x2, t4, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            "AnAsyncDisposeMethod",
            "for (await using x of [{ async [Symbol.asyncDispose]() { log.push('dispose start'); await null; log.push('dispose end'); } }]) { log.push('body'); } log.push('after');",
            "body, dispose start, called, t1, dispose end, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body, dispose start, called, t1, dispose end, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // An await using of a sync-disposable resource awaits what its dispose method returns all the same.
            "ASyncDisposableResource",
            "for (await using x of [sr(log, 'x1'), sr(log, 'x2')]) { log.push('body'); } log.push('after');",
            "body, dispose x1, called, t1, body, dispose x2, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, body, dispose x2, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // DisposeResources step 5: a null or undefined resource still owes an await.
            "NullResources",
            "for (await using x of [null, undefined]) { log.push('body'); } log.push('after');",
            "body, called, t1, body, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body, called, t1, body, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ABracelessBody",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) log.push('body'); log.push('after');",
            "body, dispose x1, called, t1, body, dispose x2, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, body, dispose x2, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ABreak",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { log.push('body'); break; } log.push('after');",
            "body, dispose x1, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "AContinue",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { log.push('body'); continue; } log.push('after');",
            "body, dispose x1, called, t1, body, dispose x2, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, body, dispose x2, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 8.m: the continue leaves this loop for the outer one once the dispose has settled. Jint went on with
            // this loop's next iteration, in an async function too.
            "ALabelledContinueOfAnOuterLoop",
            "outer: for (const i of [1, 2]) { for (await using x of [ar(log, 'x' + i), ar(log, 'y' + i)]) { log.push('body ' + i); continue outer; } } log.push('after');",
            "body 1, dispose x1, called, t1, body 2, dispose x2, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body 1, dispose x1, called, t1, body 2, dispose x2, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ALabelledBreakOfAnOuterLoop",
            "outer: for (const i of [1, 2]) { for (await using x of [ar(log, 'x' + i), ar(log, 'y' + i)]) { log.push('body ' + i); break outer; } } log.push('after');",
            "body 1, dispose x1, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body 1, dispose x1, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "AThrowCaught",
            "try { for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { log.push('body'); throw 'E'; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x1, called, t1, caught E, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, caught E, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "AThrowUncaught",
            "for (await using x of [ar(log, 'x1')]) { throw 'E'; } log.push('UNREACHED');",
            "dispose x1, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // A generator's `return 5;` awaits its operand first (sebastienros/jint#4332), an async function's does not.
            "AReturnStatement",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { log.push('body'); return 5; } log.push('UNREACHED');",
            "body, dispose x1, called, t1, t2, resolved 5, t3, t4, t5, t6, t7, t8, t9",
            "body, called, t1, dispose x1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ADisposeThatRejects",
            "try { for (await using x of [rejecting(log, 'x1', 'D'), ar(log, 'x2')]) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x1, called, t1, caught D, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, caught D, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ADisposeThatRejectsUncaught",
            "for (await using x of [rejecting(log, 'x1', 'D')]) { log.push('body'); } log.push('UNREACHED');",
            "body, dispose x1, called, t1, t2, rejected D, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, t2, rejected D, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // A dispose method that throws has no result to await (DisposeResources step 4.e.ii applies to a normal
            // completion only).
            "ADisposeThatThrows",
            "try { for (await using x of [throwing(log, 'x1', 'D'), ar(log, 'x2')]) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x1, caught D, after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, caught D, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "TheBodyThrowsAndTheDisposeRejects",
            "try { for (await using x of [rejecting(log, 'x1', 'D')]) { throw 'B'; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x1, called, t1, caught SuppressedError(D over B), after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, caught SuppressedError(D over B), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The rejection replaces the break completion.
            "ABreakAndADisposeThatRejects",
            "try { for (await using x of [rejecting(log, 'x1', 'D'), ar(log, 'x2')]) { log.push('body'); break; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x1, called, t1, caught D, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, caught D, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The resource outlives the body's await. Jint disposed it there, in an async function too.
            "AnAwaitInTheBody",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { await null; log.push('body'); } log.push('after');",
            "called, t1, body, dispose x1, t2, t3, body, dispose x2, t4, after, t5, resolved undefined, t6, t7, t8, t9",
            "called, t1, body, dispose x1, t2, t3, body, dispose x2, t4, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            "ASyncUsingWithAnAwaitInTheBody",
            "for (using x of [sr(log, 'x1'), sr(log, 'x2')]) { await null; log.push('body'); } log.push('after');",
            "called, t1, body, dispose x1, t2, body, dispose x2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "called, t1, body, dispose x1, t2, body, dispose x2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AnAwaitInTheBodyThenABreak",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { await null; log.push('body'); break; } log.push('after');",
            "called, t1, body, dispose x1, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "called, t1, body, dispose x1, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Resuming at the dispose does not run the enclosing loop's test again.
            "AWhileLoopAroundDoesNotRunItsTestAgain",
            "let i = 0; while (log.push('test ' + i), i < 2) { for (await using x of [ar(log, 'x' + i)]) { i++; } } log.push('after');",
            "test 0, dispose x0, called, t1, test 1, dispose x1, t2, test 2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "test 0, dispose x0, called, t1, test 1, dispose x1, t2, test 2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AnIfAroundDoesNotRunItsTestAgain",
            "let i = 0; if (log.push('test'), i === 0) { for (await using x of [ar(log, 'x')]) { i++; } } else { log.push('UNREACHED'); } log.push('after');",
            "test, dispose x, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "test, dispose x, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "InsideATryWithAFinally",
            "try { for (await using x of [ar(log, 'x')]) { log.push('body'); } } finally { log.push('finally'); } log.push('after');",
            "body, dispose x, called, t1, finally, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x, called, t1, finally, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "AForAwaitLoop",
            "for await (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { log.push('body'); } log.push('after');",
            "called, t1, t2, body, dispose x1, t3, t4, t5, body, dispose x2, t6, t7, t8, after, t9, resolved undefined",
            "called, t1, t2, body, dispose x1, t3, t4, t5, body, dispose x2, t6, t7, t8, after, t9, resolved {undefined,true}"
        },
        {
            "AForAwaitLoopThatBreaks",
            "for await (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { log.push('body'); break; } log.push('after');",
            "called, t1, t2, body, dispose x1, t3, t4, after, t5, resolved undefined, t6, t7, t8, t9",
            "called, t1, t2, body, dispose x1, t3, t4, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            "AForAwaitLoopThatThrows",
            "try { for await (await using x of [ar(log, 'x1')]) { throw 'E'; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "called, t1, t2, dispose x1, t3, t4, caught E, after, t5, resolved undefined, t6, t7, t8, t9",
            "called, t1, t2, dispose x1, t3, t4, caught E, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            "AForAwaitLoopWhoseDisposeRejects",
            "try { for await (await using x of [rejecting(log, 'x1', 'D')]) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "called, t1, t2, body, dispose x1, t3, t4, caught D, after, t5, resolved undefined, t6, t7, t8, t9",
            "called, t1, t2, body, dispose x1, t3, t4, caught D, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            // The dispose settles before the break closes the iterator, and the close's await comes after it.
            "AForAwaitLoopOverAnAsyncIteratorThatBreaks",
            "const src = { [Symbol.asyncIterator]() { let n = 0; return { next() { n++; return Promise.resolve({ value: ar(log, 'x' + n), done: n > 2 }); }, return() { log.push('return'); return Promise.resolve({}); } }; } }; for await (await using x of src) { log.push('body'); if (log.includes('dispose x1')) break; } log.push('after');",
            "called, t1, body, dispose x1, t2, t3, body, dispose x2, t4, return, t5, after, t6, resolved undefined, t7, t8, t9",
            "called, t1, body, dispose x1, t2, t3, body, dispose x2, t4, return, t5, after, t6, resolved {undefined,true}, t7, t8, t9"
        },
        {
            // https://tc39.es/ecma262/#sec-iteratorclose step 6: for any completion but a throw, a return() that throws
            // replaces it, once the dispose has settled. Jint swallowed it, in an async function too.
            "ABreakWhenTheIteratorsReturnThrows",
            "try { for (await using x of closeThrows(log)) { log.push('body'); break; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x1, called, t1, finally, caught F, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, finally, caught F, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "AReturnStatementWhenTheIteratorsReturnThrows",
            "try { for (await using x of closeThrows(log)) { log.push('body'); return 5; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x1, called, t1, finally, caught F, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, called, t1, dispose x1, t2, finally, caught F, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ALabelledBreakWhenTheIteratorsReturnThrows",
            "try { outer: for (const i of [1]) { for (await using x of closeThrows(log)) { log.push('body'); break outer; } } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x1, called, t1, finally, caught F, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, finally, caught F, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ALabelledContinueWhenTheIteratorsReturnThrows",
            "try { outer: for (const i of [1]) { for (await using x of closeThrows(log)) { log.push('body'); continue outer; } } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x1, called, t1, finally, caught F, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, finally, caught F, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 7: ... and so does a return() that answers a value that is not an object, with a TypeError.
            "ABreakWhenTheIteratorsReturnAnswersANonObject",
            "try { for (await using x of closeAnswersANonObject(log)) { log.push('body'); break; } } catch (e) { log.push('caught ' + (e instanceof TypeError ? 'TypeError' : show(e))); } log.push('after');",
            "body, dispose x1, called, t1, return, caught TypeError, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, return, caught TypeError, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 5: a throw completion is the one that is kept.
            "AThrowWhenTheIteratorsReturnThrows",
            "try { for (await using x of closeThrows(log)) { log.push('body'); throw 'E'; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x1, called, t1, finally, caught E, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x1, called, t1, finally, caught E, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> GeneratorFromTheStart => new()
    {
        {
            "AYieldAfterTheLoop",
            "for (await using x of [ar(log, 'x1')]) { log.push('body'); } yield 1; log.push('UNREACHED');",
            "it.next()",
            "body, dispose x1, called, t1, t2, t3, resolved {1,false}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Await's PromiseResolve reads the result's constructor, and its throw is the await's throw completion.
            "APromiseWhoseConstructorGetterThrows",
            "try { for (await using x of [{ [Symbol.asyncDispose]() { log.push('dispose x'); const p = Promise.resolve(1); Object.defineProperty(p, 'constructor', { get() { log.push('get constructor'); throw 'C'; } }); return p; } }]) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next()",
            "body, dispose x, get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The generator is executing while the dispose awaits, so a second next() queues behind the first.
            "ASecondNextQueuesBehindTheDispose",
            "for (await using x of [later(log, 'x')]) { log.push('body'); } yield 1;",
            "(it.next().then(r => log.push('first ' + fmt(r))), it.next())",
            "body, dispose x, called, t1, disposed x, t2, t3, t4, first {1,false}, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> GeneratorAfterTheFirstYield => new()
    {
        {
            // The resource is disposed once the body has completed, not at its yield.
            "NextAtAYieldInTheBody",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { yield 1; log.push('resumed'); } log.push('after');",
            "it.next()",
            "resumed, dispose x1, called, t1, t2, t3, resolved {1,false}, t4, t5, t6, t7, t8, t9"
        },
        {
            "NextAtAYieldInTheLastIteration",
            "for (await using x of [ar(log, 'x1')]) { yield 1; log.push('resumed'); } log.push('after');",
            "it.next()",
            "resumed, dispose x1, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "NextAtAYieldInABracelessBody",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) yield 1; log.push('after');",
            "it.next()",
            "dispose x1, called, t1, t2, t3, resolved {1,false}, t4, t5, t6, t7, t8, t9"
        },
        {
            "NextAtAYieldBeforeTheNextIteration",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { log.push('body'); yield 1; } log.push('after');",
            "it.next()",
            "body, dispose x1, called, t1, body, t2, t3, resolved {1,false}, t4, t5, t6, t7, t8, t9"
        },
        {
            // return() is awaited at the yield (sebastienros/jint#4315), then its return completion leaves the body,
            // whose dispose is awaited before the generator completes.
            "ReturnAtAYieldInTheBody",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { yield 1; log.push('UNREACHED'); } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInTheBodyDisposingALaterPromise",
            "for (await using x of [later(log, 'x1'), ar(log, 'x2')]) { yield 1; } log.push('UNREACHED');",
            "it.return(Promise.resolve(5))",
            "called, t1, dispose x1, t2, disposed x1, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldNestedInTheBody",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { log.push('got ' + (yield 1)); } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInATryInTheBody",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { try { yield 1; } finally { log.push('finally'); } }",
            "it.return(5)",
            "called, t1, finally, dispose x1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInTheLoopInATry",
            "try { for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { yield 1; } } finally { log.push('finally'); }",
            "it.return(5)",
            "called, t1, dispose x1, t2, finally, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // The iterator is closed, and its close awaited, once the dispose has settled.
            "ReturnAtAYieldInAForAwaitLoop",
            "for await (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { yield 1; }",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            // A dispose that rejects replaces the return completion.
            "ReturnAtAYieldWhenTheDisposeRejects",
            "for (await using x of [rejecting(log, 'x1', 'D')]) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, rejected D, t4, t5, t6, t7, t8, t9"
        },
        {
            // ... with a throw a catch around the loop sees.
            "ReturnAtAYieldWhenTheDisposeRejectsCaught",
            "try { for (await using x of [rejecting(log, 'x1', 'D')]) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, t2, caught D, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInTheBody",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { yield 1; log.push('UNREACHED'); } log.push('UNREACHED');",
            "it.throw('E')",
            "dispose x1, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInTheBodyCaught",
            "try { for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x1, called, t1, caught E, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldWhenTheDisposeRejects",
            "try { for (await using x of [rejecting(log, 'x1', 'D')]) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x1, called, t1, caught SuppressedError(D over E), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInAForAwaitLoop",
            "try { for await (await using x of [ar(log, 'x1'), ar(log, 'x2')]) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x1, called, t1, t2, caught E, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // The rejection replaces the return completion here too: the return() request is no longer in force, so the
            // statement list around the loop must not turn the throw back into the return.
            "ReturnAtAYieldInAForAwaitLoopWhenTheDisposeRejects",
            "for await (await using x of [rejecting(log, 'x1', 'D')]) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, t4, rejected D, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInAForAwaitLoopWhenTheDisposeRejectsCaught",
            "try { for await (await using x of [rejecting(log, 'x1', 'D')]) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, caught D, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInAForAwaitLoopWhenTheDisposeRejects",
            "try { for await (await using x of [rejecting(log, 'x1', 'D')]) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x1, called, t1, t2, caught SuppressedError(D over E), after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // The return completion closes the iterator once the dispose has settled, and the close's throw replaces it.
            "ReturnAtAYieldWhenTheIteratorsReturnThrows",
            "try { for (await using x of closeThrows(log)) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, t2, finally, caught F, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
    };

    /// <summary>
    /// A sync generator's <c>for (using x of …)</c>: the resource outlives the body's <c>yield</c>, and is disposed when
    /// the body completes, normally or by a <c>return()</c> or a <c>throw()</c> resumption. Jint disposed it at the yield.
    /// </summary>
    public static TestCases<string, string, string, string> InASyncGenerator => new()
    {
        {
            "NextAtAYieldInTheBody",
            "for (using x of [sr(log, 'a'), sr(log, 'b')]) { log.push('body'); yield 1; log.push('resumed'); } log.push('end');",
            "it.next(); log.push('step'); it.next(); log.push('step'); it.next();",
            "body, step, resumed, dispose a, body, step, resumed, dispose b, end"
        },
        {
            "ReturnAtAYieldInTheBody",
            "for (using x of [sr(log, 'a'), sr(log, 'b')]) { log.push('body'); yield 1; log.push('UNREACHED'); }",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "body, step, dispose a, {5,true}"
        },
        {
            "ThrowAtAYieldInTheBodyCaught",
            "try { for (using x of [sr(log, 'a'), sr(log, 'b')]) { yield 1; } } catch (e) { log.push('caught ' + e); } log.push('after');",
            "it.next(); log.push('step'); log.push(fmt(it.throw('E')));",
            "step, dispose a, caught E, after, {undefined,true}"
        },
        {
            "NextAtAYieldInABracelessBody",
            "for (using x of [sr(log, 'a'), sr(log, 'b')]) yield 1; log.push('end');",
            "it.next(); log.push('step'); it.next(); log.push('step'); it.next();",
            "step, dispose a, step, dispose b, end"
        },
    };

    [TestCaseSource(nameof(InBothKinds)), CancelAfter(10000)]
    public void AnAsyncLoopDisposesEachIteration(string name, string body, string asAFunction, string asAGenerator)
    {
        using var _ = new AssertionScope();
        RunAsync("probe(log => (async function () { " + body + " }), it => it(), false);")
            .Should().Be(asAFunction, name + ", in an async function");
        RunAsync("probe(log => (async function* () { " + body + " })(), it => it.next(), false);")
            .Should().Be(asAGenerator, name + ", in an async generator");
    }

    [TestCaseSource(nameof(GeneratorFromTheStart)), CancelAfter(10000)]
    public void ALoopInAnAsyncGeneratorReachedByTheFirstRequest(string name, string body, string request, string expected)
    {
        RunAsync("probe(log => (async function* () { " + body + " })(), (it, log) => " + request + ", false);").Should().Be(expected, name);
    }

    [TestCaseSource(nameof(GeneratorAfterTheFirstYield)), CancelAfter(10000)]
    public void ALoopInAnAsyncGeneratorResumedAtAYield(string name, string body, string request, string expected)
    {
        RunAsync("probe(log => (async function* () { " + body + " })(), (it, log) => " + request + ", true);").Should().Be(expected, name);
    }

    [TestCaseSource(nameof(InASyncGenerator)), CancelAfter(10000)]
    public void ALoopInASyncGeneratorDisposesEachIterationWhenItsBodyCompletes(string name, string body, string driver, string expected)
    {
        Run("""
            (() => {
              const log = [];
              const it = (function* () { BODY })();
              DRIVER
              return log.join(', ');
            })();
            """.Replace("BODY", body).Replace("DRIVER", driver)).Should().Be(expected, name);
    }

    [Test]
    public void TheLoopKeepsItsCompletionValue()
    {
        var engine = new Engine();
        engine.Evaluate("let n = 0; for (using x of [{ [Symbol.dispose]() {} }, { [Symbol.dispose]() {} }]) { n++ * 10; }").AsNumber().Should().Be(10);
    }

    /// <summary>
    /// A body's throw waits for the iteration's dispose, and then leaves the loop as the throw completion it already
    /// is: it is reported to the debugger once, where it was thrown, not a second time where the loop hands it on.
    /// </summary>
    [Test, CancelAfter(10000)]
    public void ABodysThrowThatWaitsForTheDisposeIsReportedOnce()
    {
        var engine = new Engine(options =>
        {
            options.Debugger.Enabled = true;
            options.ObserveCancellation(TestContext.CurrentContext.CancellationToken);
        });

        var thrown = new List<string>();
        engine.Debugger.ExceptionThrown += (_, args) => thrown.Add(args.ThrownValue.ToString());

        engine.Execute("""
            const r = () => ({ [Symbol.asyncDispose]() {} });
            (async function () {
                try { for (await using x of [r()]) { throw 'function'; } } catch (e) {}
                try { for await (await using x of [r()]) { throw 'function, for await'; } } catch (e) {}
            })();
            (async function* () {
                try { for (await using x of [r()]) { throw 'generator'; } } catch (e) {}
                try { for await (await using x of [r()]) { throw 'generator, for await'; } } catch (e) {}
            })().next();
            """);

        thrown.OrderBy(s => s, StringComparer.Ordinal).Should().Equal("function", "function, for await", "generator", "generator, for await");
    }

    /// <summary>
    /// A host exception that escapes the rest of an async function's body, once a dispose await has resumed it, reaches
    /// the host as one escaping after an <c>await</c> does, whatever <c>Interop.ExceptionHandler</c> says: the dispose
    /// await resumes the function directly, not through a pair of handler functions that turned the exception into a
    /// script error with nowhere to go and left the function's promise pending.
    /// </summary>
    [TestCase("await null; host.Boom;")]
    [TestCase("{ await using r = { [Symbol.asyncDispose]() {} }; } host.Boom;")]
    [TestCase("let n = 0; for (await using r = { [Symbol.asyncDispose]() {} }; n < 1; n++) { } host.Boom;")]
    [TestCase("for (await using r of [{ [Symbol.asyncDispose]() {} }]) { } host.Boom;")]
    [CancelAfter(10000)]
    public void AHostExceptionAfterADisposeAwaitReachesTheHost(string body)
    {
        var engine = new Engine(options => options.CatchClrExceptions().ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        engine.SetValue("host", new ThrowingObject(engine));

        Caught.Exception(() => engine.Execute("(async function () { " + body + " })();"))
            .Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("boom");
    }

    // A host object whose property read throws a CLR exception the interpreter does not translate.
    private sealed class ThrowingObject(Engine engine) : ObjectInstance(engine)
    {
        public override JsValue Get(JsValue property, JsValue receiver) => throw new InvalidOperationException("boom");
    }

    // An iterable whose return() throws F once its generator's finally runs, and one whose return() answers a number.
    private const string IteratorHelpers = """
        function closeThrows(log) {
          return (function* () { try { yield ar(log, 'x1'); yield ar(log, 'x2'); } finally { log.push('finally'); throw 'F'; } })();
        }
        function closeAnswersANonObject(log) {
          let n = 0;
          return { [Symbol.iterator]() { return { next() { n++; return { value: ar(log, 'x' + n), done: false }; }, return() { log.push('return'); return 1; } }; } };
        }

        """;

    private static string RunAsync(string script)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        return engine.Evaluate(AsyncBlockDisposalTests.Prelude + IteratorHelpers + script).UnwrapIfPromise().AsString();
    }

    private static string Run(string script)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        return engine.Evaluate(AsyncBlockDisposalTests.Prelude + script).AsString();
    }
}
