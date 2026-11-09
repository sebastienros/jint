namespace Jint.Tests.Runtime;

/// <summary>
/// Pins <a href="https://tc39.es/ecma262/#sec-getiterator">GetIterator</a>(obj, async) for a
/// <c>for await…of</c> loop whose iterable is a built-in iterator object — <c>arr.values()</c>,
/// <c>map.entries()</c>, <c>set.values()</c>, a string iterator, <c>matchAll</c> (sebastienros/jint#4392).
/// <para>
/// Such an object has no <c>@@asyncIterator</c>, so GetIterator takes the sync iterator its <c>@@iterator</c>
/// hands back and wraps it in <a href="https://tc39.es/ecma262/#sec-createasyncfromsynciterator">CreateAsyncFromSyncIterator</a>.
/// Every step then goes through
/// <a href="https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.next">%AsyncFromSyncIteratorPrototype%.next</a>,
/// which awaits the step's value, and every close through
/// <a href="https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.return">%AsyncFromSyncIteratorPrototype%.return</a>,
/// whose promise <a href="https://tc39.es/ecma262/#sec-asynciteratorclose">AsyncIteratorClose</a> awaits.
/// </para>
/// <para>
/// Jint adopted a built-in iterator as the loop's record without asking GetIterator, as the sync kind does,
/// and stepped it directly. So neither await happened: the loop ran a microtask early per step and per close,
/// an iterated promise reached the body unawaited, an own <c>return</c> was never called, and an own
/// <c>@@asyncIterator</c> or <c>@@iterator</c> was never read. A plain array, a string, a generator and a
/// user iterable already went through GetIterator; they are the controls. Every expected value is Node 26's.
/// </para>
/// </summary>
public class ForAwaitBuiltInIteratorTests
{
    private const string Harness = """
        // Starts a chain of eight microtasks logging t1..t8, then `start(log)` beside it, and answers the log
        // once both are over. Where an entry lands among the ticks is how many microtasks it took to get there.
        function interleave(start) {
            const log = [];
            let p = Promise.resolve();
            for (let i = 1; i <= 8; i++) { const n = i; p = p.then(function () { log.push('t' + n); }); }
            const done = start(log);
            return Promise.all([done, p]).then(function () { return log.join(' '); });
        }

        function describe(e) { return e instanceof Error ? e.name : String(e); }

        // The four ways a for await loop ends: running out, break, a throw and a return from the body.
        const bodies = {
            normal: async function (it, log) {
                try { for await (const x of it) { log.push('x' + String(x)); } } catch (e) { log.push('caught:' + describe(e)); }
                log.push('after');
            },
            break: async function (it, log) {
                try { for await (const x of it) { log.push('x' + String(x)); break; } } catch (e) { log.push('caught:' + describe(e)); }
                log.push('after');
            },
            throw: async function (it, log) {
                try { for await (const x of it) { log.push('x' + String(x)); throw 'E'; } } catch (e) { log.push('caught:' + describe(e)); }
                log.push('after');
            },
            return: function (it, log) {
                return (async function () {
                    try { for await (const x of it) { log.push('x' + String(x)); return; } }
                    finally { log.push('after'); }
                })().catch(function (e) { log.push('caught:' + describe(e)); });
            }
        };

        function run(source, body) {
            return interleave(function (log) { return bodies[body](source, log); });
        }

        // The same loop in an async generator, a yield in its body and three next() requests pulling on it.
        function runInAsyncGenerator(source) {
            return interleave(function (log) {
                async function* g() { for await (const x of source) { log.push('x' + String(x)); yield x; } log.push('end'); }
                const it = g();
                return Promise.all([
                    it.next().then(function (r) { log.push('r' + r.value); }),
                    it.next().then(function (r) { log.push('r' + r.value); }),
                    it.next().then(function (r) { log.push('done:' + r.done); })
                ]);
            });
        }

        // An async generator suspended at the yield inside a for await loop over `makeSource(log)`, then asked
        // to return(5), with the microtask chain started just before the request.
        function returnAtYield(makeSource, braced) {
            const log = [];
            const source = makeSource(log);
            const g = braced
                ? async function* () { try { for await (const x of source) { log.push('x' + String(x)); yield x; } } finally { log.push('finally'); } }
                : async function* () { try { for await (const x of source) yield x; } finally { log.push('finally'); } };
            const it = g();
            return it.next().then(function () {
                let p = Promise.resolve();
                for (let i = 1; i <= 8; i++) { const n = i; p = p.then(function () { log.push('t' + n); }); }
                const done = it.return(5).then(function (r) { log.push('ret:' + r.value + ':' + r.done); });
                log.push('called');
                return Promise.all([done, p]);
            }).then(function () { return log.join(' '); });
        }

        // Gives a built-in iterator an own return(), which a close reaches only through the wrapper's return().
        function withOwnReturn(iterator, log, answer) {
            iterator.return = function () { log.push('return:' + (this === iterator)); return answer(); };
            return iterator;
        }
        """;

