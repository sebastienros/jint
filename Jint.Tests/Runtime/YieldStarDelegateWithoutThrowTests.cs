#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A generator suspended in <c>yield*</c> and resumed with <c>throw()</c>, over a delegate that has no <c>throw</c>,
/// closes the delegate and then throws a <c>TypeError</c>
/// (<a href="https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation">YieldExpression : yield * AssignmentExpression</a>,
/// step 7.b.iii). An async generator closes it with
/// <a href="https://tc39.es/ecma262/#sec-asynciteratorclose">AsyncIteratorClose</a>, whose completion is a normal one:
/// it awaits what <c>return</c> answered (step 4.d), so a rejection is what the <c>yield*</c> throws (step 6), a thenable's
/// <c>then</c> is called, and an answer that settles to something other than an object is a <c>TypeError</c> of its
/// own (step 7). Only an object answer lets the close complete, and the <c>TypeError</c> for the missing <c>throw</c>
/// follows, one await later. Jint called <c>return</c> and threw the <c>TypeError</c> at once (sebastienros/jint#4498).
/// <para>
/// Every row runs in a generator and in an async generator, whose delegate is an async iterable. A generator's
/// IteratorClose awaits nothing, so a promise or a thenable is just an object to it and its rows are the controls, with
/// the two async rows that already held: no <c>return</c>, and a <c>return</c> that throws. A promise whose
/// <c>constructor</c> getter throws is the Await's PromiseResolve failing before anything is awaited, which is thrown
/// at once; Jint never read it. The expected values are Node 26's. A <c>TypeError</c> is told apart by its message, for
/// the missing <c>throw</c> or the non-object answer, which reads the same in both engines.
/// </para>
/// </summary>
public class YieldStarDelegateWithoutThrowTests
{
    private const string Harness = """
        // What a delegate with no `throw` answers from its `return`, as yield* closes it on a throw() resumption.
        const log = [];
        const returns = {
            none: undefined,
            throws: function () { log.push('return'); throw 'R'; },
            rejects: function () { log.push('return'); const p = Promise.reject('R'); p.catch(function () {}); return p; },
            thenable: function () { log.push('return'); return { then(resolve) { log.push('then'); resolve({}); } }; },
            thenableRejects: function () { log.push('return'); return { then(_, reject) { log.push('then'); reject('R'); } }; },
            thenableNonObject: function () { log.push('return'); return { then(resolve) { log.push('then'); resolve(1); } }; },
            nonObject: function () { log.push('return'); return 1; },
            promiseNonObject: function () { log.push('return'); return Promise.resolve(1); },
            object: function () { log.push('return'); return {}; },
            promiseObject: function () { log.push('return'); return Promise.resolve({}); },
            constructorThrows: function () {
                log.push('return');
                const p = Promise.resolve({});
                Object.defineProperty(p, 'constructor', { get() { log.push('constructor'); throw 'C'; } });
                return p;
            }
        };

        // A TypeError is told apart by what it is for: the delegate's missing `throw`, or the non-object `return` answered.
        const tag = function (e) {
            if (typeof e === 'string') { return e; }
            if (e instanceof TypeError) { return /throw/.test(e.message) ? 'TypeError:noThrow' : 'TypeError:nonObject'; }
            return e.constructor.name;
        };

        // The delegate's iterator steps forever and has no `throw`; its `return` is called once, on it, with no argument.
        function delegateFor(kind, way) {
            const it = {
                next() { return { value: 'x', done: false }; },
            };
            if (returns[way] !== undefined) {
                it.return = function () {
                    if (arguments.length !== 0) { log.push('args:' + arguments.length); }
                    if (this !== it) { log.push('this:wrong'); }
                    return returns[way].call(this);
                };
            }
            return kind === 'async' ? { [Symbol.asyncIterator]() { return it; } } : { [Symbol.iterator]() { return it; } };
        }

        // The generator suspended in yield* is resumed with throw('T'); nothing in its body catches what the yield* throws.
        async function uncaught(kind, way) {
            log.length = 0;
            const d = delegateFor(kind, way);
            const g = kind === 'sync' ? (function* () { yield* d; })() : (async function* () { yield* d; })();
            await g.next();
            let outcome;
            try { const r = await g.throw('T'); outcome = r.value + ':' + r.done; } catch (e) { outcome = 'threw:' + tag(e); }
            const after = await g.next();
            return outcome + ' | ' + log.join(',') + ' | after ' + after.value + ':' + after.done;
        }

        // The same, with a try/catch around the yield*: the throw lands at the yield*, and the body goes on.
        async function caught(kind, way) {
            log.length = 0;
            const d = delegateFor(kind, way);
            const g = kind === 'sync'
                ? (function* () { try { yield* d; } catch (e) { yield 'caught:' + tag(e); } return 'end'; })()
                : (async function* () { try { yield* d; } catch (e) { yield 'caught:' + tag(e); } return 'end'; })();
            const out = [];
            let r = await g.next();
            out.push(r.value + ':' + r.done);
            r = await g.throw('T');
            out.push(r.value + ':' + r.done);
            r = await g.next();
            out.push(r.value + ':' + r.done);
            return out.join(' ') + ' | ' + log.join(',');
        }

        // Microtask interleaving: when the close's steps run and when throw()'s promise settles, against a chain of ticks
        // started right after the throw() call. A next() queued behind the throw() shows the generator stayed busy.
        async function ticks(way) {
            log.length = 0;
            const d = delegateFor('async', way);
            const g = (async function* () { try { yield* d; } catch (e) { log.push('caught:' + tag(e)); } yield 'after'; })();
            await g.next();
            const p = g.throw('T');
            log.push('throw() returned');
            const q = g.next();
            p.then(function (r) { log.push('throw:' + r.value + ':' + r.done); }, function (e) { log.push('throw rejected:' + tag(e)); });
            q.then(function (r) { log.push('next:' + r.value + ':' + r.done); }, function (e) { log.push('next rejected:' + tag(e)); });
            let chain = Promise.resolve();
            for (let i = 1; i <= 8; i++) { chain = chain.then(function () { log.push('t' + i); }); }
            await chain;
            return log.join(',');
        }
        """;

