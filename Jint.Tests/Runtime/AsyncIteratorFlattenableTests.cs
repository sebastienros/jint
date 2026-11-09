#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins GetIteratorFlattenable with the <c>async</c> hint, the operation two async iterator helpers build their record
/// with: the inner iterator of
/// <a href="https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.flatmap">AsyncIterator.prototype.flatMap</a>,
/// and the argument of <a href="https://tc39.es/proposal-async-iterator-helpers/#sec-asynciterator.from">AsyncIterator.from</a>.
/// Those calls were written against the operation as it was before the async helpers were split out of
/// tc39/proposal-iterator-helpers (at ef4471f), when it took a <c>sync</c>/<c>async</c> hint; the page the link reaches
/// now renders the sync-only form (tc39/proposal-async-iterator-helpers#19). In that version an object with no
/// <c>@@asyncIterator</c> but an <c>@@iterator</c> is wrapped by
/// <a href="https://tc39.es/ecma262/#sec-createasyncfromsynciterator">CreateAsyncFromSyncIterator</a>, so each value it
/// answers is awaited, and <c>AsyncIterator.from</c> converts a string with <c>ToObject</c> before it gets there.
/// <para>
/// flatMap stepped a sync inner iterator directly, so a promise it held came out as the value instead of what the promise
/// settled to, and <c>AsyncIterator.from</c> threw a <c>TypeError</c> for a string (sebastienros/jint#4439). Each case
/// runs the helper beside an async generator doing the same with <c>yield*</c> or <c>for await</c>, which already go
/// through CreateAsyncFromSyncIterator and are the control. Expected values are core-js 3.51's on Node 26, which has
/// no native <c>AsyncIterator</c>.
/// </para>
/// </summary>
public class AsyncIteratorFlattenableTests
{
    private const string Harness = """
        function describe(e) { return e instanceof Error ? e.name : String(e); }
        function show(v) { return v instanceof Promise ? 'promise' : typeof v === 'string' ? v : String(v); }

        // Steps an async iterator through its own next() to the end, answering its values joined by ','.
        async function drain(it) {
            const out = [];
            for (;;) { const r = await it.next(); if (r.done) return out.map(show).join(','); out.push(r.value); }
        }

        // A sync iterable holding Promise.resolve(x) and then x * 10: an array, a generator, or a hand-written iterable.
        function syncInner(shape, x) {
            switch (shape) {
                case 'array': return [Promise.resolve(x), x * 10];
                case 'generator': return (function* () { yield Promise.resolve(x); yield x * 10; })();
                case 'iterable': return {
                    [Symbol.iterator]() {
                        let j = 0;
                        return { next() { j++; return { value: j === 1 ? Promise.resolve(x) : x * 10, done: j > 2 }; } };
                    }
                };
            }
        }

        // flatMap over 1, 2 with `mapper`, or the async generator that does the same with yield*.
        function flatten(how, mapper) {
            if (how === 'flatMap') return AsyncIterator.from([1, 2]).flatMap(mapper);
            return (async function* () { for await (const x of [1, 2]) yield* mapper(x); })();
        }

        // AsyncIterator.from(s), or the async generator that iterates s with for await.
        function fromString(how, s) {
            if (how === 'from') return AsyncIterator.from(s);
            return (async function* () { for await (const c of s) yield c; })();
        }
        """;

    private static readonly string[] _flatteners = ["flatMap", "yieldStar"];

    private static readonly string[] _syncShapes = ["array", "generator", "iterable"];

    private static readonly string[] _stringReaders = ["from", "forAwait"];

    private static string Run(string script) =>
        new Engine().Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    /// <summary>
    /// A sync inner iterable's values are awaited: a promise it holds comes out as what it settled to.
    /// </summary>
    [Test]
    public void ASyncInnerIterablesPromisesAreAwaited([ValueSource(nameof(_flatteners))] string how, [ValueSource(nameof(_syncShapes))] string shape)
    {
        Run($"drain(flatten('{how}', x => syncInner('{shape}', x)))").Should().Be("1,10,2,20");
    }

    /// <summary>
    /// A sync inner value that rejects rejects the step. CreateAsyncFromSyncIterator closes the sync iterator, as its
    /// <c>closeOnRejection</c> says for <c>next</c>, and IfAbruptCloseAsyncIterator closes the outer one; a
    /// <c>return()</c> afterwards closes nothing more.
    /// </summary>
    [Test]
    public void ARejectedSyncInnerValueRejectsTheStepAndClosesBothIterators()
    {
        Run("""
            (async function () {
                const counts = { outer: 0, inner: 0 };
                let i = 0;
                const outer = {
                    next() { i++; return Promise.resolve({ value: i, done: i > 3 }); },
                    return() { counts.outer++; return Promise.resolve({ done: true }); }
                };
                const helper = AsyncIterator.prototype.flatMap.call(outer, () => ({
                    [Symbol.iterator]() {
                        return {
                            next() { return { value: Promise.reject(new RangeError('inner')), done: false }; },
                            return() { counts.inner++; return {}; }
                        };
                    }
                }));
                let caught = 'none';
                try { await helper.next(); } catch (e) { caught = describe(e); }
                const afterNext = JSON.stringify(counts);
                await helper.return();
                return caught + ':' + afterNext + ':' + JSON.stringify(counts);
            })()
            """).Should().Be("""RangeError:{"outer":1,"inner":1}:{"outer":1,"inner":1}""");
    }

