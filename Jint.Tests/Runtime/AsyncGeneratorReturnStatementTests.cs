#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A <c>return expr;</c> statement in an async generator, timed against the microtask queue. The statement awaits its
/// operand itself (<see href="https://tc39.es/ecma262/#sec-return-statement-runtime-semantics-evaluation">ReturnStatement
/// evaluation</see>, step 3: <c>Set exprValue to ? Await(exprValue)</c>), so the return completion leaves the statement,
/// and reaches the enclosing <c>finally</c> blocks, a loop's iterator close and the generator's end, only once that
/// await has settled; a rejection is a throw completion of the statement, which a <c>try</c> around it catches.
/// <para>
/// Jint awaited the operand only after the body had finished, so the <c>finally</c> blocks ran during the very
/// <c>next()</c> call that reached the <c>return</c>, and a rejection skipped every <c>catch</c>
/// (sebastienros/jint#4332).
/// </para>
/// <para>
/// The logs use the ladder of <see cref="AsyncGeneratorReturnResumptionTests"/>, and every expected log is Node 24's.
/// </para>
/// </summary>
public class AsyncGeneratorReturnStatementTests
{
    public static TestCases<string, string, string, string> FromTheStart => new()
    {
        {
            // The issue's own example.
            "ResolvedPromiseInATryWithAFinally",
            "(async function* () { try { return Promise.resolve(5); } finally { log.push('finally'); } })()",
            "it.next()",
            "called, t1, finally, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            "RejectedPromiseCaughtAroundTheReturn",
            "(async function* () { try { return Promise.reject('X'); } catch (e) { log.push('caught ' + e); } finally { log.push('finally'); } log.push('after'); })()",
            "it.next()",
            "called, t1, caught X, finally, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7"
        },
        {
            // Caught, the rejection is just a throw: the generator carries on and the request is answered by its next yield.
            "RejectedPromiseCaughtAndTheGeneratorYields",
            "(async function* () { try { return Promise.reject('X'); } catch (e) { log.push('caught ' + e); } yield 2; log.push('UNREACHED'); })()",
            "it.next()",
            "called, t1, caught X, t2, t3, resolved {2,false}, t4, t5, t6, t7"
        },
        {
            "RejectedPromiseWithoutACatch",
            "(async function* () { try { return Promise.reject('X'); } finally { log.push('finally'); } })()",
            "it.next()",
            "called, t1, finally, t2, rejected X, t3, t4, t5, t6, t7"
        },
        {
            "NestedFinally",
            "(async function* () { try { try { return Promise.resolve(5); } finally { log.push('inner finally'); } } finally { log.push('outer finally'); } })()",
            "it.next()",
            "called, t1, inner finally, outer finally, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            "NestedFinallyThatAwaits",
            "(async function* () { try { try { return Promise.resolve(5); } finally { log.push('inner fin'); await null; log.push('inner fin 2'); } } finally { log.push('outer fin'); } })()",
            "it.next()",
            "called, t1, inner fin, t2, inner fin 2, outer fin, t3, resolved {5,true}, t4, t5, t6, t7"
        },
        {
            // PromiseResolve wraps a thenable, whose then runs in a job of its own: one microtask more.
            "Thenable",
            "(async function* () { try { return { then(r) { log.push('then called'); r(5); } }; } finally { log.push('finally'); } })()",
            "it.next()",
            "called, t1, then called, t2, finally, t3, resolved {5,true}, t4, t5, t6, t7"
        },
        {
            // A literal operand is awaited too, although Jint resolves it before the statement runs.
            "LiteralNonPromise",
            "(async function* () { try { return 5; } finally { log.push('finally'); } })()",
            "it.next()",
            "called, t1, finally, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            "ComputedNonPromise",
            "(async function* () { const v = 5; try { return v; } finally { log.push('finally'); } })()",
            "it.next()",
            "called, t1, finally, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            "LiteralAsTheWholeBody",
            "(async function* () { return 5; })()",
            "it.next()",
            "called, t1, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            // `return;` has no operand to await, so its finally runs inside the next() call.
            "WithoutAnOperand",
            "(async function* () { try { return; } finally { log.push('finally'); } })()",
            "it.next()",
            "finally, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7"
        },
        {
            // Two awaits: the operand's own, then the statement's.
            "ReturnAwait",
            "(async function* () { try { return await Promise.resolve(5); } finally { log.push('finally'); } })()",
            "it.next()",
            "called, t1, t2, finally, t3, resolved {5,true}, t4, t5, t6, t7"
        },
        {
            "TheOperandIsEvaluatedOnce",
            "(async function* () { try { return (log.push('operand'), Promise.resolve(5)); } finally { log.push('finally'); } })()",
            "it.next()",
            "operand, called, t1, finally, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            "InsideAForOfTheIteratorIsClosedAfterTheAwait",
            "(async function* () { for (const x of closable(log)) return Promise.resolve(5); })()",
            "it.next()",
            "called, t1, iter return, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            // The loop's next() is awaited, then the return, then the iterator's return() (AsyncIteratorClose).
            "InsideAForAwaitTheIteratorIsClosedAfterTheAwait",
            "(async function* () { const source = { [Symbol.asyncIterator]() { return { next() { return Promise.resolve({ value: 1, done: false }); }, return() { log.push('iter return'); return Promise.resolve({}); } }; } }; for await (const x of source) return Promise.resolve(5); })()",
            "it.next()",
            "called, t1, t2, iter return, t3, t4, resolved {5,true}, t5, t6, t7"
        },
        {
            "AUsingDeclarationIsDisposedAfterTheAwait",
            "(async function* () { using r = { [Symbol.dispose]() { log.push('dispose'); } }; return Promise.resolve(5); })()",
            "it.next()",
            "called, t1, dispose, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            // Resuming at the return does not run the loop's or the if's test again.
            "InsideAnIfInALoop",
            "(async function* () { let i = 0; while (log.push('test ' + i), i++ < 3) { if (log.push('if ' + i), i === 2) return Promise.resolve(i); } })()",
            "it.next()",
            "test 0, if 1, test 1, if 2, called, t1, t2, resolved {2,true}, t3, t4, t5, t6, t7"
        },
        {
            "InsideACatchBlock",
            "(async function* () { try { throw 'E'; } catch (e) { return Promise.resolve(e); } finally { log.push('finally'); } })()",
            "it.next()",
            "called, t1, finally, t2, resolved {E,true}, t3, t4, t5, t6, t7"
        },
        {
            "InsideAFinallyBlock",
            "(async function* () { try { log.push('try'); } finally { return Promise.resolve('F'); } })()",
            "it.next()",
            "try, called, t1, t2, resolved {F,true}, t3, t4, t5, t6, t7"
        },
        {
            "AFinallyReplacesThePendingReturn",
            "(async function* () { try { return Promise.resolve(5); } finally { log.push('finally'); return 'F'; } })()",
            "it.next()",
            "called, t1, finally, t2, t3, resolved {F,true}, t4, t5, t6, t7"
        },
        {
            "InsideASwitchCase",
            "(async function* () { switch (1) { case 1: try { return Promise.resolve(5); } finally { log.push('finally'); } } })()",
            "it.next()",
            "called, t1, finally, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            // PromiseResolve reads the constructor of a promise and throws, so the await fails before anything is
            // queued: the throw is the return statement's, and the catch around it sees it.
            "APromiseWhoseConstructorGetterThrows",
            "(async function* () { try { const p = Promise.resolve(42); Object.defineProperty(p, 'constructor', { get() { log.push('get constructor'); throw new Error('broken'); } }); return p; } catch (e) { log.push('caught ' + e.message); } finally { log.push('finally'); } })()",
            "it.next()",
            "get constructor, caught broken, finally, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7"
        },
        {
            "AnAsyncGeneratorMethod",
            "(new (class { async *m() { try { return Promise.resolve(5); } finally { log.push('finally'); } } })()).m()",
            "it.next()",
            "called, t1, finally, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            // A function or an arrow inside the generator returns as any function does: nothing awaits its operand.
            "ANestedFunctionsReturnIsNotAwaited",
            "(async function* () { function inner() { return Promise.resolve(1); } const arrow = () => { return Promise.resolve(2); }; log.push('inner ' + (inner() instanceof Promise) + ' ' + (arrow() instanceof Promise)); try { return Promise.resolve(5); } finally { log.push('finally'); } })()",
            "it.next()",
            "inner true true, called, t1, finally, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            // The generator is executing while the return awaits, so a second next() queues behind the first.
            "ASecondNextQueuesBehindTheAwait",
            "(async function* () { try { return Promise.resolve(5); } finally { log.push('finally'); } })()",
            "(it.next().then(r => log.push('first ' + fmt(r))), it.next())",
            "called, t1, finally, t2, first {5,true}, resolved {undefined,true}, t3, t4, t5, t6, t7"
        },
    };

