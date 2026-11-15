#nullable enable

using AwesomeAssertions.Execution;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

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

    /// <summary>
    /// A body that is not a block and leaves as an exception rather than as a completion — a call that throws, an
    /// operator's <c>TypeError</c>, an <c>if</c> whose test throws, a nested statement — is a throw completion all the
    /// same, and step 8.k disposes the iteration's resources with it before the iterator is closed
    /// (sebastienros/jint#4393). Jint skipped the dispose. A <c>throw</c> statement was always a completion, and is the
    /// control.
    /// </summary>
    public static TestCases<string, string, string> BracelessBodiesInASyncFunction => new()
    {
        {
            "AThrowStatement",
            "try { for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) throw 'E'; } catch (e) { log.push('caught ' + show(e)); }",
            "dispose a, return, caught E"
        },
        {
            "ACallThatThrows",
            "try { for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) boom('E'); } catch (e) { log.push('caught ' + show(e)); }",
            "dispose a, return, caught E"
        },
        {
            "ACallThatThrowsNotCaught",
            "try { (() => { for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) boom('E'); log.push('UNREACHED'); })(); } catch (e) { log.push('uncaught ' + show(e)); }",
            "dispose a, return, uncaught E"
        },
        {
            "ATypeError",
            "try { for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) undefined.p; } catch (e) { log.push('caught ' + (e instanceof TypeError ? 'TypeError' : show(e))); }",
            "dispose a, return, caught TypeError"
        },
        {
            "AnIfWhoseTestThrows",
            "try { for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) if (boom('E')) log.push('UNREACHED'); } catch (e) { log.push('caught ' + show(e)); }",
            "dispose a, return, caught E"
        },
        {
            "ALabelledStatement",
            "try { for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) l: boom('E'); } catch (e) { log.push('caught ' + show(e)); }",
            "dispose a, return, caught E"
        },
        {
            // The inner loop disposes and closes, then hands its throw completion to the outer one, which does the same.
            "ANestedLoop",
            "try { for (using x of logged(log, [sr(log, 'a')])) for (using y of logged(log, [sr(log, 'b')])) boom('E'); } catch (e) { log.push('caught ' + show(e)); }",
            "dispose b, return, dispose a, return, caught E"
        },
        {
            "ANestedLoopWhoseHeadThrows",
            "try { for (using x of logged(log, [sr(log, 'a')])) for (const y of 1) log.push('UNREACHED'); } catch (e) { log.push('caught ' + (e instanceof TypeError ? 'TypeError' : show(e))); }",
            "dispose a, return, caught TypeError"
        },
        {
            "ALoopInsideAForIn",
            "try { for (const k in { a: 1 }) for (using x of logged(log, [sr(log, k)])) boom('E'); } catch (e) { log.push('caught ' + show(e)); }",
            "dispose a, return, caught E"
        },
        {
            // DisposeResources step 4.e.iii.1: the dispose's throw is wrapped over the body's.
            "TheBodyThrowsAndTheDisposeThrows",
            "try { for (using x of logged(log, [throwingSync(log, 'a', 'D')])) boom('E'); } catch (e) { log.push('caught ' + show(e)); }",
            "dispose a, return, caught SuppressedError(D over E)"
        },
    };

    /// <summary>
    /// The same bodies in an async function and an async generator, with <c>await using</c> as well: the dispose awaits
    /// with the body's throw parked, as it does for a block body.
    /// </summary>
    public static TestCases<string, string, string, string> BracelessBodiesInBothKinds => new()
    {
        {
            "AThrowStatement",
            "try { for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) throw 'E'; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x1, called, t1, caught E, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, caught E, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ACallThatThrows",
            "try { for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) boom('E'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x1, called, t1, caught E, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, caught E, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ACallThatThrowsUncaught",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) boom('E'); log.push('UNREACHED');",
            "dispose x1, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ATypeError",
            "try { for (await using x of [ar(log, 'x1')]) undefined.p; } catch (e) { log.push('caught ' + (e instanceof TypeError ? 'TypeError' : show(e))); } log.push('after');",
            "dispose x1, called, t1, caught TypeError, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, caught TypeError, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ASyncUsing",
            "try { for (using x of [sr(log, 'x1'), sr(log, 'x2')]) boom('E'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x1, caught E, after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, caught E, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "TheBodyThrowsAndTheDisposeSettlesLater",
            "try { for (await using x of [later(log, 'x1')]) boom('B'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x1, called, t1, disposed x1, t2, caught B, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, disposed x1, t2, caught B, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "TheBodyThrowsAndTheDisposeRejects",
            "try { for (await using x of [rejecting(log, 'x1', 'D')]) boom('B'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x1, called, t1, caught SuppressedError(D over B), after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, caught SuppressedError(D over B), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "TheBodyThrowsAndTheDisposeThrows",
            "try { for (await using x of [throwing(log, 'x1', 'D')]) boom('B'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x1, caught SuppressedError(D over B), after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, caught SuppressedError(D over B), after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // IteratorClose step 5: return()'s throw is discarded for the body's, once the dispose has settled.
            "WhenTheIteratorsReturnThrows",
            "try { for (await using x of closeThrows(log)) boom('E'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x1, called, t1, finally, caught E, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, finally, caught E, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "InsideATryWithAFinally",
            "try { for (await using x of [ar(log, 'x1')]) boom('E'); } catch (e) { log.push('caught ' + show(e)); } finally { log.push('finally'); } log.push('after');",
            "dispose x1, called, t1, caught E, finally, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x1, called, t1, caught E, finally, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The dispose settles before AsyncIteratorClose awaits.
            "AForAwaitLoop",
            "try { for await (await using x of [ar(log, 'x1'), ar(log, 'x2')]) boom('E'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "called, t1, t2, dispose x1, t3, t4, caught E, after, t5, resolved undefined, t6, t7, t8, t9",
            "called, t1, t2, dispose x1, t3, t4, caught E, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            "AForAwaitLoopWithASyncUsing",
            "try { for await (using x of [sr(log, 'x1'), sr(log, 'x2')]) boom('E'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "called, t1, t2, dispose x1, t3, caught E, after, t4, resolved undefined, t5, t6, t7, t8, t9",
            "called, t1, t2, dispose x1, t3, caught E, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
        {
            "AForAwaitLoopWhoseDisposeRejects",
            "try { for await (await using x of [rejecting(log, 'x1', 'D')]) boom('B'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "called, t1, t2, dispose x1, t3, t4, caught SuppressedError(D over B), after, t5, resolved undefined, t6, t7, t8, t9",
            "called, t1, t2, dispose x1, t3, t4, caught SuppressedError(D over B), after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
    };

    /// <summary>
    /// A brace-less body that a generator's <c>return()</c> or <c>throw()</c> resumption ends at its <c>yield</c>: the
    /// yield evaluates to that abrupt completion (https://tc39.es/ecma262/#sec-generatorresumeabrupt), which Jint carries
    /// as an exception out of an expression, and the iteration is disposed with it. Jint skipped the dispose; until
    /// sebastienros/jint#4358 it disposed the resource at the yield instead.
    /// </summary>
    public static TestCases<string, string, string, string> BracelessYieldsInASyncGenerator => new()
    {
        {
            "ReturnAtAYield",
            "for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) yield 1; log.push('UNREACHED');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose a, return, {5,true}"
        },
        {
            "ReturnAtANestedYield",
            "for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) log.push('got ' + (yield 1)); log.push('UNREACHED');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose a, return, {5,true}"
        },
        {
            "ReturnAtAYieldStar",
            "for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) yield* (function* () { try { yield 1; } finally { log.push('inner finally'); } })(); log.push('UNREACHED');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, inner finally, dispose a, return, {5,true}"
        },
        {
            "ReturnAtAYieldInATryWithAFinally",
            "try { for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) yield 1; } finally { log.push('finally'); }",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose a, return, finally, {5,true}"
        },
        {
            // The dispose's throw replaces the return completion.
            "ReturnAtAYieldWhenTheDisposeThrows",
            "try { for (using x of logged(log, [throwingSync(log, 'a', 'D')])) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose a, return, caught D, after, {undefined,true}"
        },
        {
            "ReturnAtAYieldWhenTheDisposeThrowsUncaught",
            "for (using x of logged(log, [throwingSync(log, 'a', 'D')])) yield 1; log.push('UNREACHED');",
            "it.next(); log.push('step'); try { it.return(5); } catch (e) { log.push('threw ' + show(e)); }",
            "step, dispose a, return, threw D"
        },
        {
            "ThrowAtAYield",
            "try { for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next(); log.push('step'); log.push(fmt(it.throw('E')));",
            "step, dispose a, return, caught E, after, {undefined,true}"
        },
        {
            "ThrowAtAYieldUncaught",
            "for (using x of logged(log, [sr(log, 'a'), sr(log, 'b')])) yield 1; log.push('UNREACHED');",
            "it.next(); log.push('step'); try { it.throw('E'); } catch (e) { log.push('threw ' + show(e)); }",
            "step, dispose a, return, threw E"
        },
        {
            "ThrowAtAYieldWhenTheDisposeThrows",
            "try { for (using x of logged(log, [throwingSync(log, 'a', 'D')])) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next(); log.push('step'); log.push(fmt(it.throw('E')));",
            "step, dispose a, return, caught SuppressedError(D over E), after, {undefined,true}"
        },
    };

    /// <summary>
    /// A <c>return()</c> resumption leaving a braced body whose iteration's dispose then throws: the throw replaces the
    /// return completion, and a <c>catch</c> around the loop sees it (sebastienros/jint#4360). Jint completed the
    /// generator with the return value, because the request <c>return()</c> raised was still in force once the dispose
    /// had thrown.
    /// </summary>
    public static TestCases<string, string, string, string> ADisposeThatThrowsOverAReturnInASyncGenerator => new()
    {
        {
            "AYieldThatIsTheWholeBody",
            "try { for (using x of logged(log, [throwingSync(log, 'a', 'D')])) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose a, return, caught D, after, {undefined,true}"
        },
        {
            "AYieldThatIsTheWholeBodyUncaught",
            "for (using x of logged(log, [throwingSync(log, 'a', 'D')])) { yield 1; } log.push('UNREACHED');",
            "it.next(); log.push('step'); try { it.return(5); } catch (e) { log.push('threw ' + show(e)); }",
            "step, dispose a, return, threw D"
        },
        {
            "AYieldInAStatementList",
            "try { for (using x of logged(log, [throwingSync(log, 'a', 'D')])) { log.push('body'); yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "body, step, dispose a, return, caught D, after, {undefined,true}"
        },
    };

    /// <summary>
    /// The async generator's counterpart of <see cref="BracelessYieldsInASyncGenerator"/>, the generator first run to its
    /// first yield. A <c>return()</c> is awaited at the yield (sebastienros/jint#4315), then the body's return completion
    /// waits for the dispose, as it does for a block body.
    /// </summary>
    public static TestCases<string, string, string, string> BracelessYieldsInAnAsyncGenerator => new()
    {
        {
            "ReturnAtAYield",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtANestedYield",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) log.push('got ' + (yield 1)); log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldDisposingALaterPromise",
            "for (await using x of [later(log, 'x1'), ar(log, 'x2')]) yield 1; log.push('UNREACHED');",
            "it.return(Promise.resolve(5))",
            "called, t1, dispose x1, t2, disposed x1, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInATryWithAFinally",
            "try { for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) yield 1; } finally { log.push('finally'); }",
            "it.return(5)",
            "called, t1, dispose x1, t2, finally, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldWithASyncUsing",
            "for (using x of [sr(log, 'x1'), sr(log, 'x2')]) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, resolved {5,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldWhenTheDisposeRejects",
            "try { for (await using x of [rejecting(log, 'x1', 'D')]) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, t2, caught D, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldWhenTheDisposeRejectsUncaught",
            "for (await using x of [rejecting(log, 'x1', 'D')]) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, rejected D, t4, t5, t6, t7, t8, t9"
        },
        {
            // A dispose method that throws has no result to await, and its throw replaces the return completion at once.
            "ReturnAtAYieldWhenTheDisposeThrows",
            "try { for (await using x of [throwing(log, 'x1', 'D')]) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, caught D, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldWhenTheIteratorsReturnThrows",
            "try { for (await using x of closeThrows(log)) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, t2, finally, caught F, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYield",
            "try { for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x1, called, t1, caught E, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldUncaught",
            "for (await using x of [ar(log, 'x1'), ar(log, 'x2')]) yield 1; log.push('UNREACHED');",
            "it.throw('E')",
            "dispose x1, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldWithASyncUsing",
            "try { for (using x of [sr(log, 'x1'), sr(log, 'x2')]) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x1, caught E, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldWhenTheDisposeRejects",
            "try { for (await using x of [rejecting(log, 'x1', 'D')]) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x1, called, t1, caught SuppressedError(D over E), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The iterator is closed, and its close awaited, once the dispose has settled.
            "ReturnAtAYieldInAForAwaitLoop",
            "for await (await using x of [ar(log, 'x1'), ar(log, 'x2')]) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInAForAwaitLoopWhenTheDisposeRejects",
            "try { for await (await using x of [rejecting(log, 'x1', 'D')]) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, caught D, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInAForAwaitLoop",
            "try { for await (await using x of [ar(log, 'x1'), ar(log, 'x2')]) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x1, called, t1, t2, caught E, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInAForAwaitLoopWhenTheDisposeRejects",
            "try { for await (await using x of [rejecting(log, 'x1', 'D')]) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x1, called, t1, t2, caught SuppressedError(D over E), after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // sebastienros/jint#4360 in an async generator, with braces: a dispose that throws without being awaited.
            "ReturnAtABracedYieldWhenTheDisposeThrows",
            "try { for (await using x of [throwing(log, 'x1', 'D')]) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, caught D, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtABracedYieldWhenTheDisposeThrowsUncaught",
            "for (await using x of [throwing(log, 'x1', 'D')]) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x1, t2, rejected D, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtABracedYieldWithASyncUsingWhenTheDisposeThrows",
            "try { for (using x of [throwingSync(log, 'x1', 'D')]) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, caught D, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtABracedYieldInAForAwaitLoopWhenTheDisposeThrows",
            "try { for await (await using x of [throwing(log, 'x1', 'D')]) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x1, t2, caught D, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
    };

    [TestCaseSource(nameof(InBothKinds)), CancelAfter(10000)]
    public void AnAsyncLoopDisposesEachIteration(string name, string body, string asAFunction, string asAGenerator)
        => RunInBothKinds(name, body, asAFunction, asAGenerator);

    [TestCaseSource(nameof(BracelessBodiesInBothKinds)), CancelAfter(10000)]
    public void ABracelessBodyLeavingAsAnExceptionDisposesItsIteration(string name, string body, string asAFunction, string asAGenerator)
        => RunInBothKinds(name, body, asAFunction, asAGenerator);

    [TestCaseSource(nameof(BracelessBodiesInASyncFunction)), CancelAfter(10000)]
    public void ABracelessBodyLeavingAsAnExceptionDisposesItsIterationInASyncFunction(string name, string body, string expected)
    {
        Run("""
            (() => {
              const log = [];
              BODY
              return log.join(', ');
            })();
            """.Replace("BODY", body)).Should().Be(expected, name);
    }

    [TestCaseSource(nameof(BracelessYieldsInASyncGenerator)), CancelAfter(10000)]
    public void ABracelessBodyEndedAtItsYieldDisposesItsIterationInASyncGenerator(string name, string body, string driver, string expected)
        => ALoopInASyncGeneratorDisposesEachIterationWhenItsBodyCompletes(name, body, driver, expected);

    [TestCaseSource(nameof(ADisposeThatThrowsOverAReturnInASyncGenerator)), CancelAfter(10000)]
    public void ADisposeThatThrowsReplacesAReturnResumptionInASyncGenerator(string name, string body, string driver, string expected)
        => ALoopInASyncGeneratorDisposesEachIterationWhenItsBodyCompletes(name, body, driver, expected);

    [TestCaseSource(nameof(BracelessYieldsInAnAsyncGenerator)), CancelAfter(10000)]
    public void ABracelessBodyEndedAtItsYieldDisposesItsIterationInAnAsyncGenerator(string name, string body, string request, string expected)
        => ALoopInAnAsyncGeneratorResumedAtAYield(name, body, request, expected);

    /// <summary>
    /// The issue's own example, at the top level of a script.
    /// </summary>
    [Test, CancelAfter(10000)]
    public void TheIssuesExample()
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        engine.Evaluate("""
            const log = [];
            const res = { [Symbol.dispose]() { log.push('disposed'); } };
            function f() { throw new Error('E'); }
            try {
              for (using x of [res]) f();
            } catch (e) { log.push('caught'); }
            log.join();
            """).AsString().Should().Be("disposed,caught");
    }

    /// <summary>
    /// A brace-less body's throw is reported to the debugger once, where it was thrown, whether it leaves as an exception
    /// or waits for an await using dispose; a dispose that throws over it is reported where it throws, and the
    /// <c>SuppressedError</c> wrapping the two is not a throw of its own.
    /// </summary>
    [Test, CancelAfter(10000)]
    public void ABracelessBodysThrowIsReportedOnce()
    {
        var engine = new Engine(options =>
        {
            options.Debugger.Enabled = true;
            options.ObserveCancellation(TestContext.CurrentContext.CancellationToken);
        });

        var thrown = new List<string>();
        engine.Debugger.ExceptionThrown += (_, args) => thrown.Add(args.ThrownValue.IsObject() ? "TypeError" : args.ThrownValue.ToString());

        engine.Execute("""
            const r = () => ({ [Symbol.dispose]() {} });
            const ar = () => ({ [Symbol.asyncDispose]() {} });
            const d = () => ({ [Symbol.dispose]() { throw 'dispose'; } });
            function boom(e) { throw e; }
            try { for (using x of [r()]) boom('call'); } catch (e) {}
            try { for (using x of [r()]) undefined.p; } catch (e) {}
            try { for (using x of [d()]) boom('under a dispose'); } catch (e) {}
            const it = (function* () { try { for (using x of [r(), r()]) yield 1; } catch (e) {} })();
            it.next();
            it.throw('throw()');
            (async function () {
                try { for (await using x of [ar()]) boom('function'); } catch (e) {}
                try { for await (await using x of [ar()]) boom('function, for await'); } catch (e) {}
            })();
            (async function* () {
                try { for (await using x of [ar()]) boom('generator'); } catch (e) {}
            })().next();
            """);

        thrown.OrderBy(s => s, StringComparer.Ordinal).Should().Equal(
            "TypeError", "call", "dispose", "function", "function, for await", "generator", "throw()", "under a dispose");
    }

    /// <summary>
    /// What a brace-less body hands the loop as a completion is only what a statement list would: a host exception and a
    /// constraint still unwind through the loop to the host untouched, exactly as they do from a block body, whose rules
    /// for disposing on the way out the brace-less body now shares.
    /// </summary>
    [TestCase("for (using x of [r]) host.Boom;", "for (using x of [r]) { host.Boom; }")]
    [TestCase("for (using x of [r]) l: host.Boom;", "for (using x of [r]) { l: host.Boom; }")]
    [CancelAfter(10000)]
    public void AHostExceptionInABracelessBodyReachesTheHostAsOneInABlockBodyDoes(string braceless, string braced)
    {
        var (bracelessException, bracelessLog) = RunWithHostObject(braceless);
        var (bracedException, bracedLog) = RunWithHostObject(braced);

        using var _ = new AssertionScope();
        bracelessException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("boom");
        bracedException.Should().BeOfType<InvalidOperationException>();
        bracelessLog.Should().Be(bracedLog);

        static (Exception? Exception, string Log) RunWithHostObject(string loop)
        {
            var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
            engine.SetValue("host", new ThrowingObject(engine));
            engine.Execute("var log = []; var r = { [Symbol.dispose]() { log.push('disposed'); } };");
            var exception = Caught.Exception(() => engine.Execute("try { " + loop + " } catch (e) { log.push('caught'); }"));
            return (exception, engine.Evaluate("log.join()").AsString());
        }
    }

    [Test, CancelAfter(10000)]
    public void AStatementLimitInABracelessBodyStaysFatal()
    {
        var engine = new Engine(options =>
        {
            options.LimitStatements(200);
            options.ObserveCancellation(TestContext.CurrentContext.CancellationToken);
        });

        Caught.Exception(() => engine.Execute("""
            var r = { [Symbol.dispose]() {} };
            try { for (using x of [r]) (function () { while (true) {} })(); } catch (e) { throw 'caught a constraint'; }
            """)).Should().BeOfType<StatementsCountOverflowException>();
    }

    [Test, CancelAfter(10000)]
    public void ARecursionLimitInABracelessBodyStaysFatal()
    {
        var engine = new Engine(options =>
        {
            options.Constraints.MaxRecursionDepth = 20;
            options.ObserveCancellation(TestContext.CurrentContext.CancellationToken);
        });

        Caught.Exception(() => engine.Execute("""
            var r = { [Symbol.dispose]() {} };
            function f() { for (using x of [r]) f(); }
            try { f(); } catch (e) { throw 'caught a constraint'; }
            """)).Should().BeOfType<RecursionDepthOverflowException>();
    }

    private static void RunInBothKinds(string name, string body, string asAFunction, string asAGenerator)
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

    // An iterable whose return() throws F once its generator's finally runs, one whose return() answers a number, one
    // over a list whose return() is logged, a function that throws its argument, and a sync-disposable resource whose
    // dispose throws.
    private const string IteratorHelpers = """
        function closeThrows(log) {
          return (function* () { try { yield ar(log, 'x1'); yield ar(log, 'x2'); } finally { log.push('finally'); throw 'F'; } })();
        }
        function closeAnswersANonObject(log) {
          let n = 0;
          return { [Symbol.iterator]() { return { next() { n++; return { value: ar(log, 'x' + n), done: false }; }, return() { log.push('return'); return 1; } }; } };
        }
        function logged(log, items) {
          return { [Symbol.iterator]() { let i = 0; return { next() { return i < items.length ? { value: items[i++], done: false } : { value: undefined, done: true }; }, return() { log.push('return'); return {}; } }; } };
        }
        function boom(reason) {
          throw reason;
        }
        function throwingSync(log, name, reason) {
          return { [Symbol.dispose]() { log.push('dispose ' + name); throw reason; } };
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
        return engine.Evaluate(AsyncBlockDisposalTests.Prelude + IteratorHelpers + script).AsString();
    }
}