    /// <summary>
    /// <c>return()</c> while a sync inner iterable is open closes it, through the async-from-sync wrapper's
    /// <c>return</c>, and then the outer iterator.
    /// </summary>
    [Test]
    public void ReturnWhileASyncInnerIterableIsOpenClosesBothIterators()
    {
        Run("""
            (async function () {
                const counts = { outer: 0, inner: 0 };
                let i = 0;
                const outer = {
                    next() { i++; return Promise.resolve({ value: i, done: i > 3 }); },
                    return() { counts.outer++; return Promise.resolve({ done: true }); }
                };
                const helper = AsyncIterator.prototype.flatMap.call(outer, x => ({
                    [Symbol.iterator]() {
                        let j = 0;
                        return {
                            next() { j++; return { value: x + '.' + j, done: j > 2 }; },
                            return() { counts.inner++; return {}; }
                        };
                    }
                }));
                const first = await helper.next();
                const last = await helper.return();
                return first.value + ':' + last.done + ':' + JSON.stringify(counts);
            })()
            """).Should().Be("""1.1:true:{"outer":1,"inner":1}""");
    }

    /// <summary>
    /// <c>return()</c> while an inner iterator is open whose <c>return</c> fails still closes the outer iterator, once,
    /// whether the inner iterator is sync or async. A sync inner iterator's throw used to skip that close and leave the
    /// outer iterator open.
    /// <para>
    /// Only the close is pinned, because it is the one part every reading agrees on. Whether the helper's
    /// <c>return()</c> then resolves or rejects is not. Jint resolves only because it closes the inner iterator with a
    /// synchronous IteratorClose and never awaits the promise its <c>return</c> answers (sebastienros/jint#4500).
    /// core-js awaits it and rejects. The proposal is moving the same way. Its July 2026 plenary decided the inner
    /// iterator is closed first, and the champion leans towards awaiting each close in turn
    /// (tc39/proposal-async-iterator-helpers#30). The champion also says the current spec text is stale (#19, #31).
    /// </para>
    /// </summary>
    [TestCase("sync")]
    [TestCase("async")]
    public void ReturnClosesTheOuterIteratorWhenTheInnerIteratorsReturnFails(string shape)
    {
        Run($$"""
            (async function () {
                const counts = { outer: 0 };
                let i = 0;
                const outer = {
                    next() { i++; return Promise.resolve({ value: i, done: i > 3 }); },
                    return() { counts.outer++; return Promise.resolve({ done: true }); }
                };
                const inner = '{{shape}}' === 'sync'
                    ? { [Symbol.iterator]() { let j = 0; return { next() { j++; return { value: j, done: j > 2 }; }, return() { throw new RangeError('inner'); } }; } }
                    : { [Symbol.asyncIterator]() { let j = 0; return { next() { j++; return Promise.resolve({ value: j, done: j > 2 }); }, return() { return Promise.reject(new RangeError('inner')); } }; } };
                const helper = AsyncIterator.prototype.flatMap.call(outer, () => inner);
                await helper.next();
                // Settled either way: whether it resolves or rejects is sebastienros/jint#4500's to decide.
                try { await helper.return(); } catch { }
                return 'outer=' + counts.outer;
            })()
            """).Should().Be("outer=1");
    }

    /// <summary>
    /// The wrapper calls a sync inner iterator's <c>next</c> with no argument, as the helper calls it.
    /// </summary>
    [Test]
    public void ASyncInnerIteratorsNextIsCalledWithNoArgument()
    {
        Run("""
            (async function () {
                let count = -1;
                await AsyncIterator.from([1]).flatMap(() => ({
                    [Symbol.iterator]() { return { next() { count = arguments.length; return { done: true }; } }; }
                })).toArray();
                return String(count);
            })()
            """).Should().Be("0");
    }