    public static TestCases<string, string, string, string> AfterTheFirstYield => new()
    {
        {
            "ResolvedPromiseAfterAYield",
            "(async function* () { yield 1; try { return Promise.resolve(5); } finally { log.push('finally'); } })()",
            "it.next()",
            "called, t1, finally, t2, resolved {5,true}, t3, t4, t5, t6, t7"
        },
        {
            // The yield's value is the operand, and the statement awaits it.
            "YieldOperandResumedByNext",
            "(async function* () { try { return yield 1; } finally { log.push('finally'); } })()",
            "it.next(Promise.resolve(7))",
            "called, t1, finally, t2, resolved {7,true}, t3, t4, t5, t6, t7"
        },
        {
            // return() is awaited at the yield, and its return completion leaves the statement before the statement's
            // own await (sebastienros/jint#4315).
            "YieldOperandResumedByReturn",
            "(async function* () { try { return yield 1; } finally { log.push('finally'); } })()",
            "it.return(Promise.resolve(42))",
            "called, t1, finally, t2, resolved {42,true}, t3, t4, t5, t6, t7"
        },
    };

    [TestCaseSource(nameof(FromTheStart)), CancelAfter(10000)]
    public void AReturnStatementReachedByTheFirstNext(string name, string generator, string resume, string expected)
    {
        AsyncGeneratorReturnResumptionTests.Probe(generator, resume, started: false).Should().Be(expected, name);
    }

