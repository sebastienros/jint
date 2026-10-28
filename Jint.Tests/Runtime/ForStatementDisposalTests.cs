#nullable enable

using AwesomeAssertions.Execution;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// A <c>for (using …; …; …)</c> or <c>for (await using …; …; …)</c> loop disposes its header's resources once, after
/// the loop has ended (<see href="https://tc39.es/ecma262/#sec-runtime-semantics-forloopevaluation">ForLoopEvaluation</see>,
/// step 13), or after a declarator of the header has thrown (step 8), through
/// <see href="https://tc39.es/ecma262/#sec-disposeresources">DisposeResources</see>: an async dispose result is awaited,
/// which suspends the function or generator, and a dispose that throws replaces the loop's completion, or is wrapped in a
/// <c>SuppressedError</c> over the loop's own throw.
/// <para>
/// Jint disposed them whenever the statement was left, including at every <c>await</c> or <c>yield</c> in the loop, and
/// threw away what the dispose produced: an <c>await using</c> was never awaited, a sync <c>using</c> was disposed at
/// the first suspension, and a throwing dispose was swallowed (sebastienros/jint#4359).
/// </para>
/// <para>
/// The rows use <see cref="AsyncBlockDisposalTests"/>' helpers, and the async ones its microtask ladder. Every expected
/// log is Node 24's, except where a row says it is the specification's: there V8 disposes the resource twice, crashes, or
/// never finishes.
/// </para>
/// </summary>
public class ForStatementDisposalTests
{
    private const string SyncHelpers = """
        function syncThrowing(log, name, reason) {
          return { [Symbol.dispose]() { log.push('dispose ' + name); throw reason; } };
        }

        """;

