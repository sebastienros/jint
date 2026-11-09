#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins how the async iterator helpers close their source: through
/// <a href="https://tc39.es/ecma262/#sec-asynciteratorclose">AsyncIteratorClose</a>, which awaits what <c>return</c>
/// answered (step 4.d) and then decides what the close raises (steps 5 to 8). <c>take</c> at its limit, and
/// <c>some</c>, <c>every</c> and <c>find</c> finishing early, close with a normal completion
/// (<a href="https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.take">take</a>,
/// <a href="https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.some">some</a>), so a
/// <c>return</c> that throws, rejects or answers something other than an object fails them.
/// <a href="https://tc39.es/proposal-async-iterator-helpers/#sec-ifabruptcloseasynciterator">IfAbruptCloseAsyncIterator</a>
/// closes with a throw completion, which wins whatever the close does, but only once what <c>return</c> answered has
/// settled. A helper's <c>return()</c> closes with a return completion, and <c>flatMap</c>'s closes the inner iterator
/// first and awaits it, as tc39/proposal-async-iterator-helpers#30 decided.
/// <para>
/// Jint called <c>return</c> synchronously inside a <c>try</c> that swallowed everything and settled at once
/// (sebastienros/jint#4500). Expected values are Node 26's: with the core-js 3.51 polyfill for the helpers, since Node
/// has no <c>AsyncIterator</c>, and natively for the controls that run beside them — the sync helpers, a
/// <c>for await</c> leaving its loop, an async generator, and #30's nested <c>for await</c> for <c>flatMap</c>.
/// </para>
/// </summary>
public class AsyncIteratorHelperCloseTests
{
    private const string Harness = """
        // A TypeError is the engine's, whose message differs between engines; every other error is the script's own.
        function describe(e) { return e instanceof TypeError ? 'TypeError' : e instanceof Error ? e.name + ':' + e.message : 'value:' + String(e); }
        function settle(p, log) {
            return p.then(
                v => 'fulfilled:' + JSON.stringify(v) + '|' + log.join(','),
                e => 'rejected:' + describe(e) + '|' + log.join(','));
        }

        // A `return` that answers `how`, logging `<label>return` when called and `<label>then` when a thenable it answered is
        // awaited. `missing` is no return at all; `getterThrows` is a return getter that throws.
        function attachReturn(target, how, log, label) {
            label = label || '';
            const thenable = (ok) => ({ then(f, r) { log.push(label + 'then'); ok ? f({}) : r(new RangeError(label + 'r')); } });
            let fn;
            switch (how) {
                case 'missing': return target;
                case 'getterThrows':
                    Object.defineProperty(target, 'return', { get() { log.push(label + 'get'); throw new RangeError(label + 'g'); } });
                    return target;
                case 'throws': fn = () => { throw new RangeError(label + 'r'); }; break;
                case 'rejects': fn = () => Promise.reject(new RangeError(label + 'r')); break;
                case 'number': fn = () => 5; break;
                case 'promiseOfNumber': fn = () => Promise.resolve(5); break;
                case 'undefined': fn = () => undefined; break;
                case 'object': fn = () => ({}); break;
                case 'promiseOfObject': fn = () => Promise.resolve({}); break;
                case 'thenable': fn = () => thenable(true); break;
                case 'thenableRejects': fn = () => thenable(false); break;
            }
            target.return = function () { log.push(label + 'return:' + arguments.length + ':' + (this === target)); return fn(); };
            return target;
        }

        function asyncSource(how, log) {
            return attachReturn({ next() { log.push('next'); return Promise.resolve({ value: 1, done: false }); } }, how, log);
        }

        function syncSource(how, log) {
            return attachReturn({ next() { log.push('next'); return { value: 1, done: false }; } }, how, log);
        }

        // take at its limit, and some/every/find finishing early: ? AsyncIteratorClose(iterated, NormalCompletion(...)).
        function finish(name, how) {
            const log = [];
            const src = asyncSource(how, log);
            const P = AsyncIterator.prototype;
            switch (name) {
                case 'take': return settle(P.take.call(src, 0).next(), log);
                case 'takeAfterOne': { const h = P.take.call(src, 1); return settle(h.next().then(() => h.next()), log); }
                case 'some': return settle(P.some.call(src, () => true), log);
                case 'every': return settle(P.every.call(src, () => false), log);
                case 'find': return settle(P.find.call(src, () => true), log);
                case 'forAwait': return settle((async () => { for await (const x of { [Symbol.asyncIterator]() { return src; } }) { return true; } })(), log);
            }
        }

        // The sync helpers doing the same: ? IteratorClose(iterated, ...), the control.
        function finishSync(name, how) {
            const log = [];
            const src = syncSource(how, log);
            const P = Iterator.prototype;
            const run = () => {
                switch (name) {
                    case 'take': return P.take.call(src, 0).next();
                    case 'takeAfterOne': { const h = P.take.call(src, 1); h.next(); return h.next(); }
                    case 'some': return P.some.call(src, () => true);
                    case 'every': return P.every.call(src, () => false);
                    case 'find': return P.find.call(src, () => true);
                }
            };
            try { return 'returned:' + JSON.stringify(run()) + '|' + log.join(','); } catch (e) { return 'threw:' + describe(e) + '|' + log.join(','); }
        }

        // What the helper calls with a value fails: IfAbruptCloseAsyncIterator(value, iterated), a throw completion.
        function abrupt(name, how) {
            const log = [];
            const src = asyncSource(how, log);
            const P = AsyncIterator.prototype;
            const boom = () => { throw new SyntaxError('cb'); };
            const rejecting = () => Promise.reject(new SyntaxError('cb'));
            switch (name) {
                case 'map': return settle(P.map.call(src, boom).next(), log);
                case 'mapRejects': return settle(P.map.call(src, rejecting).next(), log);
                case 'filter': return settle(P.filter.call(src, boom).next(), log);
                case 'flatMap': return settle(P.flatMap.call(src, boom).next(), log);
                case 'flatMapNotIterable': return settle(P.flatMap.call(src, () => 5).next(), log);
                case 'forEach': return settle(P.forEach.call(src, boom), log);
                case 'reduce': return settle(P.reduce.call(src, boom, 0), log);
                case 'some': return settle(P.some.call(src, boom), log);
                case 'someRejects': return settle(P.some.call(src, rejecting), log);
                case 'every': return settle(P.every.call(src, boom), log);
                case 'find': return settle(P.find.call(src, boom), log);
                case 'forAwait': return settle((async () => { for await (const x of { [Symbol.asyncIterator]() { return src; } }) { boom(); } })(), log);
            }
        }

        // The microtask order: a tick ladder starts inside `return`, so where the helper settles among the ladder's ticks is how
        // many jobs the close took after `return` was called.
        function order(name, how) {
            const log = [];
            let ticks = 0;
            let done;
            const finished = new Promise(r => { done = r; });
            const tick = () => { log.push('t' + ticks++); if (ticks < 8) Promise.resolve().then(tick); else done(log.join(',')); };
            const src = attachReturn({ next() { return Promise.resolve({ value: 1, done: false }); } }, how, log);
            const original = src.return;
            src.return = function () { Promise.resolve().then(tick); return original.call(this); };
            const P = AsyncIterator.prototype;
            const boom = () => { throw new SyntaxError('cb'); };
            let p;
            switch (name) {
                case 'take': p = P.take.call(src, 0).next(); break;
                case 'some': p = P.some.call(src, () => true); break;
                case 'every': p = P.every.call(src, () => false); break;
                case 'find': p = P.find.call(src, () => true); break;
                case 'map': p = P.map.call(src, boom).next(); break;
                case 'forEach': p = P.forEach.call(src, boom); break;
                case 'reduce': p = P.reduce.call(src, boom, 0); break;
                case 'forAwait': p = (async () => { for await (const x of { [Symbol.asyncIterator]() { return src; } }) { return true; } })(); break;
                case 'forAwaitThrow': p = (async () => { for await (const x of { [Symbol.asyncIterator]() { return src; } }) { boom(); } })(); break;
            }
            p.then(() => log.push('fulfilled'), e => log.push('rejected:' + describe(e)));
            return finished;
        }

        // A helper's return() at a yield: IfAbruptCloseAsyncIterator(completion, iterated) with the return completion, which
        // awaits what `return` answered and checks it is an object. A second return() closes nothing.
        function helperReturn(name, how) {
            const log = [];
            const src = asyncSource(how, log);
            const P = AsyncIterator.prototype;
            let h;
            switch (name) {
                case 'map': h = P.map.call(src, x => x); break;
                case 'filter': h = P.filter.call(src, () => true); break;
                case 'take': h = P.take.call(src, 5); break;
                case 'drop': h = P.drop.call(src, 0); break;
                case 'flatMap': h = P.flatMap.call(src, x => [x]); break;
                case 'generator': h = (async function* () { for await (const x of { [Symbol.asyncIterator]() { return src; } }) yield x; })(); break;
            }
            return settle(h.next().then(() => h.return()).then(r => { log.push('second:' + JSON.stringify(r)); return h.return(); }), log);
        }

        // flatMap's return() with an inner iterator open: the inner one is closed first, and awaited, then the outer one. `naive`
        // runs tc39/proposal-async-iterator-helpers#30's nested for await instead, which is that order natively.
        function flatMapReturn(innerHow, outerHow, naive) {
            const log = [];
            const inner = attachReturn({
                [Symbol.asyncIterator]() { return this; },
                next() { log.push('inner.next'); return Promise.resolve({ value: 'i', done: false }); }
            }, innerHow, log, 'inner.');
            const outer = attachReturn({
                next() { log.push('outer.next'); return Promise.resolve({ value: 1, done: false }); }
            }, outerHow, log, 'outer.');
            const h = naive
                ? (async function* () {
                    for await (const x of { [Symbol.asyncIterator]() { return outer; } }) {
                        for await (const item of inner) yield item;
                    }
                })()
                : AsyncIterator.prototype.flatMap.call(outer, () => inner);
            return settle(h.next().then(() => h.return()).then(r => { log.push('second:' + JSON.stringify(r)); return h.return(); }), log);
        }

        // flatMap's inner step whose promise's `constructor` throws in the Await's PromiseResolve: the outer iterator is closed,
        // the helper completes, and a later return() closes nothing, the inner iterator included. `good` inner steps succeed
        // first, so 0 fails inside the mapper's continuation and 1 inside a later next().
        function flatMapInnerResolveThrows(good) {
            const log = [];
            const bad = Promise.resolve({ value: 'i', done: false });
            Object.defineProperty(bad, 'constructor', { get() { log.push('constructor'); throw new RangeError('c'); } });
            let n = 0;
            const inner = {
                [Symbol.asyncIterator]() { return this; },
                next() { log.push('inner.next'); return n++ < good ? Promise.resolve({ value: 'i', done: false }) : bad; },
                return() { log.push('inner.return'); return {}; }
            };
            const outer = {
                next() { log.push('outer.next'); return Promise.resolve({ value: 1, done: false }); },
                return() { log.push('outer.return'); return {}; }
            };
            const h = AsyncIterator.prototype.flatMap.call(outer, () => inner);
            return (async function () {
                let caught = 'none';
                try { for (;;) { const r = await h.next(); if (r.done) break; log.push('value:' + r.value); } } catch (e) { caught = describe(e); }
                log.push('caught:' + caught);
                const r = await h.return();
                log.push('return:' + JSON.stringify(r));
                return log.join(',');
            })();
        }

        // Two return() calls while flatMap's inner iterator is open, the inner `return` answering a promise that settles a
        // few jobs later: the second return() finds the helper completed and resolves at once, and the source is closed
        // only once the inner close has settled, after which the first resolves.
        function flatMapReturnTwice() {
            const log = [];
            const inner = {
                [Symbol.asyncIterator]() { return this; },
                next() { return Promise.resolve({ value: 'i', done: false }); },
                return() {
                    log.push('inner.return');
                    return Promise.resolve().then(() => { }).then(() => { log.push('inner.settled'); return {}; });
                }
            };
            const outer = {
                next() { return Promise.resolve({ value: 1, done: false }); },
                return() { log.push('outer.return'); return {}; }
            };
            const h = AsyncIterator.prototype.flatMap.call(outer, () => inner);
            return h.next().then(() => {
                const a = h.return().then(() => log.push('a'));
                const b = h.return().then(() => log.push('b'));
                return Promise.all([a, b]).then(() => log.join(','));
            });
        }

        // A return() while a step is in flight: the step still answers its value, the source is closed once, and a later
        // next() answers done without stepping the closed source again. The async generator doing the same with
        // for await is the control.
        function returnDuringStep(name) {
            const log = [];
            let n = 0;
            const src = {
                next() { n++; log.push('next' + n); return Promise.resolve({ value: 1, done: false }); },
                return() { log.push('return'); return {}; }
            };
            const P = AsyncIterator.prototype;
            let h;
            switch (name) {
                case 'map': h = P.map.call(src, x => x); break;
                case 'filter': h = P.filter.call(src, () => true); break;
                case 'take': h = P.take.call(src, 5); break;
                case 'drop': h = P.drop.call(src, 0); break;
                case 'flatMap': h = P.flatMap.call(src, x => [x]); break;
                case 'generator': h = (async function* () { for await (const x of { [Symbol.asyncIterator]() { return src; } }) yield x; })(); break;
            }
            const p1 = h.next();
            const p2 = h.return();
            return Promise.all([p1, p2]).then(([r1, r2]) => h.next().then(r3 =>
                'p1:' + JSON.stringify(r1) + '|p2:' + JSON.stringify(r2) + '|p3:' + JSON.stringify(r3) + '|' + log.join(',')));
        }
        """;