    [TestCaseSource(nameof(AfterTheFirstYield)), CancelAfter(10000)]
    public void AReturnStatementReachedAfterAYield(string name, string generator, string resume, string expected)
    {
        AsyncGeneratorReturnResumptionTests.Probe(generator, resume, started: true).Should().Be(expected, name);
    }

    [TestCase(true), TestCase(false), CancelAfter(10000)]
    public void AReturnStatementAwaitsItsOperandWhateverThePreparation(bool staticAnalysis)
    {
        // The static analysis resolves `return <literal>;` to a constant ahead of time, and a parse-only preparation
        // leaves the function's own analysis to its first call; the statement awaits its operand either way, and a
        // nested function's return in the same tree still does not.
        const string Script = """
            (() => {
              const results = [];
              const run = make => probe(make, it => it.next(), false).then(r => { results.push(r); });
              return run(log => (async function* () { try { return 5; } finally { log.push('finally'); } })())
                .then(() => run(log => (async function* () { function f() { return 1; } try { return f(); } finally { log.push('finally'); } })()))
                .then(() => results.join(' | '));
            })();
            """;

        const string Expected = "called, t1, finally, t2, resolved {5,true}, t3, t4, t5, t6, t7 | called, t1, finally, t2, resolved {1,true}, t3, t4, t5, t6, t7";
        var prepared = Engine.PrepareScript(
            AsyncGeneratorReturnResumptionTests.Prelude + Script,
            options: new ScriptPreparationOptions { StaticAnalysis = staticAnalysis });

        // Twice on one engine, then on a second engine sharing the prepared tree.
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        engine.Evaluate(prepared).UnwrapIfPromise().AsString().Should().Be(Expected);
        engine.Evaluate(prepared).UnwrapIfPromise().AsString().Should().Be(Expected);
        var second = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        second.Evaluate(prepared).UnwrapIfPromise().AsString().Should().Be(Expected);
    }
}