    /// <summary>
    /// A body run as an async function and as an async generator, each from the start: its log as a function, then as
    /// a generator.
    /// </summary>
    public static TestCases<string, string, string, string> InBothKinds => new()
    {
        {
            // The issue's first example.
            "TheIssuesExample",
            "let n = 0; for (await using x = ar(log, 'x'); n < 1; n++) { log.push('body'); } log.push('after');",
            "body, dispose x, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The issue's second example: a sync resource outlives every await of the loop.
            "ASyncUsingWithAnAwaitInTheBody",
            "let n = 0; for (using x = sr(log, 'x'); n < 2; n++) { await null; log.push('body ' + n); } log.push('after');",
            "called, t1, body 0, t2, body 1, dispose x, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "called, t1, body 0, t2, body 1, dispose x, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AnAwaitUsingWithAnAwaitInTheBody",
            "let n = 0; for (await using x = ar(log, 'x'); n < 2; n++) { await null; log.push('body ' + n); } log.push('after');",
            "called, t1, body 0, t2, body 1, dispose x, t3, after, t4, resolved undefined, t5, t6, t7, t8, t9",
            "called, t1, body 0, t2, body 1, dispose x, t3, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
        {
            "ADisposeReturningAPromise",
            "let n = 0; for (await using x = later(log, 'x'); n < 1; n++) { log.push('body'); } log.push('after');",
            "body, dispose x, called, t1, disposed x, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body, dispose x, called, t1, disposed x, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Last declared, first disposed, and each result is awaited before the next dispose method runs.
            "TwoResourcesInReverseOrder",
            "let n = 0; for (await using a = ar(log, 'a'), b = later(log, 'b'); n < 1; n++) { log.push('body'); } log.push('after');",
            "body, dispose b, called, t1, disposed b, t2, dispose a, t3, after, t4, resolved undefined, t5, t6, t7, t8, t9",
            "body, dispose b, called, t1, disposed b, t2, dispose a, t3, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
        {
            "ABreak",
            "for (await using x = ar(log, 'x'); ; ) { log.push('body'); break; } log.push('after');",
            "body, dispose x, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // A continue goes on to the next iteration, so the resource is disposed once, at the end.
            "AContinue",
            "let n = 0; for (await using x = ar(log, 'x'); n < 2; n++) { log.push('body ' + n); continue; } log.push('after');",
            "body 0, body 1, dispose x, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body 0, body 1, dispose x, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ALabelledContinueOfAnOuterLoop",
            "outer: for (let j = 0; j < 2; j++) { for (await using x = ar(log, 'x' + j); ; ) { log.push('body ' + j); continue outer; } } log.push('after');",
            "body 0, dispose x0, called, t1, body 1, dispose x1, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body 0, dispose x0, called, t1, body 1, dispose x1, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // A generator's `return 5;` awaits its operand first (sebastienros/jint#4332), an async function's does not.
            "AReturn",
            "for (await using x = ar(log, 'x'); ; ) { log.push('body'); return 5; }",
            "body, dispose x, called, t1, t2, resolved 5, t3, t4, t5, t6, t7, t8, t9",
            "body, called, t1, dispose x, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ABodyThatThrows",
            "try { for (await using x = ar(log, 'x'); ; ) { throw 'B'; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x, called, t1, caught B, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x, called, t1, caught B, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ADisposeThatRejects",
            "let n = 0; try { for (await using x = rejecting(log, 'x', 'D'); n < 1; n++) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x, called, t1, caught D, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x, called, t1, caught D, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // A dispose method that throws has no result to await (step 4.e.ii applies to a normal completion only).
            "ADisposeThatThrows",
            "let n = 0; try { for (await using x = throwing(log, 'x', 'D'); n < 1; n++) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x, caught D, after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x, caught D, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "TheBodyThrowsAndTheDisposeRejects",
            "try { for (await using x = rejecting(log, 'x', 'D'); ; ) { throw 'B'; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x, called, t1, caught SuppressedError(D over B), after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x, called, t1, caught SuppressedError(D over B), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The specification's answer: Node 24 crashes. The test's throw is the completion the resource is disposed with,
            // exactly as the body's is in the row above.
            "TheTestThrows",
            "try { for (await using x = ar(log, 'x'); (() => { throw 'T'; })(); ) { log.push('UNREACHED'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x, called, t1, caught T, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose x, called, t1, caught T, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The specification's answer: Node 24 crashes.
            "TheUpdateThrowsAndTheDisposeRejects",
            "try { for (await using x = rejecting(log, 'x', 'D'); ; (() => { throw 'U'; })()) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x, called, t1, caught SuppressedError(D over U), after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, dispose x, called, t1, caught SuppressedError(D over U), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The specification's answer: Node 24 crashes. Step 8 disposes what the declarators before the throw registered.
            "ALaterDeclaratorThrows",
            "try { for (await using a = ar(log, 'a'), b = (() => { throw 'I'; })(); ; ) { log.push('UNREACHED'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose a, called, t1, caught I, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "dispose a, called, t1, caught I, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // An await in the header's declarations does not dispose what the declarators before it registered.
            "ALaterDeclaratorAwaits",
            "let n = 0; for (await using a = ar(log, 'a'), b = await Promise.resolve(ar(log, 'b')); n < 1; n++) { log.push('body'); } log.push('after');",
            "called, t1, body, dispose b, t2, dispose a, t3, after, t4, resolved undefined, t5, t6, t7, t8, t9",
            "called, t1, body, dispose b, t2, dispose a, t3, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
        {
            "AnAwaitInTheTest",
            "let n = 0; for (await using x = ar(log, 'x'); await Promise.resolve(n < 2); n++) { log.push('body ' + n); } log.push('after');",
            "called, t1, body 0, t2, body 1, t3, dispose x, t4, after, t5, resolved undefined, t6, t7, t8, t9",
            "called, t1, body 0, t2, body 1, t3, dispose x, t4, after, t5, resolved {undefined,true}, t6, t7, t8, t9"
        },
        {
            "AnAwaitInTheUpdate",
            "let n = 0; for (await using x = ar(log, 'x'); n < 2; n = await Promise.resolve(n + 1)) { log.push('body ' + n); } log.push('after');",
            "body 0, called, t1, body 1, t2, dispose x, t3, after, t4, resolved undefined, t5, t6, t7, t8, t9",
            "body 0, called, t1, body 1, t2, dispose x, t3, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
        {
            "ANonBlockBody",
            "let n = 0; for (await using x = ar(log, 'x'); n < 2; n++) log.push('body ' + n); log.push('after');",
            "body 0, body 1, dispose x, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body 0, body 1, dispose x, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ANonBlockBodyThatAwaits",
            "let n = 0; for (await using x = ar(log, 'x'); n < 2; n++) log.push('body ' + n + ' ' + (await n)); log.push('after');",
            "called, t1, body 0 0, t2, body 1 1, dispose x, t3, after, t4, resolved undefined, t5, t6, t7, t8, t9",
            "called, t1, body 0 0, t2, body 1 1, dispose x, t3, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9"
        },
        {
            // DisposeResources step 5: a null resource owes one await of its own.
            "ANullResource",
            "let n = 0; for (await using x = null; n < 1; n++) { log.push('body'); } log.push('after');",
            "body, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9",
            "body, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // The body block disposes its own resource as each iteration ends, and the header's once the loop has.
            "ABodyBlocksOwnResourceFirst",
            "let n = 0; for (await using x = ar(log, 'x'); n < 1; n++) { await using y = ar(log, 'y'); log.push('body'); } log.push('after');",
            "body, dispose y, called, t1, dispose x, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "body, dispose y, called, t1, dispose x, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Resuming at the dispose does not run the enclosing loop's test again.
            "InsideAWhileLoopDoesNotRunItsTestAgain",
            "let i = 0; while (log.push('test ' + i), i < 2) { for (await using x = ar(log, 'x' + i); ; ) { break; } i++; } log.push('after');",
            "test 0, dispose x0, called, t1, test 1, dispose x1, t2, test 2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "test 0, dispose x0, called, t1, test 1, dispose x1, t2, test 2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // The outer loop is parked while the inner loop's dispose suspends, and its own dispose comes once it ends.
            "NestedLoops",
            "let i = 0; for (await using a = ar(log, 'a'); i < 2; i++) { for (await using b = later(log, 'b' + i); ; ) { log.push('inner ' + i); break; } log.push('between ' + i); } log.push('after');",
            "inner 0, dispose b0, called, t1, disposed b0, t2, between 0, inner 1, dispose b1, t3, disposed b1, t4, between 1, dispose a, t5, after, t6, resolved undefined, t7, t8, t9",
            "inner 0, dispose b0, called, t1, disposed b0, t2, between 0, inner 1, dispose b1, t3, disposed b1, t4, between 1, dispose a, t5, after, t6, resolved {undefined,true}, t7, t8, t9"
        },
        {
            // The inner loop's rejection is the throw the outer loop disposes with.
            "NestedLoopsWhoseDisposesBothReject",
            "try { for (await using a = rejecting(log, 'a', 'A'); ; ) { for (await using b = rejecting(log, 'b', 'B'); ; ) { break; } } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose b, called, t1, dispose a, t2, caught SuppressedError(A over B), after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "dispose b, called, t1, dispose a, t2, caught SuppressedError(A over B), after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // The specification's answer: Node 24 crashes. A closure made before an await reads the binding the loop
            // goes on with.
            "AClosureSeesTheSameBindingAcrossAnAwait",
            "let n = 0; let get; for (await using x = ar(log, 'x'); n < 1; n++) { get = () => x; await null; log.push('same ' + (get() === x)); } log.push('after');",
            "called, t1, same true, dispose x, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9",
            "called, t1, same true, dispose x, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> GeneratorFromTheStart => new()
    {
        {
            "AYieldAfterTheLoop",
            "let n = 0; for (await using x = ar(log, 'x'); n < 1; n++) { log.push('body'); } yield 1; log.push('UNREACHED');",
            "it.next()",
            "body, dispose x, called, t1, t2, t3, resolved {1,false}, t4, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> GeneratorAfterTheFirstYield => new()
    {
        {
            // The yield does not end the loop, so nothing is disposed.
            "NextAtAYieldInTheBody",
            "let n = 0; for (await using x = ar(log, 'x'); n < 2; n++) { yield n; log.push('resumed ' + n); } log.push('after');",
            "it.next()",
            "resumed 0, called, t1, t2, resolved {1,false}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ASyncUsingNextAtAYieldInTheBody",
            "let n = 0; for (using x = sr(log, 'x'); n < 2; n++) { yield n; log.push('resumed ' + n); } log.push('after');",
            "it.next()",
            "resumed 0, called, t1, t2, resolved {1,false}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "NextAtTheLastYieldInTheBody",
            "let n = 0; for (await using x = ar(log, 'x'); n < 1; n++) { yield n; log.push('resumed ' + n); } log.push('after');",
            "it.next()",
            "resumed 0, dispose x, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // return() is awaited at the yield (sebastienros/jint#4315), then its return completion leaves the loop,
            // which awaits its dispose before the generator completes.
            "ReturnAtAYieldInTheBody",
            "for (await using x = ar(log, 'x'); ; ) { yield 1; log.push('UNREACHED'); }",
            "it.return(5)",
            "called, t1, dispose x, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInANonBlockBody",
            "for (await using x = ar(log, 'x'); ; ) log.push('got ' + (yield 1));",
            "it.return(5)",
            "called, t1, dispose x, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInTheTest",
            "let n = 0; for (await using x = ar(log, 'x'); (yield n) < 2; n++) { log.push('body ' + n); } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, dispose x, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInTheUpdate",
            "let n = 0; for (await using x = ar(log, 'x'); n < 2; n = yield n) { log.push('body ' + n); } log.push('UNREACHED');",
            "it.return(5)",
            "body 0, called, t1, dispose x, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // A yield that is the whole update.
            "ReturnAtABareYieldInTheUpdate",
            "let n = 0; for (await using x = ar(log, 'x'); n < 2; yield n++) { log.push('body ' + n); } log.push('UNREACHED');",
            "it.return(5)",
            "body 0, called, t1, dispose x, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 8 with a return completion: what the declarators before the yield registered is disposed.
            "ReturnAtAYieldInALaterDeclarator",
            "for (await using a = ar(log, 'a'), b = (yield 1, ar(log, 'b')); ; ) { log.push('UNREACHED'); }",
            "it.return(5)",
            "called, t1, dispose a, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ReturnAtAYieldInALaterDeclaratorWhenTheDisposeRejectsCaught",
            "try { for (await using a = rejecting(log, 'a', 'D'), b = (yield 1, ar(log, 'b')); ; ) { log.push('UNREACHED'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose a, t2, caught D, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInALaterDeclarator",
            "for (await using a = ar(log, 'a'), b = (yield 1, ar(log, 'b')); ; ) { log.push('UNREACHED'); }",
            "it.throw('E')",
            "dispose a, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "NextAtAYieldInANestedLoop",
            "let i = 0; for (await using a = ar(log, 'a'); i < 2; i++) { for (await using b = ar(log, 'b' + i); ; ) { yield i; log.push('inner ' + i); break; } log.push('between ' + i); } log.push('after');",
            "it.next()",
            "inner 0, dispose b0, called, t1, between 0, t2, t3, resolved {1,false}, t4, t5, t6, t7, t8, t9"
        },
        {
            // The return completion leaves both loops, the inner one disposing first.
            "ReturnAtAYieldInANestedLoop",
            "for (await using a = ar(log, 'a'); ; ) { for (await using b = later(log, 'b'); ; ) { yield 1; } }",
            "it.return(5)",
            "called, t1, dispose b, t2, disposed b, t3, dispose a, t4, t5, resolved {5,true}, t6, t7, t8, t9"
        },
        {
            // A dispose that rejects replaces the return completion, and a catch around the loop sees it.
            "ReturnAtAYieldWhenTheDisposeRejectsCaught",
            "try { for (await using x = rejecting(log, 'x', 'D'); ; ) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, dispose x, t2, caught D, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInTheBody",
            "for (await using x = ar(log, 'x'); ; ) { yield 1; log.push('UNREACHED'); }",
            "it.throw('E')",
            "dispose x, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldInTheUpdate",
            "let n = 0; for (await using x = ar(log, 'x'); n < 2; n = yield n) { log.push('body ' + n); } log.push('UNREACHED');",
            "it.throw('E')",
            "body 0, dispose x, called, t1, t2, rejected E, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ThrowAtAYieldWhenTheDisposeRejects",
            "try { for (await using x = rejecting(log, 'x', 'D'); ; ) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')",
            "dispose x, called, t1, caught SuppressedError(D over E), after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
    };

    /// <summary>A sync function's body, and its log: what it returns is logged after the body's own entries.</summary>
    public static TestCases<string, string, string> InASyncFunction => new()
    {
        {
            // The issue's third example: the dispose's throw replaces the loop's normal completion.
            "TheIssuesThrowingDispose",
            "let n = 0; try { for (using x = syncThrowing(log, 'x', 'D'); n < 1; n++) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x, caught D, after"
        },
        {
            "TheBodyThrowsAndTheDisposeThrows",
            "try { for (using x = syncThrowing(log, 'x', 'D'); ; ) { throw 'B'; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x, caught SuppressedError(D over B), after"
        },
        {
            // The specification's answer: Node 24 disposes the resource twice.
            "ANonBlockBodyThrowsAndTheDisposeThrows",
            "try { for (using x = syncThrowing(log, 'x', 'D'); ; ) (() => { throw 'B'; })(); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x, caught SuppressedError(D over B), after"
        },
        {
            // The specification's answer: Node 24 disposes the resource twice.
            "TheTestThrows",
            "try { for (using x = sr(log, 'x'); (() => { throw 'T'; })(); ) { log.push('UNREACHED'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x, caught T, after"
        },
        {
            // The specification's answer: Node 24 disposes the resource twice.
            "TheTestThrowsAndTheDisposeThrows",
            "try { for (using x = syncThrowing(log, 'x', 'D'); (() => { throw 'T'; })(); ) { log.push('UNREACHED'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x, caught SuppressedError(D over T), after"
        },
        {
            // The specification's answer: Node 24 disposes the resource twice.
            "TheUpdateThrowsAndTheDisposeThrows",
            "try { for (using x = syncThrowing(log, 'x', 'D'); ; (() => { throw 'U'; })()) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "body, dispose x, caught SuppressedError(D over U), after"
        },
        {
            "ALaterDeclaratorThrowsAndTheDisposeThrows",
            "try { for (using a = syncThrowing(log, 'a', 'D'), b = (() => { throw 'I'; })(); ; ) { log.push('UNREACHED'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose a, caught SuppressedError(D over I), after"
        },
        {
            "AReturn",
            "for (using x = sr(log, 'x'); ; ) { log.push('body'); return 5; }",
            "body, dispose x, returned 5"
        },
        {
            "AReturnAndTheDisposeThrows",
            "try { (() => { for (using x = syncThrowing(log, 'x', 'D'); ; ) { return 5; } })(); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x, caught D, after"
        },
        {
            "ABreakAndTheDisposeThrows",
            "try { for (using x = syncThrowing(log, 'x', 'D'); ; ) { break; } log.push('UNREACHED'); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose x, caught D, after"
        },
        {
            "TwoThrowingDisposes",
            "try { for (using a = syncThrowing(log, 'a', 'A'), b = syncThrowing(log, 'b', 'B'); ; ) { break; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "dispose b, dispose a, caught SuppressedError(A over B), after"
        },
        {
            "ANestedLoopDisposesOncePerEntry",
            "for (let i = 0; i < 2; i++) { for (using x = sr(log, 'x' + i); ; ) { log.push('body ' + i); break; } } log.push('after');",
            "body 0, dispose x0, body 1, dispose x1, after"
        },
    };

    /// <summary>A sync generator's body, the script that drives it, and the log.</summary>
    public static TestCases<string, string, string, string> InASyncGenerator => new()
    {
        {
            // The issue's generator example: the yield does not end the loop.
            "TheIssuesGenerator",
            "let n = 0; for (using x = sr(log, 'x'); n < 2; n++) { yield; log.push('body ' + n); } log.push('after');",
            "it.next(); log.push('step'); it.next(); it.next();",
            "step, body 0, body 1, dispose x, after"
        },
        {
            "AYieldInTheTest",
            "let n = 0; for (using x = sr(log, 'x'); (yield n) < 2; n++) { log.push('body ' + n); } log.push('after');",
            "it.next(); log.push('step'); it.next(0); it.next(1); it.next(2);",
            "step, body 0, body 1, dispose x, after"
        },
        {
            "AYieldInTheUpdate",
            "let n = 0; for (using x = sr(log, 'x'); n < 2; n = yield n) { log.push('body ' + n); } log.push('after');",
            "it.next(); log.push('step'); it.next(1); it.next(2);",
            "body 0, step, body 1, dispose x, after"
        },
        {
            "AYieldInALaterDeclarator",
            "let n = 0; for (using a = sr(log, 'a'), b = (yield 1, sr(log, 'b')); n < 1; n++) { log.push('body'); } log.push('after');",
            "it.next(); log.push('step'); it.next();",
            "step, body, dispose b, dispose a, after"
        },
        {
            "AYieldInANonBlockBody",
            "let n = 0; for (using x = sr(log, 'x'); n < 2; n++) log.push('body ' + (yield n)); log.push('after');",
            "it.next(); log.push('step'); it.next('a'); it.next('b');",
            "step, body a, body b, dispose x, after"
        },
        {
            "ReturnAtAYieldInTheBody",
            "for (using x = sr(log, 'x'); ; ) { yield 1; log.push('UNREACHED'); }",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose x, {5,true}"
        },
        {
            "ReturnAtAYieldInTheTest",
            "let n = 0; for (using x = sr(log, 'x'); (yield n) < 2; n++) { log.push('body ' + n); } log.push('UNREACHED');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose x, {5,true}"
        },
        {
            "ReturnAtAYieldInANonBlockBody",
            "for (using x = sr(log, 'x'); ; ) log.push('got ' + (yield 1));",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose x, {5,true}"
        },
        {
            "ReturnAtAYieldInTheUpdate",
            "let n = 0; for (using x = sr(log, 'x'); n < 2; n = yield n) { log.push('body ' + n); } log.push('UNREACHED');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "body 0, step, dispose x, {5,true}"
        },
        {
            // A yield that is the whole update.
            "ReturnAtABareYieldInTheUpdate",
            "let n = 0; for (using x = sr(log, 'x'); n < 2; yield n++) { log.push('body ' + n); } log.push('UNREACHED');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "body 0, step, dispose x, {5,true}"
        },
        {
            // Step 8 with a return completion: what the declarators before the yield registered is disposed.
            "ReturnAtAYieldInALaterDeclarator",
            "for (using a = sr(log, 'a'), b = (yield 1, sr(log, 'b')); ; ) { log.push('UNREACHED'); }",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose a, {5,true}"
        },
        {
            "ReturnAtAYieldInALaterDeclaratorAndTheDisposeThrows",
            "try { for (using a = syncThrowing(log, 'a', 'D'), b = (yield 1, sr(log, 'b')); ; ) { log.push('UNREACHED'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose a, caught D, after, {undefined,true}"
        },
        {
            "ThrowAtAYieldInALaterDeclarator",
            "for (using a = sr(log, 'a'), b = (yield 1, sr(log, 'b')); ; ) { log.push('UNREACHED'); }",
            "it.next(); log.push('step'); try { it.throw('E'); } catch (e) { log.push('threw ' + show(e)); }",
            "step, dispose a, threw E"
        },
        {
            // The dispose's throw replaces the return completion, so the catch around the loop sees it.
            "ReturnAtAYieldAndTheDisposeThrows",
            "try { for (using x = syncThrowing(log, 'x', 'D'); ; ) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next(); log.push('step'); log.push(fmt(it.return(5)));",
            "step, dispose x, caught D, after, {undefined,true}"
        },
        {
            "ThrowAtAYieldInTheBody",
            "for (using x = sr(log, 'x'); ; ) { yield 1; log.push('UNREACHED'); }",
            "it.next(); log.push('step'); try { it.throw('E'); } catch (e) { log.push('threw ' + show(e)); }",
            "step, dispose x, threw E"
        },
        {
            "ThrowAtAYieldAndTheDisposeThrows",
            "for (using x = syncThrowing(log, 'x', 'D'); ; ) { yield 1; }",
            "it.next(); log.push('step'); try { it.throw('E'); } catch (e) { log.push('threw ' + show(e)); }",
            "step, dispose x, threw SuppressedError(D over E)"
        },
        {
            "ThrowAtAYieldInTheUpdateAndTheDisposeThrows",
            "let n = 0; for (using x = syncThrowing(log, 'x', 'D'); n < 2; n = yield n) { log.push('body ' + n); }",
            "it.next(); log.push('step'); try { it.throw('E'); } catch (e) { log.push('threw ' + show(e)); }",
            "body 0, step, dispose x, threw SuppressedError(D over E)"
        },
    };

    [TestCaseSource(nameof(InBothKinds)), CancelAfter(10000)]
    public void AnAsyncLoopDisposesItsHeader(string name, string body, string asAFunction, string asAGenerator)
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

    [TestCaseSource(nameof(InASyncFunction)), CancelAfter(10000)]
    public void ALoopInASyncFunctionDisposesItsHeader(string name, string body, string expected)
    {
        Run("""
            (() => {
              const log = [];
              const r = (function () { BODY })();
              if (r !== undefined) {
                log.push('returned ' + show(r));
              }
              return log.join(', ');
            })();
            """.Replace("BODY", body)).Should().Be(expected, name);
    }

    [TestCaseSource(nameof(InASyncGenerator)), CancelAfter(10000)]
    public void ALoopInASyncGeneratorDisposesItsHeaderWhenItEnds(string name, string body, string driver, string expected)
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

    /// <summary>
    /// A module's top-level <c>await</c> suspends the module body as it suspends an async function, and so does the loop's
    /// dispose. The specification's answer: Node 24 never finishes this module.
    /// </summary>
    [Test, CancelAfter(10000)]
    public void ALoopAtTheTopLevelOfAModule()
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        engine.Modules.Add("main", """
            const log = [];
            Promise.resolve().then(() => log.push('tick'));
            let n = 0;
            for (await using x = { [Symbol.asyncDispose]() { log.push('dispose'); } }; n < 2; n++) { log.push('body ' + n); await null; }
            log.push('after');
            export const result = log.join(', ');
            """);

        engine.Modules.Import("main").Get("result").AsString().Should().Be("body 0, tick, body 1, dispose, after");
    }

    [Test]
    public void TheLoopKeepsItsCompletionValue()
    {
        var engine = new Engine();
        engine.Evaluate("let n = 0; for (using x = { [Symbol.dispose]() {} }; n < 2; n++) { n * 10; }").AsNumber().Should().Be(10);
    }

    [Test]
    public void AThrowingDisposeReachesTheHost()
    {
        var engine = new Engine();
        var thrown = Caught.Exception(() => engine.Execute("for (using x = { [Symbol.dispose]() { throw new Error('D'); } }; ; ) { break; }"));
        thrown.Should().BeOfType<JavaScriptException>().Which.Message.Should().Be("D");
    }

    /// <summary>
    /// A host exception or a constraint stops the script where it is, and neither is a completion the script can see:
    /// the header's resources are left as a block's are, rather than having script run on the way out.
    /// </summary>
    [TestCase("for (using x = { [Symbol.dispose]() { disposed.push('x'); } }; ; ) { STOP; }")]
    [TestCase("{ using x = { [Symbol.dispose]() { disposed.push('x'); } }; for (;;) { STOP; } }")]
    public void AHostExceptionLeavesTheResourcesUndisposed(string script)
    {
        var engine = new Engine();
        engine.SetValue("fail", new Action(() => throw new InvalidOperationException("host")));
        engine.Execute("var disposed = [];");
        Caught.Exception(() => engine.Execute(script.Replace("STOP", "fail()"))).Should().BeOfType<InvalidOperationException>();
        engine.Evaluate("disposed.length").AsNumber().Should().Be(0);
    }

    /// <inheritdoc cref="AHostExceptionLeavesTheResourcesUndisposed"/>
    /// <remarks>
    /// The dispose method is a host delegate, so the statement limit cannot stop a call to it the way it stops a script
    /// method at its first statement: the count is of calls made, not of statements run.
    /// </remarks>
    [TestCase("for (using x = { [Symbol.dispose]: dispose }; ; ) { n++; }")]
    [TestCase("{ using x = { [Symbol.dispose]: dispose }; for (;;) { n++; } }")]
    public void AConstraintLeavesTheResourcesUndisposed(string script)
    {
        var disposeCalls = 0;

        // The statement limit is what ends `for (;;)`. The execution-time limit is a wedge ceiling for a regression that
        // lets the loop run on, never an assertion.
        var engine = new Engine(options => options.LimitStatements(1000).LimitExecutionTime(TestBudgets.WedgeCeiling));
        engine.SetValue("dispose", new Action(() => disposeCalls++));
        engine.Execute("var n = 0;");

        Caught.Exception(() => engine.Execute(script)).Should().BeOfType<StatementsCountOverflowException>();
        disposeCalls.Should().Be(0);
    }

    private static string RunAsync(string script)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        return engine.Evaluate(AsyncBlockDisposalTests.Prelude + script).UnwrapIfPromise().AsString();
    }

    private static string Run(string script)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        return engine.Evaluate(AsyncBlockDisposalTests.Prelude + SyncHelpers + script).AsString();
    }
}