    private static string Run(string script) =>
        new Engine().Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    /// <summary>
    /// <c>? AsyncIteratorClose(iterated, NormalCompletion(…))</c>: what <c>return</c> throws, a rejection and an answer
    /// that is not an object (a <c>TypeError</c>, after the Await) each fail the helper; an object answer, a thenable
    /// that fulfils with one, and a missing <c>return</c> let it finish. <c>return</c> is called once, with no
    /// arguments and the source as <c>this</c>, and a thenable it answered is awaited. A <c>for await</c> whose body
    /// returns is the native control.
    /// </summary>
    [TestCase("take", "throws", "rejected:RangeError:r|return:0:true")]
    [TestCase("take", "rejects", "rejected:RangeError:r|return:0:true")]
    [TestCase("take", "number", "rejected:TypeError|return:0:true")]
    [TestCase("take", "promiseOfNumber", "rejected:TypeError|return:0:true")]
    [TestCase("take", "undefined", "rejected:TypeError|return:0:true")]
    [TestCase("take", "object", "fulfilled:{\"done\":true}|return:0:true")]
    [TestCase("take", "promiseOfObject", "fulfilled:{\"done\":true}|return:0:true")]
    [TestCase("take", "thenable", "fulfilled:{\"done\":true}|return:0:true,then")]
    [TestCase("take", "thenableRejects", "rejected:RangeError:r|return:0:true,then")]
    [TestCase("take", "missing", "fulfilled:{\"done\":true}|")]
    [TestCase("take", "getterThrows", "rejected:RangeError:g|get")]
    [TestCase("takeAfterOne", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("takeAfterOne", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("takeAfterOne", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("takeAfterOne", "promiseOfNumber", "rejected:TypeError|next,return:0:true")]
    [TestCase("takeAfterOne", "undefined", "rejected:TypeError|next,return:0:true")]
    [TestCase("takeAfterOne", "object", "fulfilled:{\"done\":true}|next,return:0:true")]
    [TestCase("takeAfterOne", "promiseOfObject", "fulfilled:{\"done\":true}|next,return:0:true")]
    [TestCase("takeAfterOne", "thenable", "fulfilled:{\"done\":true}|next,return:0:true,then")]
    [TestCase("takeAfterOne", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("takeAfterOne", "missing", "fulfilled:{\"done\":true}|next")]
    [TestCase("takeAfterOne", "getterThrows", "rejected:RangeError:g|next,get")]
    [TestCase("some", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("some", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("some", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("some", "promiseOfNumber", "rejected:TypeError|next,return:0:true")]
    [TestCase("some", "undefined", "rejected:TypeError|next,return:0:true")]
    [TestCase("some", "object", "fulfilled:true|next,return:0:true")]
    [TestCase("some", "promiseOfObject", "fulfilled:true|next,return:0:true")]
    [TestCase("some", "thenable", "fulfilled:true|next,return:0:true,then")]
    [TestCase("some", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("some", "missing", "fulfilled:true|next")]
    [TestCase("some", "getterThrows", "rejected:RangeError:g|next,get")]
    [TestCase("every", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("every", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("every", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("every", "promiseOfNumber", "rejected:TypeError|next,return:0:true")]
    [TestCase("every", "undefined", "rejected:TypeError|next,return:0:true")]
    [TestCase("every", "object", "fulfilled:false|next,return:0:true")]
    [TestCase("every", "promiseOfObject", "fulfilled:false|next,return:0:true")]
    [TestCase("every", "thenable", "fulfilled:false|next,return:0:true,then")]
    [TestCase("every", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("every", "missing", "fulfilled:false|next")]
    [TestCase("every", "getterThrows", "rejected:RangeError:g|next,get")]
    [TestCase("find", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("find", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("find", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("find", "promiseOfNumber", "rejected:TypeError|next,return:0:true")]
    [TestCase("find", "undefined", "rejected:TypeError|next,return:0:true")]
    [TestCase("find", "object", "fulfilled:1|next,return:0:true")]
    [TestCase("find", "promiseOfObject", "fulfilled:1|next,return:0:true")]
    [TestCase("find", "thenable", "fulfilled:1|next,return:0:true,then")]
    [TestCase("find", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("find", "missing", "fulfilled:1|next")]
    [TestCase("find", "getterThrows", "rejected:RangeError:g|next,get")]
    [TestCase("forAwait", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("forAwait", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("forAwait", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("forAwait", "promiseOfNumber", "rejected:TypeError|next,return:0:true")]
    [TestCase("forAwait", "undefined", "rejected:TypeError|next,return:0:true")]
    [TestCase("forAwait", "object", "fulfilled:true|next,return:0:true")]
    [TestCase("forAwait", "promiseOfObject", "fulfilled:true|next,return:0:true")]
    [TestCase("forAwait", "thenable", "fulfilled:true|next,return:0:true,then")]
    [TestCase("forAwait", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("forAwait", "missing", "fulfilled:true|next")]
    [TestCase("forAwait", "getterThrows", "rejected:RangeError:g|next,get")]
    public void ANormalCompletionCloseAwaitsReturnAndRaisesWhatItDecides(string name, string how, string expected)
    {
        Run($"finish('{name}', '{how}')").Should().Be(expected);
    }

    /// <summary>
    /// The sync helpers do the same through IteratorClose, which awaits nothing: the control the async rows now agree
    /// with, Node's native values.
    /// </summary>
    [TestCase("take", "throws", "threw:RangeError:r|return:0:true")]
    [TestCase("take", "number", "threw:TypeError|return:0:true")]
    [TestCase("take", "undefined", "threw:TypeError|return:0:true")]
    [TestCase("take", "object", "returned:{\"done\":true}|return:0:true")]
    [TestCase("take", "missing", "returned:{\"done\":true}|")]
    [TestCase("take", "getterThrows", "threw:RangeError:g|get")]
    [TestCase("takeAfterOne", "throws", "threw:RangeError:r|next,return:0:true")]
    [TestCase("takeAfterOne", "number", "threw:TypeError|next,return:0:true")]
    [TestCase("takeAfterOne", "undefined", "threw:TypeError|next,return:0:true")]
    [TestCase("takeAfterOne", "object", "returned:{\"done\":true}|next,return:0:true")]
    [TestCase("takeAfterOne", "missing", "returned:{\"done\":true}|next")]
    [TestCase("takeAfterOne", "getterThrows", "threw:RangeError:g|next,get")]
    [TestCase("some", "throws", "threw:RangeError:r|next,return:0:true")]
    [TestCase("some", "number", "threw:TypeError|next,return:0:true")]
    [TestCase("some", "undefined", "threw:TypeError|next,return:0:true")]
    [TestCase("some", "object", "returned:true|next,return:0:true")]
    [TestCase("some", "missing", "returned:true|next")]
    [TestCase("some", "getterThrows", "threw:RangeError:g|next,get")]
    [TestCase("every", "throws", "threw:RangeError:r|next,return:0:true")]
    [TestCase("every", "number", "threw:TypeError|next,return:0:true")]
    [TestCase("every", "undefined", "threw:TypeError|next,return:0:true")]
    [TestCase("every", "object", "returned:false|next,return:0:true")]
    [TestCase("every", "missing", "returned:false|next")]
    [TestCase("every", "getterThrows", "threw:RangeError:g|next,get")]
    [TestCase("find", "throws", "threw:RangeError:r|next,return:0:true")]
    [TestCase("find", "number", "threw:TypeError|next,return:0:true")]
    [TestCase("find", "undefined", "threw:TypeError|next,return:0:true")]
    [TestCase("find", "object", "returned:1|next,return:0:true")]
    [TestCase("find", "missing", "returned:1|next")]
    [TestCase("find", "getterThrows", "threw:RangeError:g|next,get")]
    public void TheSyncHelpersCloseTheSameWay(string name, string how, string expected)
    {
        Run($"finishSync('{name}', '{how}')").Should().Be(expected);
    }

    /// <summary>
    /// IfAbruptCloseAsyncIterator: a mapper or predicate that throws or rejects, and a flatMap mapper answering
    /// something that is not iterable. The close's throw completion wins whatever <c>return</c> does (step 5), but
    /// <c>return</c> is still called and a thenable it answered is still awaited.
    /// </summary>
    [TestCase("map", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("map", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("map", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("map", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("map", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("map", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("map", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("mapRejects", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("mapRejects", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("mapRejects", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("mapRejects", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("mapRejects", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("mapRejects", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("mapRejects", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("filter", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("filter", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("filter", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("filter", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("filter", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("filter", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("filter", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("flatMap", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("flatMap", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("flatMap", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("flatMap", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("flatMap", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("flatMap", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("flatMap", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("flatMapNotIterable", "throws", "rejected:TypeError|next,return:0:true")]
    [TestCase("flatMapNotIterable", "rejects", "rejected:TypeError|next,return:0:true")]
    [TestCase("flatMapNotIterable", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("flatMapNotIterable", "thenable", "rejected:TypeError|next,return:0:true,then")]
    [TestCase("flatMapNotIterable", "thenableRejects", "rejected:TypeError|next,return:0:true,then")]
    [TestCase("flatMapNotIterable", "missing", "rejected:TypeError|next")]
    [TestCase("flatMapNotIterable", "getterThrows", "rejected:TypeError|next,get")]
    [TestCase("forEach", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("forEach", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("forEach", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("forEach", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("forEach", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("forEach", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("forEach", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("reduce", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("reduce", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("reduce", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("reduce", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("reduce", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("reduce", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("reduce", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("some", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("some", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("some", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("some", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("some", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("some", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("some", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("someRejects", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("someRejects", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("someRejects", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("someRejects", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("someRejects", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("someRejects", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("someRejects", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("every", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("every", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("every", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("every", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("every", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("every", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("every", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("find", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("find", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("find", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("find", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("find", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("find", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("find", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    [TestCase("forAwait", "throws", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("forAwait", "rejects", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("forAwait", "number", "rejected:SyntaxError:cb|next,return:0:true")]
    [TestCase("forAwait", "thenable", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("forAwait", "thenableRejects", "rejected:SyntaxError:cb|next,return:0:true,then")]
    [TestCase("forAwait", "missing", "rejected:SyntaxError:cb|next")]
    [TestCase("forAwait", "getterThrows", "rejected:SyntaxError:cb|next,get")]
    public void AThrowCompletionCloseAwaitsReturnAndKeepsItsOwnError(string name, string how, string expected)
    {
        Run($"abrupt('{name}', '{how}')").Should().Be(expected);
    }

    /// <summary>
    /// A helper's <c>return()</c> at a yield closes the source with a return completion: a rejection or an answer that
    /// is not an object fails it, as a <c>return()</c> to an async generator suspended in a <c>for await</c> does, and a
    /// second <c>return()</c> closes nothing.
    /// </summary>
    [TestCase("map", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("map", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("map", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("map", "thenable", "fulfilled:{\"done\":true}|next,return:0:true,then,second:{\"done\":true}")]
    [TestCase("map", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("map", "missing", "fulfilled:{\"done\":true}|next,second:{\"done\":true}")]
    [TestCase("map", "getterThrows", "rejected:RangeError:g|next,get")]
    [TestCase("filter", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("filter", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("filter", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("filter", "thenable", "fulfilled:{\"done\":true}|next,return:0:true,then,second:{\"done\":true}")]
    [TestCase("filter", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("filter", "missing", "fulfilled:{\"done\":true}|next,second:{\"done\":true}")]
    [TestCase("filter", "getterThrows", "rejected:RangeError:g|next,get")]
    [TestCase("take", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("take", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("take", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("take", "thenable", "fulfilled:{\"done\":true}|next,return:0:true,then,second:{\"done\":true}")]
    [TestCase("take", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("take", "missing", "fulfilled:{\"done\":true}|next,second:{\"done\":true}")]
    [TestCase("take", "getterThrows", "rejected:RangeError:g|next,get")]
    [TestCase("drop", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("drop", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("drop", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("drop", "thenable", "fulfilled:{\"done\":true}|next,return:0:true,then,second:{\"done\":true}")]
    [TestCase("drop", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("drop", "missing", "fulfilled:{\"done\":true}|next,second:{\"done\":true}")]
    [TestCase("drop", "getterThrows", "rejected:RangeError:g|next,get")]
    [TestCase("flatMap", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("flatMap", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("flatMap", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("flatMap", "thenable", "fulfilled:{\"done\":true}|next,return:0:true,then,second:{\"done\":true}")]
    [TestCase("flatMap", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("flatMap", "missing", "fulfilled:{\"done\":true}|next,second:{\"done\":true}")]
    [TestCase("flatMap", "getterThrows", "rejected:RangeError:g|next,get")]
    [TestCase("generator", "throws", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("generator", "rejects", "rejected:RangeError:r|next,return:0:true")]
    [TestCase("generator", "number", "rejected:TypeError|next,return:0:true")]
    [TestCase("generator", "thenable", "fulfilled:{\"done\":true}|next,return:0:true,then,second:{\"done\":true}")]
    [TestCase("generator", "thenableRejects", "rejected:RangeError:r|next,return:0:true,then")]
    [TestCase("generator", "missing", "fulfilled:{\"done\":true}|next,second:{\"done\":true}")]
    [TestCase("generator", "getterThrows", "rejected:RangeError:g|next,get")]
    public void AHelpersReturnRaisesWhatTheCloseDecides(string name, string how, string expected)
    {
        Run($"helperReturn('{name}', '{how}')").Should().Be(expected);
    }

    /// <summary>
    /// What flatMap's <c>return()</c> answers, and the order things happen in, for each way the inner and the outer
    /// iterator's <c>return</c> can behave. core-js 3.51's answers, which are also what #30's nested <c>for await</c>
    /// gives natively in Node 26.
    /// </summary>
    public static TestCases<string, string, string> FlatMapReturnRows => new()
    {
        { "object", "object", "fulfilled:{\"done\":true}|outer.next,inner.next,inner.return:0:true,outer.return:0:true,second:{\"done\":true}" },
        { "object", "rejects", "rejected:RangeError:outer.r|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "object", "number", "rejected:TypeError|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "object", "missing", "fulfilled:{\"done\":true}|outer.next,inner.next,inner.return:0:true,second:{\"done\":true}" },
        { "promiseOfObject", "object", "fulfilled:{\"done\":true}|outer.next,inner.next,inner.return:0:true,outer.return:0:true,second:{\"done\":true}" },
        { "promiseOfObject", "rejects", "rejected:RangeError:outer.r|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "promiseOfObject", "number", "rejected:TypeError|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "promiseOfObject", "missing", "fulfilled:{\"done\":true}|outer.next,inner.next,inner.return:0:true,second:{\"done\":true}" },
        { "thenable", "object", "fulfilled:{\"done\":true}|outer.next,inner.next,inner.return:0:true,inner.then,outer.return:0:true,second:{\"done\":true}" },
        { "thenable", "rejects", "rejected:RangeError:outer.r|outer.next,inner.next,inner.return:0:true,inner.then,outer.return:0:true" },
        { "thenable", "number", "rejected:TypeError|outer.next,inner.next,inner.return:0:true,inner.then,outer.return:0:true" },
        { "thenable", "missing", "fulfilled:{\"done\":true}|outer.next,inner.next,inner.return:0:true,inner.then,second:{\"done\":true}" },
        { "throws", "object", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "throws", "rejects", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "throws", "number", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "throws", "missing", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true" },
        { "rejects", "object", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "rejects", "rejects", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "rejects", "number", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "rejects", "missing", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true" },
        { "thenableRejects", "object", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,inner.then,outer.return:0:true" },
        { "thenableRejects", "rejects", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,inner.then,outer.return:0:true" },
        { "thenableRejects", "number", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,inner.then,outer.return:0:true" },
        { "thenableRejects", "missing", "rejected:RangeError:inner.r|outer.next,inner.next,inner.return:0:true,inner.then" },
        { "number", "object", "rejected:TypeError|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "number", "rejects", "rejected:TypeError|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "number", "number", "rejected:TypeError|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "number", "missing", "rejected:TypeError|outer.next,inner.next,inner.return:0:true" },
        { "undefined", "object", "rejected:TypeError|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "undefined", "rejects", "rejected:TypeError|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "undefined", "number", "rejected:TypeError|outer.next,inner.next,inner.return:0:true,outer.return:0:true" },
        { "undefined", "missing", "rejected:TypeError|outer.next,inner.next,inner.return:0:true" },
        { "missing", "object", "fulfilled:{\"done\":true}|outer.next,inner.next,outer.return:0:true,second:{\"done\":true}" },
        { "missing", "rejects", "rejected:RangeError:outer.r|outer.next,inner.next,outer.return:0:true" },
        { "missing", "number", "rejected:TypeError|outer.next,inner.next,outer.return:0:true" },
        { "missing", "missing", "fulfilled:{\"done\":true}|outer.next,inner.next,second:{\"done\":true}" },
        { "getterThrows", "object", "rejected:RangeError:inner.g|outer.next,inner.next,inner.get,outer.return:0:true" },
        { "getterThrows", "rejects", "rejected:RangeError:inner.g|outer.next,inner.next,inner.get,outer.return:0:true" },
        { "getterThrows", "number", "rejected:RangeError:inner.g|outer.next,inner.next,inner.get,outer.return:0:true" },
        { "getterThrows", "missing", "rejected:RangeError:inner.g|outer.next,inner.next,inner.get" },
    };

    /// <summary>
    /// flatMap's <c>return()</c> with an inner iterator open closes the inner one first and awaits it; only then is the
    /// outer one closed: with the return completion when the inner close completed, and with the inner close's error,
    /// which then wins, when it did not. Jint closed the inner iterator synchronously, so a rejection or a non-object
    /// answer from its <c>return</c> was lost.
    /// </summary>
    [TestCaseSource(nameof(FlatMapReturnRows))]
    public void FlatMapsReturnClosesTheInnerIteratorFirstAndAwaitsIt(string innerHow, string outerHow, string expected)
    {
        Run($"flatMapReturn('{innerHow}', '{outerHow}', false)").Should().Be(expected);
    }

    /// <summary>
    /// The control: tc39/proposal-async-iterator-helpers#30's nested <c>for await</c>, run through Jint's own
    /// <c>for await</c> and async generator, answers every row the same way.
    /// </summary>
    [TestCaseSource(nameof(FlatMapReturnRows))]
    public void ANestedForAwaitClosesInTheSameOrder(string innerHow, string outerHow, string expected)
    {
        Run($"flatMapReturn('{innerHow}', '{outerHow}', true)").Should().Be(expected);
    }

    /// <summary>
    /// A second <c>return()</c> while the first is still awaiting the inner iterator's close resolves at once, as one on
    /// a completed helper does, and the source is closed only after the inner close has settled. The first used to leave
    /// the source open for the second, which closed it before the inner close had settled and resolved ahead of it.
    /// core-js 3.51's order.
    /// </summary>
    [Test]
    public void ASecondReturnDoesNotCloseTheSourceAheadOfTheInnerClose()
    {
        Run("flatMapReturnTwice()").Should().Be("inner.return,b,inner.settled,outer.return,a");
    }

    /// <summary>
    /// A <c>return()</c> that comes in while a step is in flight completes the helper for good: the step still answers
    /// its value, the source is closed once, and a later <c>next()</c> answers done. The step used to put the helper
    /// back into its suspended state, so that <c>next()</c> stepped the closed source again. core-js 3.51's answers; the
    /// async generator is the native control.
    /// </summary>
    [TestCase("map")]
    [TestCase("filter")]
    [TestCase("take")]
    [TestCase("drop")]
    [TestCase("flatMap")]
    [TestCase("generator")]
    public void AReturnDuringAStepCompletesTheHelperForGood(string name)
    {
        Run($"returnDuringStep('{name}')").Should().Be("p1:{\"value\":1,\"done\":false}|p2:{\"done\":true}|p3:{\"done\":true}|next1,return");
    }

    /// <summary>
    /// The Await of flatMap's inner step reads a promise's <c>constructor</c> (PromiseResolve) and it throws:
    /// IfAbruptCloseAsyncIterator closes the outer iterator and the helper is completed, so a later <c>return()</c>
    /// closes nothing. Jint kept the inner iterator and called its <c>return</c>. With no successful inner step first the
    /// failure is in the mapper's continuation, with one it is in a later <c>next()</c>.
    /// </summary>
    [TestCase("0", "outer.next,inner.next,constructor,outer.return,caught:RangeError:c,return:{\"done\":true}")]
    [TestCase("1", "outer.next,inner.next,value:i,inner.next,constructor,outer.return,caught:RangeError:c,return:{\"done\":true}")]
    public void AFlatMapInnerStepWhoseAwaitThrowsLeavesNothingForReturnToClose(string good, string expected)
    {
        Run($"flatMapInnerResolveThrows({good})").Should().Be(expected);
    }

    /// <summary>
    /// The microtask order. A tick ladder starts inside <c>return</c>, so where the helper settles among its ticks is
    /// how many jobs the close took: one Await of what <c>return</c> answered (two jobs for a thenable, the second after
    /// its <c>then</c> ran), and then the settlement. The consuming methods match core-js's order and a native
    /// <c>for await</c>'s exactly. For <c>take</c> and <c>map</c>, which are helper objects, the expected values are
    /// the proposal's: the closure's return completes the helper's step at once (AsyncGeneratorCompleteStep), and core-js
    /// settles one job later, because its helper <c>next</c> wraps every step's promise in one more <c>then</c>.
    /// </summary>
    [TestCase("take", "promiseOfObject", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("take", "object", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("take", "thenable", "return:0:true,t0,then,t1,t2,fulfilled,t3,t4,t5,t6,t7")]
    [TestCase("take", "rejects", "return:0:true,t0,t1,rejected:RangeError:r,t2,t3,t4,t5,t6,t7")]
    [TestCase("take", "promiseOfNumber", "return:0:true,t0,t1,rejected:TypeError,t2,t3,t4,t5,t6,t7")]
    [TestCase("some", "promiseOfObject", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("some", "object", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("some", "thenable", "return:0:true,t0,then,t1,t2,fulfilled,t3,t4,t5,t6,t7")]
    [TestCase("some", "rejects", "return:0:true,t0,t1,rejected:RangeError:r,t2,t3,t4,t5,t6,t7")]
    [TestCase("some", "promiseOfNumber", "return:0:true,t0,t1,rejected:TypeError,t2,t3,t4,t5,t6,t7")]
    [TestCase("every", "promiseOfObject", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("every", "object", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("every", "thenable", "return:0:true,t0,then,t1,t2,fulfilled,t3,t4,t5,t6,t7")]
    [TestCase("every", "rejects", "return:0:true,t0,t1,rejected:RangeError:r,t2,t3,t4,t5,t6,t7")]
    [TestCase("every", "promiseOfNumber", "return:0:true,t0,t1,rejected:TypeError,t2,t3,t4,t5,t6,t7")]
    [TestCase("find", "promiseOfObject", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("find", "object", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("find", "thenable", "return:0:true,t0,then,t1,t2,fulfilled,t3,t4,t5,t6,t7")]
    [TestCase("find", "rejects", "return:0:true,t0,t1,rejected:RangeError:r,t2,t3,t4,t5,t6,t7")]
    [TestCase("find", "promiseOfNumber", "return:0:true,t0,t1,rejected:TypeError,t2,t3,t4,t5,t6,t7")]
    [TestCase("forAwait", "promiseOfObject", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("forAwait", "object", "return:0:true,t0,t1,fulfilled,t2,t3,t4,t5,t6,t7")]
    [TestCase("forAwait", "thenable", "return:0:true,t0,then,t1,t2,fulfilled,t3,t4,t5,t6,t7")]
    [TestCase("forAwait", "rejects", "return:0:true,t0,t1,rejected:RangeError:r,t2,t3,t4,t5,t6,t7")]
    [TestCase("forAwait", "promiseOfNumber", "return:0:true,t0,t1,rejected:TypeError,t2,t3,t4,t5,t6,t7")]
    [TestCase("map", "promiseOfObject", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("map", "object", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("map", "thenable", "return:0:true,t0,then,t1,t2,rejected:SyntaxError:cb,t3,t4,t5,t6,t7")]
    [TestCase("map", "rejects", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("map", "promiseOfNumber", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("forEach", "promiseOfObject", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("forEach", "object", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("forEach", "thenable", "return:0:true,t0,then,t1,t2,rejected:SyntaxError:cb,t3,t4,t5,t6,t7")]
    [TestCase("forEach", "rejects", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("forEach", "promiseOfNumber", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("reduce", "promiseOfObject", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("reduce", "object", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("reduce", "thenable", "return:0:true,t0,then,t1,t2,rejected:SyntaxError:cb,t3,t4,t5,t6,t7")]
    [TestCase("reduce", "rejects", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("reduce", "promiseOfNumber", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("forAwaitThrow", "promiseOfObject", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("forAwaitThrow", "object", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("forAwaitThrow", "thenable", "return:0:true,t0,then,t1,t2,rejected:SyntaxError:cb,t3,t4,t5,t6,t7")]
    [TestCase("forAwaitThrow", "rejects", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    [TestCase("forAwaitThrow", "promiseOfNumber", "return:0:true,t0,t1,rejected:SyntaxError:cb,t2,t3,t4,t5,t6,t7")]
    public void TheCloseSettlesAfterTheAwaitOfWhatReturnAnswered(string name, string how, string expected)
    {
        Run($"order('{name}', '{how}')").Should().Be(expected);
    }
}