    /// <summary>
    /// What the <c>throw()</c> answers when nothing in the body catches it, which <c>return</c> calls were made, and that
    /// the generator is done afterwards. In the async generator a rejection wins over the <c>TypeError</c>s, a thenable's
    /// <c>then</c> is called and its outcome counts, and an answer that settles to a non-object is the step 7
    /// <c>TypeError</c> rather than the one for the missing <c>throw</c>.
    /// </summary>
    [TestCase("sync", "none", "threw:TypeError:noThrow |  | after undefined:true")]
    [TestCase("sync", "throws", "threw:R | return | after undefined:true")]
    [TestCase("sync", "rejects", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("sync", "thenable", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("sync", "thenableRejects", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("sync", "thenableNonObject", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("sync", "nonObject", "threw:TypeError:nonObject | return | after undefined:true")]
    [TestCase("sync", "promiseNonObject", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("sync", "object", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("sync", "promiseObject", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("sync", "constructorThrows", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("async", "none", "threw:TypeError:noThrow |  | after undefined:true")]
    [TestCase("async", "throws", "threw:R | return | after undefined:true")]
    [TestCase("async", "rejects", "threw:R | return | after undefined:true")]
    [TestCase("async", "thenable", "threw:TypeError:noThrow | return,then | after undefined:true")]
    [TestCase("async", "thenableRejects", "threw:R | return,then | after undefined:true")]
    [TestCase("async", "thenableNonObject", "threw:TypeError:nonObject | return,then | after undefined:true")]
    [TestCase("async", "nonObject", "threw:TypeError:nonObject | return | after undefined:true")]
    [TestCase("async", "promiseNonObject", "threw:TypeError:nonObject | return | after undefined:true")]
    [TestCase("async", "object", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("async", "promiseObject", "threw:TypeError:noThrow | return | after undefined:true")]
    [TestCase("async", "constructorThrows", "threw:C | return,constructor | after undefined:true")]
    public void TheCloseOfADelegateWithoutThrowDecidesWhatTheYieldStarThrows(string kind, string way, string expected)
    {
        var engine = new Engine();
        engine.Evaluate(Harness + "\n" + $"uncaught('{kind}', '{way}')").UnwrapIfPromise().AsString().Should().Be(expected);
    }

    /// <summary>
    /// The same closes with a <c>try</c>/<c>catch</c> around the <c>yield*</c>: what the close decided is thrown at the
    /// <c>yield*</c>, once the awaited answer has settled, and the body goes on from the <c>catch</c>, yielding from it
    /// and then completing.
    /// </summary>
    [TestCase("sync", "none", "x:false caught:TypeError:noThrow:false end:true | ")]
    [TestCase("sync", "throws", "x:false caught:R:false end:true | return")]
    [TestCase("sync", "rejects", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("sync", "thenable", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("sync", "thenableRejects", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("sync", "thenableNonObject", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("sync", "nonObject", "x:false caught:TypeError:nonObject:false end:true | return")]
    [TestCase("sync", "promiseNonObject", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("sync", "object", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("sync", "promiseObject", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("sync", "constructorThrows", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("async", "none", "x:false caught:TypeError:noThrow:false end:true | ")]
    [TestCase("async", "throws", "x:false caught:R:false end:true | return")]
    [TestCase("async", "rejects", "x:false caught:R:false end:true | return")]
    [TestCase("async", "thenable", "x:false caught:TypeError:noThrow:false end:true | return,then")]
    [TestCase("async", "thenableRejects", "x:false caught:R:false end:true | return,then")]
    [TestCase("async", "thenableNonObject", "x:false caught:TypeError:nonObject:false end:true | return,then")]
    [TestCase("async", "nonObject", "x:false caught:TypeError:nonObject:false end:true | return")]
    [TestCase("async", "promiseNonObject", "x:false caught:TypeError:nonObject:false end:true | return")]
    [TestCase("async", "object", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("async", "promiseObject", "x:false caught:TypeError:noThrow:false end:true | return")]
    [TestCase("async", "constructorThrows", "x:false caught:C:false end:true | return,constructor")]
    public void WhatTheCloseDecidedIsThrownAtTheYieldStarWhereACatchSeesIt(string kind, string way, string expected)
    {
        var engine = new Engine();
        engine.Evaluate(Harness + "\n" + $"caught('{kind}', '{way}')").UnwrapIfPromise().AsString().Should().Be(expected);
    }

    /// <summary>
    /// The order of the close's steps against a chain of microtasks, as V8 runs them. <c>return</c> is called inside the
    /// <c>throw()</c> call; a thenable's <c>then</c> on the next microtask; the <c>catch</c> runs once the awaited answer
    /// has settled, one microtask after the call for a value or a promise and two for a thenable; and the <c>next()</c>
    /// queued behind the <c>throw()</c> waits for it. Without a <c>return</c>, or when reading the promise's
    /// <c>constructor</c> throws, nothing is awaited and the <c>catch</c> runs inside the call; the first two rows are
    /// the controls.
    /// </summary>
    [TestCase("none", "caught:TypeError:noThrow,throw() returned,t1,throw:after:false,next:undefined:true,t2,t3,t4,t5,t6,t7,t8")]
    [TestCase("throws", "return,caught:R,throw() returned,t1,throw:after:false,next:undefined:true,t2,t3,t4,t5,t6,t7,t8")]
    [TestCase("rejects", "return,throw() returned,caught:R,t1,t2,throw:after:false,next:undefined:true,t3,t4,t5,t6,t7,t8")]
    [TestCase("thenable", "return,throw() returned,then,t1,caught:TypeError:noThrow,t2,t3,throw:after:false,next:undefined:true,t4,t5,t6,t7,t8")]
    [TestCase("thenableRejects", "return,throw() returned,then,t1,caught:R,t2,t3,throw:after:false,next:undefined:true,t4,t5,t6,t7,t8")]
    [TestCase("thenableNonObject", "return,throw() returned,then,t1,caught:TypeError:nonObject,t2,t3,throw:after:false,next:undefined:true,t4,t5,t6,t7,t8")]
    [TestCase("nonObject", "return,throw() returned,caught:TypeError:nonObject,t1,t2,throw:after:false,next:undefined:true,t3,t4,t5,t6,t7,t8")]
    [TestCase("promiseNonObject", "return,throw() returned,caught:TypeError:nonObject,t1,t2,throw:after:false,next:undefined:true,t3,t4,t5,t6,t7,t8")]
    [TestCase("object", "return,throw() returned,caught:TypeError:noThrow,t1,t2,throw:after:false,next:undefined:true,t3,t4,t5,t6,t7,t8")]
    [TestCase("promiseObject", "return,throw() returned,caught:TypeError:noThrow,t1,t2,throw:after:false,next:undefined:true,t3,t4,t5,t6,t7,t8")]
    [TestCase("constructorThrows", "return,constructor,caught:C,throw() returned,t1,throw:after:false,next:undefined:true,t2,t3,t4,t5,t6,t7,t8")]
    public void TheCloseAwaitsOnTheMicrotasksV8AwaitsOn(string way, string expected)
    {
        var engine = new Engine();
        engine.Evaluate(Harness + "\n" + $"ticks('{way}')").UnwrapIfPromise().AsString().Should().Be(expected);
    }
}
