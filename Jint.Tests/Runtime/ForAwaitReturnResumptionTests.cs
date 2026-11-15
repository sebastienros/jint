#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A <c>for await</c> loop in an async generator that is left because <c>it.return(v)</c> resumed a <c>yield</c> inside
/// it. The resumption is a return completion
/// (<see href="https://tc39.es/ecma262/#sec-asyncgeneratorunwrapyieldresumption">AsyncGeneratorUnwrapYieldResumption</see>),
/// which is the loop's abrupt status, and
/// <see href="https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset">ForIn/OfBodyEvaluation</see>
/// hands it to <see href="https://tc39.es/ecma262/#sec-asynciteratorclose">AsyncIteratorClose</see>: step 4.d awaits what
/// the iterator's <c>return()</c> answered, a rejection or a throw replaces the return completion (step 6), and a settled
/// value that is not an Object is a <c>TypeError</c> (step 7). Only then does the return go on, through any enclosing
/// <c>finally</c>, and settle the request.
/// <para>
/// Jint closed that loop with the synchronous IteratorClose, braces or not, in the <c>ReturnRequested</c> exit and on the
/// <c>GeneratorReturnException</c> path, so it awaited nothing: the request settled a job early (a thenable's two jobs
/// early), a rejected <c>return()</c> was dropped and the request answered <c>{5,true}</c>, and a promise of a non-object
/// passed (sebastienros/jint#4391). A <c>return</c> statement in the body, a <c>throw()</c> resumption and a
/// <c>break</c> already took AsyncIteratorClose, and are here as controls.
/// </para>
/// <para>
/// Each log is the generator's own entries interleaved with a ladder of microtasks <c>t1</c>, <c>t2</c>, … started just
/// before the request; <c>called</c> is the statement after it. The generator is first run to its first <c>yield</c>.
/// Every expected log is Node 26's.
/// </para>
/// </summary>
public class ForAwaitReturnResumptionTests
{
    private const string Prelude = """
        function ladder(log, n) {
          let p = Promise.resolve();
          for (let i = 1; i <= n; i++) {
            const k = i;
            p = p.then(() => { log.push('t' + k); });
          }
          return p;
        }
        // An error by its name: a TypeError's message is the engine's own.
        function show(v) {
          return v instanceof Error ? v.name : String(v);
        }
        function fmt(r) {
          return r !== null && typeof r === 'object' && 'done' in r ? '{' + show(r.value) + ',' + r.done + '}' : show(r);
        }
        function sr(log, name) {
          return { [Symbol.dispose]() { log.push('dispose ' + name); } };
        }
        function ar(log, name) {
          return { [Symbol.asyncDispose]() { log.push('dispose ' + name); } };
        }
        // An iterator that never runs out, handed out as an async iterable or, with sync, as a sync one that for await
        // drives through CreateAsyncFromSyncIterator. Its return() is logged and answers what `answer` returns; without an
        // answer it has no return method. `items` supplies the values, which default to 1, 2, 3, ...
        function source(log, answer, options) {
          options = options || {};
          const tag = options.tag || '';
          const items = options.items;
          let n = 0;
          const iterator = {
            next() {
              n++;
              const result = { value: items ? items[n - 1] : n, done: false };
              return options.sync ? result : Promise.resolve(result);
            }
          };
          if (answer !== undefined) {
            iterator.return = function () { log.push('return' + tag); return answer(); };
          }
          return options.sync ? { [Symbol.iterator]() { return iterator; } } : { [Symbol.asyncIterator]() { return iterator; } };
        }
        const OBJECT = () => ({});
        const PROMISE = () => Promise.resolve({});
        const REJECTS = () => Promise.reject('R');
        const THROWS = () => { throw 'T'; };
        const NUMBER = () => 1;
        const PROMISE_OF_NUMBER = () => Promise.resolve(1);
        const THENABLE = () => ({ then(resolve) { resolve({}); } });
        // make builds the generator, which is first run to its first yield; request makes the request whose answer is
        // logged. The ladder is started just before it.
        async function probe(make, request) {
          const log = [];
          const it = make(log);
          await it.next();
          const done = ladder(log, 9);
          request(it, log).then(r => log.push('resolved ' + fmt(r)), e => log.push('rejected ' + show(e)));
          log.push('called');
          return done.then(() => log.join(', '));
        }

        """;