    private static string Run(string script) =>
        new Engine().Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    /// <summary>
    /// Each built-in iterator kind, under each of the four ways the loop ends. A step costs two microtasks
    /// (the wrapper's await of the value, then the loop's await of the wrapper's promise) and a close one.
    /// </summary>
    [TestCase("[1, 2].values()", "normal", "t1 t2 x1 t3 t4 x2 t5 t6 after t7 t8")]
    [TestCase("[1, 2].values()", "break", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].values()", "throw", "t1 t2 x1 t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].values()", "return", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].keys()", "normal", "t1 t2 x0 t3 t4 x1 t5 t6 after t7 t8")]
    [TestCase("[1, 2].keys()", "break", "t1 t2 x0 t3 after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].keys()", "throw", "t1 t2 x0 t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].keys()", "return", "t1 t2 x0 t3 after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].entries()", "normal", "t1 t2 x0,1 t3 t4 x1,2 t5 t6 after t7 t8")]
    [TestCase("[1, 2].entries()", "break", "t1 t2 x0,1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].entries()", "throw", "t1 t2 x0,1 t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].entries()", "return", "t1 t2 x0,1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("new Uint8Array([1, 2]).values()", "normal", "t1 t2 x1 t3 t4 x2 t5 t6 after t7 t8")]
    [TestCase("new Uint8Array([1, 2]).values()", "break", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("new Uint8Array([1, 2]).values()", "throw", "t1 t2 x1 t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("new Uint8Array([1, 2]).values()", "return", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("new Map([[1, 'a']]).entries()", "normal", "t1 t2 x1,a t3 t4 after t5 t6 t7 t8")]
    [TestCase("new Map([[1, 'a']]).entries()", "break", "t1 t2 x1,a t3 after t4 t5 t6 t7 t8")]
    [TestCase("new Map([[1, 'a']]).entries()", "throw", "t1 t2 x1,a t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("new Map([[1, 'a']]).entries()", "return", "t1 t2 x1,a t3 after t4 t5 t6 t7 t8")]
    [TestCase("new Set([1, 2]).values()", "normal", "t1 t2 x1 t3 t4 x2 t5 t6 after t7 t8")]
    [TestCase("new Set([1, 2]).values()", "break", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("new Set([1, 2]).values()", "throw", "t1 t2 x1 t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("new Set([1, 2]).values()", "return", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("'ab'[Symbol.iterator]()", "normal", "t1 t2 xa t3 t4 xb t5 t6 after t7 t8")]
    [TestCase("'ab'[Symbol.iterator]()", "break", "t1 t2 xa t3 after t4 t5 t6 t7 t8")]
    [TestCase("'ab'[Symbol.iterator]()", "throw", "t1 t2 xa t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("'ab'[Symbol.iterator]()", "return", "t1 t2 xa t3 after t4 t5 t6 t7 t8")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "normal", "t1 t2 x1 t3 t4 x2 t5 t6 after t7 t8")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "break", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "throw", "t1 t2 x1 t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "return", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    public void ABuiltInIteratorIsSteppedAndClosedThroughTheAsyncFromSyncWrapper(string source, string body, string expected)
    {
        Run($"run({source}, '{body}')").Should().Be(expected);
    }

    /// <summary>
    /// The iterables that already reached GetIterator: a plain array and a string (whose built-in iterator
    /// is created inside it), a generator object (whose own <c>return</c> adds a tick to the close), a user
    /// iterable, and an iterator helper, which is not a built-in iterator instance.
    /// </summary>
    [TestCase("[1, 2]", "normal", "t1 t2 x1 t3 t4 x2 t5 t6 after t7 t8")]
    [TestCase("[1, 2]", "break", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2]", "throw", "t1 t2 x1 t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2]", "return", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("'ab'", "normal", "t1 t2 xa t3 t4 xb t5 t6 after t7 t8")]
    [TestCase("'ab'", "break", "t1 t2 xa t3 after t4 t5 t6 t7 t8")]
    [TestCase("'ab'", "throw", "t1 t2 xa t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("'ab'", "return", "t1 t2 xa t3 after t4 t5 t6 t7 t8")]
    [TestCase("(function* () { yield 1; yield 2; })()", "normal", "t1 t2 x1 t3 t4 x2 t5 t6 after t7 t8")]
    [TestCase("(function* () { yield 1; yield 2; })()", "break", "t1 t2 x1 t3 t4 after t5 t6 t7 t8")]
    [TestCase("(function* () { yield 1; yield 2; })()", "throw", "t1 t2 x1 t3 t4 caught:E after t5 t6 t7 t8")]
    [TestCase("(function* () { yield 1; yield 2; })()", "return", "t1 t2 x1 t3 t4 after t5 t6 t7 t8")]
    [TestCase("({ [Symbol.iterator]() { let i = 0; return { next() { return i < 2 ? { value: ++i, done: false } : { value: undefined, done: true }; } }; } })", "normal", "t1 t2 x1 t3 t4 x2 t5 t6 after t7 t8")]
    [TestCase("({ [Symbol.iterator]() { let i = 0; return { next() { return i < 2 ? { value: ++i, done: false } : { value: undefined, done: true }; } }; } })", "break", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("({ [Symbol.iterator]() { let i = 0; return { next() { return i < 2 ? { value: ++i, done: false } : { value: undefined, done: true }; } }; } })", "throw", "t1 t2 x1 t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("({ [Symbol.iterator]() { let i = 0; return { next() { return i < 2 ? { value: ++i, done: false } : { value: undefined, done: true }; } }; } })", "return", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].values().map(function (x) { return x * 10; })", "normal", "t1 t2 x10 t3 t4 x20 t5 t6 after t7 t8")]
    [TestCase("[1, 2].values().map(function (x) { return x * 10; })", "break", "t1 t2 x10 t3 t4 after t5 t6 t7 t8")]
    [TestCase("[1, 2].values().map(function (x) { return x * 10; })", "throw", "t1 t2 x10 t3 t4 caught:E after t5 t6 t7 t8")]
    [TestCase("[1, 2].values().map(function (x) { return x * 10; })", "return", "t1 t2 x10 t3 t4 after t5 t6 t7 t8")]
    public void AnIterableThatAlreadyReachedGetIteratorIsUnchanged(string source, string body, string expected)
    {
        Run($"run({source}, '{body}')").Should().Be(expected);
    }

    /// <summary>
    /// <a href="https://tc39.es/ecma262/#sec-asyncfromsynciteratorcontinuation">AsyncFromSyncIteratorContinuation</a>
    /// resolves each step's value, so the body sees what a promise fulfils with — and a rejection is the
    /// loop's throw, which a <c>return</c> body's <c>finally</c> sees before its caller does.
    /// </summary>
    [TestCase("[Promise.resolve(1), Promise.resolve(2)].values()", "normal", "t1 t2 x1 t3 t4 x2 t5 t6 after t7 t8")]
    [TestCase("[Promise.resolve(1), Promise.resolve(2)].values()", "break", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("[Promise.resolve(1), Promise.resolve(2)].values()", "throw", "t1 t2 x1 t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("[Promise.resolve(1), Promise.resolve(2)].values()", "return", "t1 t2 x1 t3 after t4 t5 t6 t7 t8")]
    [TestCase("new Map([[1, Promise.resolve('a')]]).values()", "normal", "t1 t2 xa t3 t4 after t5 t6 t7 t8")]
    [TestCase("new Map([[1, Promise.resolve('a')]]).values()", "break", "t1 t2 xa t3 after t4 t5 t6 t7 t8")]
    [TestCase("new Map([[1, Promise.resolve('a')]]).values()", "throw", "t1 t2 xa t3 caught:E after t4 t5 t6 t7 t8")]
    [TestCase("new Map([[1, Promise.resolve('a')]]).values()", "return", "t1 t2 xa t3 after t4 t5 t6 t7 t8")]
    [TestCase("[Promise.reject('R'), 2].values()", "normal", "t1 t2 caught:R after t3 t4 t5 t6 t7 t8")]
    [TestCase("[Promise.reject('R'), 2].values()", "break", "t1 t2 caught:R after t3 t4 t5 t6 t7 t8")]
    [TestCase("[Promise.reject('R'), 2].values()", "throw", "t1 t2 caught:R after t3 t4 t5 t6 t7 t8")]
    [TestCase("[Promise.reject('R'), 2].values()", "return", "t1 t2 after t3 caught:R t4 t5 t6 t7 t8")]
    public void AnIteratedPromiseIsAwaitedBeforeTheBodySeesIt(string source, string body, string expected)
    {
        Run($"run({source}, '{body}')").Should().Be(expected);
    }

    /// <summary>
    /// The same loop in an async generator, with a <c>yield</c> in its body: the built-in iterator keeps the
    /// plain array's interleaving there too.
    /// </summary>
    [TestCase("[1, 2]", "t1 t2 x1 t3 t4 r1 t5 x2 t6 t7 r2 t8 end done:true")]
    [TestCase("[1, 2].values()", "t1 t2 x1 t3 t4 r1 t5 x2 t6 t7 r2 t8 end done:true")]
    [TestCase("new Set([1, 2]).values()", "t1 t2 x1 t3 t4 r1 t5 x2 t6 t7 r2 t8 end done:true")]
    [TestCase("[Promise.resolve(1), Promise.resolve(2)].values()", "t1 t2 x1 t3 t4 r1 t5 x2 t6 t7 r2 t8 end done:true")]
    public void AnAsyncGeneratorYieldingFromTheLoopInterleavesAsTheArrayDoes(string source, string expected)
    {
        Run($"runInAsyncGenerator({source})").Should().Be(expected);
    }

    /// <summary>
    /// An async generator's <c>return()</c> that resumes the <c>yield</c> in the loop's body closes the loop
    /// through AsyncIteratorClose, for a braced body and for a bare <c>yield</c> body alike.
    /// </summary>
    [TestCase("[1, 2]", true, "x1 called t1 t2 finally t3 ret:5:true t4 t5 t6 t7 t8")]
    [TestCase("[1, 2]", false, "called t1 t2 finally t3 ret:5:true t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].values()", true, "x1 called t1 t2 finally t3 ret:5:true t4 t5 t6 t7 t8")]
    [TestCase("[1, 2].values()", false, "called t1 t2 finally t3 ret:5:true t4 t5 t6 t7 t8")]
    [TestCase("new Set([1, 2]).values()", true, "x1 called t1 t2 finally t3 ret:5:true t4 t5 t6 t7 t8")]
    [TestCase("new Set([1, 2]).values()", false, "called t1 t2 finally t3 ret:5:true t4 t5 t6 t7 t8")]
    [TestCase("[Promise.resolve(1), Promise.resolve(2)].values()", true, "x1 called t1 t2 finally t3 ret:5:true t4 t5 t6 t7 t8")]
    [TestCase("[Promise.resolve(1), Promise.resolve(2)].values()", false, "called t1 t2 finally t3 ret:5:true t4 t5 t6 t7 t8")]
    public void AnAsyncGeneratorReturnAtTheYieldClosesTheLoop(string source, bool braced, string expected)
    {
        Run($"returnAtYield(function () {{ return {source}; }}, {(braced ? "true" : "false")})").Should().Be(expected);
    }

    /// <summary>
    /// Step 6 of %AsyncFromSyncIteratorPrototype%.return reads the sync iterator's <c>return</c>, so one
    /// the script gave a built-in iterator is called with it as the receiver, its answer is awaited, a throw
    /// from it or a non-object answer rejects the close, and the loop takes that rejection (AsyncIteratorClose
    /// step 6) unless it already carries a throw (step 5).
    /// </summary>
    [TestCase("bodies.break(withOwnReturn([1, 2].values(), log, function () { return {}; }), log)", "t1 t2 x1 return:true t3 t4 after t5 t6 t7 t8")]
    [TestCase("bodies.throw(withOwnReturn(new Set([1, 2]).values(), log, function () { return { value: Promise.resolve('v'), done: true }; }), log)", "t1 t2 x1 return:true t3 t4 caught:E after t5 t6 t7 t8")]
    [TestCase("bodies.break(withOwnReturn([1, 2].values(), log, function () { throw 'C'; }), log)", "t1 t2 x1 return:true t3 caught:C after t4 t5 t6 t7 t8")]
    [TestCase("bodies.break(withOwnReturn([1, 2].values(), log, function () { return 1; }), log)", "t1 t2 x1 return:true t3 caught:TypeError after t4 t5 t6 t7 t8")]
    public void AnOwnReturnOnABuiltInIteratorIsCalledAndAwaited(string start, string expected)
    {
        Run($"interleave(function (log) {{ return {start}; }})").Should().Be(expected);
    }

    /// <summary>
    /// The same own <c>return</c>, reached by an async generator's <c>return()</c> at the <c>yield</c>.
    /// </summary>
    [TestCase(true, "x1 called t1 return:true t2 t3 finally t4 ret:5:true t5 t6 t7 t8")]
    [TestCase(false, "called t1 return:true t2 t3 finally t4 ret:5:true t5 t6 t7 t8")]
    public void AnAsyncGeneratorReturnAtTheYieldCallsTheBuiltInIteratorsOwnReturn(bool braced, string expected)
    {
        Run($"returnAtYield(function (log) {{ return withOwnReturn(new Set([1, 2]).values(), log, function () {{ return {{}}; }}); }}, {(braced ? "true" : "false")})")
            .Should().Be(expected);
    }

    /// <summary>
    /// GetIterator step 1.a reads <c>@@asyncIterator</c> first, so one the script gave a built-in iterator
    /// is the loop's iterator, and the wrapper is never built.
    /// </summary>
    [Test]
    public void AnOwnAsyncIteratorOnABuiltInIteratorIsUsed()
    {
        Run("""
            interleave(function (log) {
                const it = [1, 2].values();
                it[Symbol.asyncIterator] = function () { log.push('asyncIterator'); let n = 0; return { next() { return Promise.resolve({ value: 'A', done: n++ > 1 }); } }; };
                return bodies.normal(it, log);
            })
            """).Should().Be("asyncIterator t1 xA t2 xA t3 after t4 t5 t6 t7 t8");
    }

    /// <summary>
    /// Without one, step 1.b calls <c>@@iterator</c> and wraps what that answers, which need not be the
    /// object the loop was given.
    /// </summary>
    [Test]
    public void AnOwnIteratorMethodOnABuiltInIteratorIsCalled()
    {
        Run("""
            interleave(function (log) {
                const it = [1, 2].values();
                it[Symbol.iterator] = function () { log.push('iterator'); return [9].values(); };
                return bodies.normal(it, log);
            })
            """).Should().Be("iterator t1 t2 x9 t3 t4 after t5 t6 t7 t8");
    }

    private const string DisposalHarness = """
        // The ways a loop over `items` can be left early, each answering what it saw. A sync for-of is the control:
        // its IteratorClose reaches the CLR iterator's own close, which disposes the host enumerator.
        const leave = {
            forOfBreak: async function () { const log = []; for (const x of items) { log.push(x); break; } return log.join(','); },
            forOfThrow: async function () { const log = []; try { for (const x of items) { log.push(x); throw 'E'; } } catch (e) { log.push('caught:' + e); } return log.join(','); },
            forAwaitBreak: async function () { const log = []; for await (const x of items) { log.push(x); break; } return log.join(','); },
            forAwaitThrow: async function () { const log = []; try { for await (const x of items) { log.push(x); throw 'E'; } } catch (e) { log.push('caught:' + e); } return log.join(','); },
            forAwaitReturn: async function () { const log = []; await (async function () { for await (const x of items) { log.push(x); return; } })(); return log.join(','); },
            // An async generator's return() at the yield in its loop closes the loop through AsyncIteratorClose.
            asyncGeneratorReturn: async function () {
                async function* g() { for await (const x of items) { yield x; } }
                const it = g();
                const first = await it.next();
                const last = await it.return('R');
                return first.value + ',' + last.value + ':' + last.done;
            },
            // yield* in an async generator delegates to the wrapper, and return() forwards to the wrapper's return().
            yieldStarReturn: async function () {
                async function* g() { yield* items; }
                const it = g();
                const first = await it.next();
                const last = await it.return('R');
                return first.value + ',' + last.value + ':' + last.done;
            },
            // Array.fromAsync closes the iterator it is reading when the mapper throws.
            fromAsyncMapperThrows: async function () {
                try { await Array.fromAsync(items, function () { throw 'M'; }); return 'no throw'; } catch (e) { return 'caught:' + e; }
            },
            // %ArrayIteratorPrototype%.next replaced: the wrapper's sync iterator record is an ObjectIterator over the
            // CLR iterator, as it is for a sync for-of in ReplacedIteratorNextTests.
            forAwaitBreakReplacedNext: async function () {
                const proto = Object.getPrototypeOf([].values());
                const original = proto.next;
                proto.next = function () { return original.call(this); };
                try { const log = []; for await (const x of items) { log.push(x); break; } return log.join(','); }
                finally { proto.next = original; }
            },
            // An @@asyncIterator answering the CLR iterator itself, which the loop then steps as its record, and whose
            // close finds no return() to call - with the iterator's next untouched, and replaced.
            asyncIteratorIsTheClrIterator: async function () {
                const source = { [Symbol.asyncIterator]() { return items[Symbol.iterator](); } };
                const log = [];
                for await (const x of source) { log.push(x); break; }
                return log.join(',');
            },
            asyncIteratorIsTheClrIteratorReplacedNext: async function () {
                const proto = Object.getPrototypeOf([].values());
                const original = proto.next;
                proto.next = function () { return original.call(this); };
                try { return await leave.asyncIteratorIsTheClrIterator(); }
                finally { proto.next = original; }
            }
        };
        """;

    /// <summary>
    /// <a href="https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.return">%AsyncFromSyncIteratorPrototype%.return</a>
    /// finds no <c>return</c> on a CLR collection's iterator and resolves a done result, which is all the script sees.
    /// But that iterator holds a host enumerator that only its native close disposes, and the sync <c>for…of</c>'s
    /// IteratorClose reaches it; the wrapper's <c>return()</c> did not, so a <c>for await</c> left early never
    /// disposed it (sebastienros/jint#4408), and neither did the close of a loop whose <c>@@asyncIterator</c> answered
    /// the CLR iterator itself. Node has no counterpart: the expected values are the sync loop's, checked in Node 26
    /// with an array in place of the CLR collection.
    /// </summary>
    [TestCase("forOfBreak", "1")]
    [TestCase("forOfThrow", "1,caught:E")]
    [TestCase("forAwaitBreak", "1")]
    [TestCase("forAwaitThrow", "1,caught:E")]
    [TestCase("forAwaitReturn", "1")]
    [TestCase("asyncGeneratorReturn", "1,R:true")]
    [TestCase("yieldStarReturn", "1,R:true")]
    [TestCase("fromAsyncMapperThrows", "caught:M")]
    [TestCase("forAwaitBreakReplacedNext", "1")]
    [TestCase("asyncIteratorIsTheClrIterator", "1")]
    [TestCase("asyncIteratorIsTheClrIteratorReplacedNext", "1")]
    public void LeavingALoopOverAClrEnumerableEarlyDisposesItsEnumerator(string way, string expected)
    {
        var items = new CountingEnumerable();
        var engine = new Engine();
        engine.SetValue("items", items);

        engine.Evaluate(DisposalHarness + "\n" + $"leave.{way}()").UnwrapIfPromise().AsString().Should().Be(expected);
        items.Disposals.Should().Be(1);
    }

    /// <summary>A host collection of 1, 2 and 3 whose enumerators count their <see cref="IDisposable.Dispose"/> calls.</summary>
    private sealed class CountingEnumerable : IEnumerable<int>
    {
        public int Disposals;

        public IEnumerator<int> GetEnumerator() => new Enumerator(this);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator(CountingEnumerable owner) : IEnumerator<int>
        {
            private int _current;

            public int Current => _current;

            object System.Collections.IEnumerator.Current => _current;

            public bool MoveNext()
            {
                if (_current >= 3)
                {
                    return false;
                }

                _current++;
                return true;
            }

            public void Reset() => _current = 0;

            public void Dispose() => owner.Disposals++;
        }
    }

    /// <summary>
    /// A sync <c>for…of</c> over the same objects awaits nothing: it hands a promise to its body as it is,
    /// and runs to completion before the first microtask.
    /// </summary>
    [Test]
    public void ASyncForOfOverTheSameIteratorsIsUnchanged()
    {
        Run("""
            interleave(function (log) {
                return (async function () {
                    for (const x of [Promise.resolve(1)].values()) log.push(typeof x);
                    for (const x of new Map([[1, 2]]).entries()) log.push(String(x));
                    for (const x of new Set([3]).values()) log.push(String(x));
                    for (const x of 'ab'[Symbol.iterator]()) log.push(x);
                    log.push('sync-done');
                })();
            })
            """).Should().Be("object 1,2 3 a b sync-done t1 t2 t3 t4 t5 t6 t7 t8");
    }
}