    /// <summary>
    /// An inner async step that settles to something other than an object fails the step with a <c>TypeError</c>,
    /// which closes the outer iterator (IfAbruptCloseAsyncIterator) and not the inner one. It used to count as the
    /// inner iterator's end, so flatMap went on to the outer iterator's next value.
    /// </summary>
    [Test]
    public void AnInnerStepThatSettlesToAPrimitiveFailsAndClosesTheOuterIterator()
    {
        Run("""
            (async function () {
                const counts = { outer: 0, inner: 0 };
                let i = 0;
                const outer = {
                    next() { i++; return Promise.resolve({ value: i, done: i > 3 }); },
                    return() { counts.outer++; return Promise.resolve({ done: true }); }
                };
                const helper = AsyncIterator.prototype.flatMap.call(outer, () => ({
                    next() { return Promise.resolve(5); },
                    return() { counts.inner++; return Promise.resolve({ done: true }); }
                }));
                let caught = 'none';
                try { await helper.next(); } catch (e) { caught = describe(e); }
                await helper.return();
                return caught + ':' + JSON.stringify(counts);
            })()
            """).Should().Be("""TypeError:{"outer":1,"inner":0}""");
    }

    /// <summary>
    /// flatMap's mapper answering a string still fails the step: the operation flatMap calls throws for any value that
    /// is not an object, and only <c>AsyncIterator.from</c> converts a string first.
    /// </summary>
    [Test]
    public void FlatMapStillRejectsAString()
    {
        Run("AsyncIterator.from([1]).flatMap(() => 'ab').toArray().then(() => 'resolved', describe)").Should().Be("TypeError");
    }

    /// <summary>
    /// <c>AsyncIterator.from</c> iterates a string by code points, as <c>for await</c> does.
    /// </summary>
    [TestCase("from", "ab", "a,b")]
    [TestCase("from", "a😀", "a,😀")]
    [TestCase("from", "", "")]
    [TestCase("forAwait", "ab", "a,b")]
    [TestCase("forAwait", "a😀", "a,😀")]
    [TestCase("forAwait", "", "")]
    public void AStringIsIteratedByCodePoints(string how, string text, string expected)
    {
        var engine = new Engine();
        engine.SetValue("text", text);
        engine.Evaluate(Harness + $"\ndrain(fromString('{how}', text))").UnwrapIfPromise().AsString().Should().Be(expected);
    }

    /// <summary>
    /// <c>AsyncIterator.from</c> answers a string an async iterator that has the helpers.
    /// </summary>
    [Test]
    public void FromAnswersAStringAnAsyncIterator()
    {
        Run("""
            (async function () {
                const it = AsyncIterator.from('abc');
                return (it instanceof AsyncIterator) + ':' + (await it.map(c => c.toUpperCase()).toArray()).join('');
            })()
            """).Should().Be("true:ABC");
    }

    /// <summary>
    /// Step 1 converts the string with <c>ToObject</c> before GetIteratorFlattenable reads the iterator method, so a strict
    /// <c>String.prototype[Symbol.iterator]</c> is called on a String object. <c>Iterator.from</c> has no such step and
    /// calls it on the primitive; it is the control.
    /// </summary>
    [TestCase("async", "object")]
    [TestCase("sync", "string")]
    public void FromCallsTheStringsIteratorMethodOn(string kind, string expected)
    {
        Run($$"""
            (async function () {
                const original = String.prototype[Symbol.iterator];
                let receiver = 'not called';
                String.prototype[Symbol.iterator] = function () { 'use strict'; receiver = typeof this; return original.call(this); };
                try {
                    if ('{{kind}}' === 'async') await AsyncIterator.from('xy').toArray();
                    else Iterator.from('xy').toArray();
                } finally {
                    String.prototype[Symbol.iterator] = original;
                }
                return receiver;
            })()
            """).Should().Be(expected);
    }

    /// <summary>
    /// The String object is looked up for <c>@@asyncIterator</c> first, so one a script puts on <c>String.prototype</c>
    /// is the one <c>AsyncIterator.from</c> uses.
    /// </summary>
    [Test]
    public void FromUsesAnAsyncIteratorMethodOnStringPrototype()
    {
        Run("""
            (async function () {
                String.prototype[Symbol.asyncIterator] = async function* () { yield 'async:' + this; };
                try {
                    return (await AsyncIterator.from('q').toArray()).join(',');
                } finally {
                    delete String.prototype[Symbol.asyncIterator];
                }
            })()
            """).Should().Be("async:q");
    }

    /// <summary>
    /// A primitive that is not a string is still a <c>TypeError</c>, thrown by the call.
    /// </summary>
    [TestCase("5")]
    [TestCase("true")]
    [TestCase("Symbol('s')")]
    [TestCase("1n")]
    [TestCase("null")]
    [TestCase("undefined")]
    public void FromStillThrowsForAPrimitiveThatIsNotAString(string value)
    {
        Run($"(function () {{ try {{ AsyncIterator.from({value}); return 'no throw'; }} catch (e) {{ return describe(e); }} }})()")
            .Should().Be("TypeError");
    }
}