    public static TestCases<string, string, string, string> WhatReturnAnswers => new()
    {
        {
            "AnObject",
            "for await (const x of source(log, OBJECT)) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AnObjectBraceless",
            "for await (const x of source(log, OBJECT)) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "APromise",
            "for await (const x of source(log, PROMISE)) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "APromiseBraceless",
            "for await (const x of source(log, PROMISE)) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 6: the rejection replaces the return completion. Jint dropped it and answered {5,true}.
            "ARejectedPromise",
            "for await (const x of source(log, REJECTS)) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, rejected R, t4, t5, t6, t7, t8, t9"
        },
        {
            "ARejectedPromiseBraceless",
            "for await (const x of source(log, REJECTS)) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, rejected R, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 4.c is abrupt, so there is nothing to await, and step 6 replaces the return at once: unchanged.
            "AThrow",
            "for await (const x of source(log, THROWS)) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, rejected T, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "AThrowBraceless",
            "for await (const x of source(log, THROWS)) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, rejected T, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 7 runs after the await.
            "ANonObject",
            "for await (const x of source(log, NUMBER)) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, rejected TypeError, t4, t5, t6, t7, t8, t9"
        },
        {
            "ANonObjectBraceless",
            "for await (const x of source(log, NUMBER)) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, rejected TypeError, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 7 checks the settled value, so this is a TypeError too, which the synchronous close could never see.
            "APromiseOfANonObject",
            "for await (const x of source(log, PROMISE_OF_NUMBER)) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, rejected TypeError, t4, t5, t6, t7, t8, t9"
        },
        {
            "APromiseOfANonObjectBraceless",
            "for await (const x of source(log, PROMISE_OF_NUMBER)) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, rejected TypeError, t4, t5, t6, t7, t8, t9"
        },
        {
            // A thenable costs its own two jobs, and its then is called.
            "AThenable",
            "for await (const x of source(log, THENABLE)) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            "AThenableBraceless",
            "for await (const x of source(log, THENABLE)) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            // Step 4.b: no return method, nothing to await: unchanged.
            "NoReturnMethod",
            "for await (const x of source(log, undefined)) { yield 1; } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, t2, resolved {5,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "NoReturnMethodBraceless",
            "for await (const x of source(log, undefined)) yield 1; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, t2, resolved {5,true}, t3, t4, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> HowTheReturnReachesTheLoop => new()
    {
        {
            "AYieldInsideAnExpression",
            "for await (const x of source(log, OBJECT)) { log.push('got ' + (yield 1)); } log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AYieldInsideAnExpressionBraceless",
            "for await (const x of source(log, OBJECT)) log.push('got ' + (yield 1)); log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // The value is awaited at the yield, before the close (sebastienros/jint#4315).
            "AReturnOfAPromise",
            "for await (const x of source(log, OBJECT)) { yield 1; }",
            "it.return(Promise.resolve(5))",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AVarHead",
            "for await (var x of source(log, OBJECT)) { yield 1; }",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AnAssignmentHead",
            "let x; for await (x of source(log, OBJECT)) { yield 1; }",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ADestructuringHead",
            "for await (const [a] of source(log, OBJECT, { items: [[1], [2]] })) { yield a; }",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Step 8.i: the binding's return completion closes the iterator as the body's does.
            "AYieldInTheHeadsPattern",
            "for await (const [a = yield 1] of source(log, OBJECT, { items: [[], []] })) { log.push('UNREACHED'); }",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AYieldInTheHeadsPatternWhenReturnRejects",
            "for await (const [a = yield 1] of source(log, REJECTS, { items: [[], []] })) { log.push('UNREACHED'); }",
            "it.return(5)",
            "called, t1, return, t2, t3, rejected R, t4, t5, t6, t7, t8, t9"
        },
        {
            // The inner generator is returned first, then the loop's iterator is closed.
            "AYieldStar",
            "for await (const x of source(log, OBJECT)) { yield* (async function* () { try { yield 1; } finally { log.push('inner finally'); } })(); }",
            "it.return(5)",
            "called, t1, t2, inner finally, t3, return, t4, t5, resolved {5,true}, t6, t7, t8, t9"
        },
        {
            // A return statement in the body always took AsyncIteratorClose; a return() resumption now matches it.
            "AReturnStatement",
            "for await (const x of source(log, OBJECT)) { yield 1; return 5; }",
            "it.next()",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AReturnStatementWhenReturnRejects",
            "for await (const x of source(log, REJECTS)) { yield 1; return 5; }",
            "it.next()",
            "called, t1, return, t2, t3, rejected R, t4, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> WithAUsingHead => new()
    {
        {
            // The issue's reproduction: an array, whose %AsyncFromSyncIteratorPrototype%.return answers a promise.
            "TheIssuesExample",
            "for await (using x of [sr(log, 'x1'), sr(log, 'x2')]) yield 1;",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "TheIssuesExampleBraced",
            "for await (using x of [sr(log, 'x1'), sr(log, 'x2')]) { yield 1; }",
            "it.return(5)",
            "called, t1, dispose x1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AUsingHead",
            "for await (using x of source(log, OBJECT, { items: [sr(log, 'x1'), sr(log, 'x2')] })) { yield 1; }",
            "it.return(5)",
            "called, t1, dispose x1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AUsingHeadBraceless",
            "for await (using x of source(log, OBJECT, { items: [sr(log, 'x1'), sr(log, 'x2')] })) yield 1;",
            "it.return(5)",
            "called, t1, dispose x1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AUsingHeadWhenReturnRejects",
            "for await (using x of source(log, REJECTS, { items: [sr(log, 'x1'), sr(log, 'x2')] })) { yield 1; }",
            "it.return(5)",
            "called, t1, dispose x1, return, t2, t3, rejected R, t4, t5, t6, t7, t8, t9"
        },
        {
            "AUsingHeadBracelessWhenReturnRejects",
            "for await (using x of source(log, REJECTS, { items: [sr(log, 'x1'), sr(log, 'x2')] })) yield 1;",
            "it.return(5)",
            "called, t1, dispose x1, return, t2, t3, rejected R, t4, t5, t6, t7, t8, t9"
        },
        {
            "AUsingHeadWhenReturnThrows",
            "for await (using x of source(log, THROWS, { items: [sr(log, 'x1'), sr(log, 'x2')] })) { yield 1; }",
            "it.return(5)",
            "called, t1, dispose x1, return, t2, rejected T, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "AUsingHeadBracelessWhenReturnAnswersANonObject",
            "for await (using x of source(log, NUMBER, { items: [sr(log, 'x1'), sr(log, 'x2')] })) yield 1;",
            "it.return(5)",
            "called, t1, dispose x1, return, t2, t3, rejected TypeError, t4, t5, t6, t7, t8, t9"
        },
        {
            // The dispose suspends, and the resume after it already closed through AsyncIteratorClose: unchanged.
            "AnAwaitUsingHead",
            "for await (await using x of source(log, OBJECT, { items: [ar(log, 'x1'), ar(log, 'x2')] })) { yield 1; }",
            "it.return(5)",
            "called, t1, dispose x1, t2, return, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            "AnAwaitUsingHeadBraceless",
            "for await (await using x of source(log, OBJECT, { items: [ar(log, 'x1'), ar(log, 'x2')] })) yield 1;",
            "it.return(5)",
            "called, t1, dispose x1, t2, return, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            "AnAwaitUsingHeadWhenReturnRejects",
            "for await (await using x of source(log, REJECTS, { items: [ar(log, 'x1'), ar(log, 'x2')] })) { yield 1; }",
            "it.return(5)",
            "called, t1, dispose x1, t2, return, t3, t4, rejected R, t5, t6, t7, t8, t9"
        },
        {
            "AnAwaitUsingHeadBracelessWhenReturnRejects",
            "for await (await using x of source(log, REJECTS, { items: [ar(log, 'x1'), ar(log, 'x2')] })) yield 1;",
            "it.return(5)",
            "called, t1, dispose x1, t2, return, t3, t4, rejected R, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> NestedLoops => new()
    {
        {
            // Each loop's close is awaited before the next one starts.
            "TwoForAwaitLoops",
            "for await (const a of source(log, OBJECT, { tag: ' outer' })) { for await (const b of source(log, OBJECT, { tag: ' inner' })) { yield 1; } }",
            "it.return(5)",
            "called, t1, return inner, t2, return outer, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            "TwoForAwaitLoopsBraceless",
            "for await (const a of source(log, OBJECT, { tag: ' outer' })) for await (const b of source(log, OBJECT, { tag: ' inner' })) yield 1;",
            "it.return(5)",
            "called, t1, return inner, t2, return outer, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            // The inner rejection is a throw completion for the outer loop, whose close is awaited and then discarded (step 5).
            "TwoForAwaitLoopsWhenTheInnerReturnRejects",
            "for await (const a of source(log, OBJECT, { tag: ' outer' })) { for await (const b of source(log, REJECTS, { tag: ' inner' })) { yield 1; } }",
            "it.return(5)",
            "called, t1, return inner, t2, return outer, t3, t4, rejected R, t5, t6, t7, t8, t9"
        },
        {
            "InsideAForOfLoop",
            "for (const a of [1, 2]) { for await (const b of source(log, OBJECT)) { yield 1; } }",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "InsideALabelledBlock",
            "outer: { for await (const b of source(log, OBJECT)) { yield 1; } }",
            "it.return(5)",
            "called, t1, return, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> AnEnclosingTry => new()
    {
        {
            "AFinally",
            "try { for await (const x of source(log, OBJECT)) { yield 1; } } finally { log.push('finally'); }",
            "it.return(5)",
            "called, t1, return, t2, finally, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AFinallyBraceless",
            "try { for await (const x of source(log, OBJECT)) yield 1; } finally { log.push('finally'); }",
            "it.return(5)",
            "called, t1, return, t2, finally, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "AFinallyWhenReturnRejects",
            "try { for await (const x of source(log, REJECTS)) { yield 1; } } finally { log.push('finally'); }",
            "it.return(5)",
            "called, t1, return, t2, finally, t3, rejected R, t4, t5, t6, t7, t8, t9"
        },
        {
            "ACatchWhenReturnRejects",
            "try { for await (const x of source(log, REJECTS)) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, return, t2, caught R, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ACatchBracelessWhenReturnRejects",
            "try { for await (const x of source(log, REJECTS)) yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, return, t2, caught R, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            "ACatchWhenReturnThrows",
            "try { for await (const x of source(log, THROWS)) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, return, caught T, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ACatchWhenReturnAnswersANonObject",
            "try { for await (const x of source(log, NUMBER)) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)",
            "called, t1, return, t2, caught TypeError, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // The rejection replaced the return, so the generator goes on to its next yield.
            "ACatchWhenReturnRejectsThenAYield",
            "try { for await (const x of source(log, REJECTS)) { yield 1; } } catch (e) { log.push('caught ' + show(e)); } yield 2; log.push('UNREACHED');",
            "it.return(5)",
            "called, t1, return, t2, caught R, t3, t4, resolved {2,false}, t5, t6, t7, t8, t9"
        },
        {
            "AFinallyThatYields",
            "try { for await (const x of source(log, OBJECT)) { yield 1; } } finally { log.push('finally'); yield 2; log.push('after yield'); }",
            "it.return(5).then(r => { log.push('first ' + fmt(r)); return it.next(); })",
            "called, t1, return, t2, finally, t3, t4, first {2,false}, after yield, t5, t6, t7, resolved {5,true}, t8, t9"
        },
        {
            "AFinallyThatYieldsBraceless",
            "try { for await (const x of source(log, OBJECT)) yield 1; } finally { log.push('finally'); yield 2; log.push('after yield'); }",
            "it.return(5).then(r => { log.push('first ' + fmt(r)); return it.next(); })",
            "called, t1, return, t2, finally, t3, t4, first {2,false}, after yield, t5, t6, t7, resolved {5,true}, t8, t9"
        },
        {
            "AFinallyThatYieldsAndIsSentAValue",
            "try { for await (const x of source(log, OBJECT)) { yield 1; } } finally { log.push('finally'); log.push('sent ' + (yield 2)); }",
            "it.return(5).then(r => { log.push('first ' + fmt(r)); return it.next('v'); })",
            "called, t1, return, t2, finally, t3, t4, first {2,false}, sent v, t5, t6, t7, resolved {5,true}, t8, t9"
        },
        {
            "AFinallyThatReturns",
            "try { for await (const x of source(log, OBJECT)) { yield 1; } } finally { log.push('finally'); return 7; }",
            "it.return(5)",
            "called, t1, return, t2, finally, t3, t4, resolved {7,true}, t5, t6, t7, t8, t9"
        },
        {
            "ALoopInAFinally",
            "try { yield 0; } finally { for await (const x of source(log, OBJECT)) { yield 1; } }",
            "it.next().then(r => { log.push('first ' + fmt(r)); return it.return(5); })",
            "called, t1, t2, t3, first {1,false}, t4, return, t5, t6, t7, resolved {5,true}, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> SyncIterables => new()
    {
        {
            // %AsyncFromSyncIteratorPrototype%.return answers a promise of {undefined,true} itself.
            "NoReturnMethod",
            "for await (const x of source(log, undefined, { sync: true })) { yield 1; }",
            "it.return(5)",
            "called, t1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // Its promise settles a job after the sync return() answers, so the close costs two.
            "AnObject",
            "for await (const x of source(log, OBJECT, { sync: true })) { yield 1; }",
            "it.return(5)",
            "called, t1, return, t2, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            "AnObjectBraceless",
            "for await (const x of source(log, OBJECT, { sync: true })) yield 1;",
            "it.return(5)",
            "called, t1, return, t2, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9"
        },
        {
            // Every failure of the sync return() is a rejection of the promise the async-from-sync return answers.
            "AThrow",
            "for await (const x of source(log, THROWS, { sync: true })) { yield 1; }",
            "it.return(5)",
            "called, t1, return, t2, t3, rejected T, t4, t5, t6, t7, t8, t9"
        },
        {
            "ANonObject",
            "for await (const x of source(log, NUMBER, { sync: true })) { yield 1; }",
            "it.return(5)",
            "called, t1, return, t2, t3, rejected TypeError, t4, t5, t6, t7, t8, t9"
        },
        {
            "ARejectedValue",
            "for await (const x of source(log, () => ({ done: true, value: Promise.reject('V') }), { sync: true })) { yield 1; }",
            "it.return(5)",
            "called, t1, return, t2, t3, t4, rejected V, t5, t6, t7, t8, t9"
        },
        {
            "AnArray",
            "for await (const x of [1, 2]) { yield 1; }",
            "it.return(5)",
            "called, t1, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9"
        },
    };

    public static TestCases<string, string, string, string> OtherRequests => new()
    {
        {
            "ASecondRequestQueuesBehindTheClose",
            "for await (const x of source(log, OBJECT)) { yield 1; }",
            "(it.return(5).then(r => log.push('first ' + fmt(r))), it.next())",
            "called, t1, return, t2, t3, first {5,true}, resolved {undefined,true}, t4, t5, t6, t7, t8, t9"
        },
        {
            // A throw() resumption at the yield, which already awaited the close (sebastienros/jint#4354).
            "AThrowResumption",
            "try { for await (const x of source(log, OBJECT)) { yield 1; } } catch (e) { log.push('caught ' + show(e)); }",
            "it.throw('E')",
            "return, called, t1, caught E, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
        {
            "ABreak",
            "for await (const x of source(log, OBJECT)) { if ((yield 1) === 'stop') break; } log.push('after');",
            "it.next('stop')",
            "return, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9"
        },
    };

    /// <summary>
    /// Each answer of <c>return()</c>, with braces around the body and without: a body whose <c>yield</c> is a statement
    /// hands the return back in-band (<c>ReturnRequested</c>), and a brace-less one raises it as an exception.
    /// </summary>
    [TestCaseSource(nameof(WhatReturnAnswers)), CancelAfter(10000)]
    public void TheReturnAwaitsWhatTheIteratorsReturnAnswers(string name, string body, string request, string expected)
        => Probe(body, request).Should().Be(expected, name);

    /// <summary>
    /// The heads and the yield positions a return() resumption leaves the loop from, including a <c>yield</c> in the
    /// head's binding pattern (step 8.i) and a <c>yield*</c> in the body.
    /// </summary>
    [TestCaseSource(nameof(HowTheReturnReachesTheLoop)), CancelAfter(10000)]
    public void EveryWayTheReturnLeavesTheLoopAwaitsTheClose(string name, string body, string request, string expected)
        => Probe(body, request).Should().Be(expected, name);

    /// <summary>
    /// A head declaring <c>using</c> disposes the iteration's resource first (step 8.k), then the iterator is closed.
    /// A sync dispose takes no await, so the close was the synchronous one; an <c>await using</c> dispose suspends, and
    /// its resume already went through AsyncIteratorClose.
    /// </summary>
    [TestCaseSource(nameof(WithAUsingHead)), CancelAfter(10000)]
    public void AUsingHeadDisposesAndThenAwaitsTheClose(string name, string body, string request, string expected)
        => Probe(body, request).Should().Be(expected, name);

    /// <summary>
    /// Nested loops, each of which owes its own close, and a loop inside another statement the replay re-enters.
    /// </summary>
    [TestCaseSource(nameof(NestedLoops)), CancelAfter(10000)]
    public void NestedLoopsAwaitEachClose(string name, string body, string request, string expected)
        => Probe(body, request).Should().Be(expected, name);

    /// <summary>
    /// The return completion goes on through an enclosing <c>finally</c>, which may itself yield or return, once the
    /// close has settled; a rejection the close produced reaches a <c>catch</c> instead.
    /// </summary>
    [TestCaseSource(nameof(AnEnclosingTry)), CancelAfter(10000)]
    public void AnEnclosingTryRunsOnceTheCloseHasSettled(string name, string body, string request, string expected)
        => Probe(body, request).Should().Be(expected, name);

    /// <summary>
    /// A sync iterable under <c>for await</c> is closed through
    /// <see href="https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.return">%AsyncFromSyncIteratorPrototype%.return</see>,
    /// which always exists and always answers a promise, so even an array's loop owes an await.
    /// </summary>
    [TestCaseSource(nameof(SyncIterables)), CancelAfter(10000)]
    public void ASyncIterableUnderForAwaitAwaitsItsAsyncFromSyncClose(string name, string body, string request, string expected)
        => Probe(body, request).Should().Be(expected, name);

    /// <summary>
    /// The generator is executing while the close awaits, so a later request queues behind it; and the resumptions that
    /// already awaited the close.
    /// </summary>
    [TestCaseSource(nameof(OtherRequests)), CancelAfter(10000)]
    public void OtherRequestsAndResumptions(string name, string body, string request, string expected)
        => Probe(body, request).Should().Be(expected, name);

    /// <summary>
    /// A sync generator's <c>for</c>-<c>of</c> left by <c>return()</c> closes its iterator with IteratorClose, which
    /// awaits nothing, before <c>return()</c> answers, braces or not: the exit this change shares with it stays
    /// synchronous.
    /// </summary>
    [Test]
    public void ASyncGeneratorsLoopStillClosesAtOnce()
    {
        var engine = new Engine();
        engine.Evaluate("""
            const log = [];
            const iterable = { [Symbol.iterator]() { return { next() { return { value: 1, done: false }; }, return() { log.push('return'); return {}; } }; } };
            const g = (function* () { for (const x of iterable) { yield x; } })();
            g.next();
            const r = g.return(5);
            log.push('answered ' + r.value + ',' + r.done);
            const h = (function* () { for (const x of iterable) yield x; })();
            h.next();
            const s = h.return(6);
            log.push('answered ' + s.value + ',' + s.done);
            log.join(', ');
            """).AsString().Should().Be("return, answered 5,true, return, answered 6,true");
    }

    private static string Probe(string body, string request)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        return engine.Evaluate(Prelude + "probe(log => (async function* () { " + body + " })(), (it, log) => " + request + ");")
            .UnwrapIfPromise()
            .AsString();
    }
}
