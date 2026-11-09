#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins step 1 of <a href="https://tc39.es/ecma262/#sec-iteratornext">IteratorNext</a> for the iterator helpers: every
/// step calls the iterator record's [[NextMethod]], which
/// <a href="https://tc39.es/ecma262/#sec-getiteratordirect">GetIteratorDirect</a> read off the iterator once, and never
/// reads <c>next</c> again. The async helpers are a stage 2 proposal that test262 has no directory for, so these tests are
/// what pins them: each step of
/// <a href="https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.map">AsyncIterator.prototype.map</a>
/// and its siblings is <c>Await(? IteratorNext(iterated))</c>, the wrapper
/// <a href="https://tc39.es/proposal-async-iterator-helpers/#sec-asynciterator.from">AsyncIterator.from</a> builds calls
/// IteratorNext on its record
/// (<a href="https://tc39.es/proposal-async-iterator-helpers/#sec-wrapforvalidasynciteratorprototype.next">%WrapForValidAsyncIteratorPrototype%.next</a>),
/// and so does the inner loop of
/// <a href="https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.flatmap">flatMap</a> on the
/// record GetIteratorFlattenable built.
/// <para>
/// The async helpers read <c>next</c> off the iterator on every step instead (sebastienros/jint#4419): a getter on
/// <c>next</c> ran once more per step, a <c>next</c> replaced mid-iteration was called from the next step on, and a getter
/// answering <c>undefined</c> could install a real <c>next</c> that the first step then found. The sync helpers are the
/// control, run side by side: they already called the captured method. Expected values are Node 26's: natively for the
/// sync rows, and with the core-js 3.51 polyfill for the async ones, since Node has no <c>AsyncIterator</c>.
/// </para>
/// </summary>
public class IteratorHelperNextMethodTests
{
    private const string Harness = """
        function result(kind, value, done) { const r = { value: value, done: done }; return kind === 'async' ? Promise.resolve(r) : r; }

        // An iterator answering 1, 2, 3 and then done, whose `next` is reached through a getter counted in reads.n.
        function getterIterator(kind, reads) {
            let i = 0;
            return { get next() { reads.n++; return function () { i++; return result(kind, i, i > 3); }; } };
        }

        // An iterator answering 1, 2, 3 and then done, whose first step replaces its own `next` with one that answers done.
        function selfReplacingIterator(kind) {
            let i = 0;
            return {
                next() {
                    i++;
                    this.next = function () { return result(kind, 'replaced', true); };
                    return result(kind, i, i > 3);
                }
            };
        }

        // An iterator whose `next` getter installs a working `next` and answers undefined, so the record holds undefined.
        function installingIterator(kind) {
            let i = 0;
            return {
                get next() {
                    Object.defineProperty(this, 'next', { value: function () { i++; return result(kind, i, i > 3); } });
                    return undefined;
                }
            };
        }

        // A generator object answering 1, 2, 3, whose body assigns an own `next` once the second step resumes it.
        function selfReplacingGenerator(kind) {
            const replaced = function () { return result(kind, 'replaced', true); };
            const gen = kind === 'async'
                ? (async function* () { yield 1; gen.next = replaced; yield 2; yield 3; })()
                : (function* () { yield 1; gen.next = replaced; yield 2; yield 3; })();
            return gen;
        }

        // An inner iterable for flatMap answering x.1, x.2 and then done: behind @@asyncIterator ('async'), behind
        // @@iterator ('sync'), or the iterator itself ('iterator'). Its `next` is a getter counted in reads.n, or with mode
        // 'replace' a method that replaces itself on its first call.
        function innerFor(kind, shape, x, reads, mode) {
            const stepKind = shape === 'sync' ? 'sync' : kind;
            let j = 0;
            const step = function () { j++; return result(stepKind, x + '.' + j, j > 2); };
            const it = mode === 'replace'
                ? { next() { this.next = function () { return result(stepKind, 'replaced', true); }; return step(); } }
                : { get next() { reads.n++; return step; } };
            if (shape === 'async') return { [Symbol.asyncIterator]() { return it; } };
            if (shape === 'sync') return { [Symbol.iterator]() { return it; } };
            return it;
        }

        // Steps `it` through its own next() until done, so that only the helper under test reads the source's `next`.
        function drain(kind, it) {
            const out = [];
            if (kind === 'sync') {
                for (;;) { const r = it.next(); if (r.done) return out.join(','); out.push(r.value); }
            }
            return (async function () {
                for (;;) { const r = await it.next(); if (r.done) return out.join(','); out.push(r.value); }
            })();
        }

        // Each consumer takes a source iterator and answers a string, or a promise of one for 'async'.
        function consumers(kind) {
            const P = kind === 'async' ? AsyncIterator.prototype : Iterator.prototype;
            const C = kind === 'async' ? AsyncIterator : Iterator;
            const then = (v, f) => kind === 'async' ? Promise.resolve(v).then(f) : f(v);
            const iterKey = kind === 'async' ? Symbol.asyncIterator : Symbol.iterator;
            return {
                map: src => drain(kind, P.map.call(src, x => x * 10)),
                filter: src => drain(kind, P.filter.call(src, x => x !== 2)),
                take: src => drain(kind, P.take.call(src, 5)),
                drop: src => drain(kind, P.drop.call(src, 1)),
                flatMap: src => drain(kind, P.flatMap.call(src, x => [x, -x])),
                reduce: src => then(P.reduce.call(src, (a, x) => a + x, 0), String),
                reduceWithoutInitialValue: src => then(P.reduce.call(src, (a, x) => a + x), String),
                toArray: src => then(P.toArray.call(src), a => a.join(',')),
                forEach: src => { const out = []; return then(P.forEach.call(src, x => { out.push(x); }), () => out.join(',')); },
                some: src => then(P.some.call(src, x => x === 3), String),
                every: src => then(P.every.call(src, x => x < 3), String),
                find: src => then(P.find.call(src, x => x === 3), String),
                from: src => drain(kind, C.from(src)),
                fromIterable: src => drain(kind, C.from({ [iterKey]() { return src; } }))
            };
        }

        function describe(e) { return e instanceof Error ? e.name : String(e); }

        // Runs consumer `name` over `src`, answering its string or 'caught:<error name>'.
        function consume(kind, name, src) {
            const run = consumers(kind)[name];
            if (kind === 'sync') {
                try { return run(src); } catch (e) { return 'caught:' + describe(e); }
            }
            return (async function () { try { return await run(src); } catch (e) { return 'caught:' + describe(e); } })();
        }
        """;

    /// <summary>
    /// What each consumer answers over a source of 1, 2, 3 that its record steps to the end.
    /// </summary>
    private static readonly Dictionary<string, string> _expected = new(StringComparer.Ordinal)
    {
        ["map"] = "10,20,30",
        ["filter"] = "1,3",
        ["take"] = "1,2,3",
        ["drop"] = "2,3",
        ["flatMap"] = "1,-1,2,-2,3,-3",
        ["reduce"] = "6",
        ["reduceWithoutInitialValue"] = "6",
        ["toArray"] = "1,2,3",
        ["forEach"] = "1,2,3",
        ["some"] = "true",
        ["every"] = "false",
        ["find"] = "3",
        ["from"] = "1,2,3",
        ["fromIterable"] = "1,2,3",
    };

    private static readonly string[] _kinds = ["async", "sync"];

    private static readonly string[] _consumers = _expected.Keys.ToArray();

    /// <summary>
    /// The consumers a generator source applies to: <c>from</c> answers a generator object itself, which is already an
    /// instance of <c>Iterator</c> or <c>AsyncIterator</c>, so stepping it reads <c>next</c> afresh by design.
    /// </summary>
    private static readonly string[] _helperConsumers = _consumers.Where(c => !c.StartsWith("from", StringComparison.Ordinal)).ToArray();

    private static string Run(string script) =>
        new Engine().Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    /// <summary>
    /// A getter on <c>next</c> runs once, whatever the number of steps.
    /// </summary>
    [Test]
    public void ANextReachedThroughAGetterIsReadOnce([ValueSource(nameof(_kinds))] string kind, [ValueSource(nameof(_consumers))] string consumer)
    {
        Run($$"""
            (function () {
                const reads = { n: 0 };
                const answer = v => reads.n + ':' + v;
                const v = consume('{{kind}}', '{{consumer}}', getterIterator('{{kind}}', reads));
                return '{{kind}}' === 'async' ? v.then(answer) : answer(v);
            })()
            """).Should().Be("1:" + _expected[consumer]);
    }

    /// <summary>
    /// A <c>next</c> replaced after the first step is not the record's, so every step still calls the original.
    /// </summary>
    [Test]
    public void ANextReplacedMidIterationIsNotCalled([ValueSource(nameof(_kinds))] string kind, [ValueSource(nameof(_consumers))] string consumer)
    {
        Run($"consume('{kind}', '{consumer}', selfReplacingIterator('{kind}'))").Should().Be(_expected[consumer]);
    }

    /// <summary>
    /// A getter that answers <c>undefined</c> and installs a real <c>next</c> as it does: the record holds
    /// <c>undefined</c>, so the first step's Call throws the TypeError rather than finding the installed method.
    /// </summary>
    [Test]
    public void ANextInstalledAfterTheRecordWasBuiltIsNotCalled([ValueSource(nameof(_kinds))] string kind, [ValueSource(nameof(_consumers))] string consumer)
    {
        Run($"consume('{kind}', '{consumer}', installingIterator('{kind}'))").Should().Be("caught:TypeError");
    }

    /// <summary>
    /// The common source: a generator object, whose <c>next</c> the helper reads once from the generator prototype, so
    /// an own <c>next</c> the generator's body assigns mid-iteration is never called.
    /// </summary>
    [Test]
    public void AGeneratorsNextReplacedMidIterationIsNotCalled([ValueSource(nameof(_kinds))] string kind, [ValueSource(nameof(_helperConsumers))] string consumer)
    {
        Run($"consume('{kind}', '{consumer}', selfReplacingGenerator('{kind}'))").Should().Be(_expected[consumer]);
    }

    /// <summary>
    /// flatMap's inner iterator: GetIteratorFlattenable reads its <c>next</c> once, so three inner iterators of two values
    /// each read it three times in all, whether the mapper answers an async iterable, a sync one, or an iterator.
    /// </summary>
    [TestCase("async", "async")]
    [TestCase("async", "sync")]
    [TestCase("async", "iterator")]
    [TestCase("sync", "sync")]
    [TestCase("sync", "iterator")]
    public void AFlatMapInnerIteratorsNextIsReadOnce(string kind, string shape)
    {
        Run($$"""
            (function () {
                const reads = { n: 0 };
                const P = '{{kind}}' === 'async' ? AsyncIterator.prototype : Iterator.prototype;
                const answer = v => reads.n + ':' + v;
                const v = drain('{{kind}}', P.flatMap.call([1, 2, 3].values(), x => innerFor('{{kind}}', '{{shape}}', x, reads, 'getter')));
                return '{{kind}}' === 'async' ? v.then(answer) : answer(v);
            })()
            """).Should().Be("3:1.1,1.2,2.1,2.2,3.1,3.2");
    }

    /// <summary>
    /// flatMap's inner iterator replacing its own <c>next</c> on its first step: every step still calls the original.
    /// </summary>
    [TestCase("async", "async")]
    [TestCase("async", "sync")]
    [TestCase("async", "iterator")]
    [TestCase("sync", "sync")]
    [TestCase("sync", "iterator")]
    public void AFlatMapInnerIteratorsNextReplacedMidIterationIsNotCalled(string kind, string shape)
    {
        Run($$"""
            (function () {
                const P = '{{kind}}' === 'async' ? AsyncIterator.prototype : Iterator.prototype;
                return drain('{{kind}}', P.flatMap.call([1, 2, 3].values(), x => innerFor('{{kind}}', '{{shape}}', x, null, 'replace')));
            })()
            """).Should().Be("1.1,1.2,2.1,2.2,3.1,3.2");
    }

    /// <summary>
    /// A mapper that answers an object with no <c>next</c> and no iterator method fails the step with a TypeError, which
    /// closes the outer iterator and leaves the inner object alone, and a <c>return()</c> on the helper afterwards closes
    /// nothing more. So does an inner <c>next</c> that throws. The async flatMap skipped such an object and went on to the
    /// outer iterator's next value, and after an inner <c>next</c> threw, its <c>return()</c> still closed the inner
    /// iterator.
    /// </summary>
    [TestCase("async", "withoutNext", "TypeError:outer=1,inner=0")]
    [TestCase("async", "throwingNext", "RangeError:outer=1,inner=0")]
    [TestCase("sync", "withoutNext", "TypeError:outer=1,inner=0")]
    [TestCase("sync", "throwingNext", "RangeError:outer=1,inner=0")]
    public void AFlatMapInnerIteratorWhoseStepFailsClosesOnlyTheOuterIterator(string kind, string how, string expected)
    {
        Run($$"""
            (async function () {
                const kind = '{{kind}}';
                const P = kind === 'async' ? AsyncIterator.prototype : Iterator.prototype;
                const counts = { outer: 0, inner: 0 };
                const outer = {
                    i: 0,
                    next() { this.i++; return result(kind, this.i, this.i > 3); },
                    return() { counts.outer++; return result(kind, undefined, true); }
                };
                const helper = P.flatMap.call(outer, () => '{{how}}' === 'withoutNext'
                    ? { return() { counts.inner++; return {}; } }
                    : { next() { throw new RangeError('inner'); }, return() { counts.inner++; return {}; } });
                let caught = 'none';
                try { await helper.next(); } catch (e) { caught = describe(e); }
                await helper.return();
                return caught + ':outer=' + counts.outer + ',inner=' + counts.inner;
            })()
            """).Should().Be(expected);
    }

    /// <summary>
    /// <c>from</c> reads <c>next</c> once when it builds its record, even when it then answers the iterator itself because
    /// that is already an instance (GetIteratorFlattenable, then OrdinaryHasInstance). AsyncIterator.from checked the
    /// prototype chain first and read <c>next</c> only to wrap the iterator.
    /// </summary>
    [TestCase("async")]
    [TestCase("sync")]
    public void FromReadsNextOnceWhenItAnswersTheIteratorItself(string kind)
    {
        Run($$"""
            (function () {
                const reads = { n: 0 };
                const src = getterIterator('{{kind}}', reads);
                Object.setPrototypeOf(src, '{{kind}}' === 'async' ? AsyncIterator.prototype : Iterator.prototype);
                const r = ('{{kind}}' === 'async' ? AsyncIterator : Iterator).from(src);
                return (r === src) + ':' + reads.n;
            })()
            """).Should().Be("true:1");
    }

    /// <summary>
    /// <c>from</c> reads <c>next</c> before OrdinaryHasInstance walks the prototype chain, as a Proxy's traps see. The
    /// async order is the one GetIteratorFlattenable with the <c>async</c> hint gives, the version the proposal links:
    /// core-js agrees on where <c>next</c> falls, and adds two reads of its own internals.
    /// </summary>
    [TestCase("async", "get:Symbol(Symbol.asyncIterator),get:Symbol(Symbol.iterator),get:next,getPrototypeOf")]
    [TestCase("sync", "get:Symbol(Symbol.iterator),get:next,getPrototypeOf")]
    public void FromReadsNextBeforeItChecksThePrototypeChain(string kind, string expected)
    {
        Run($$"""
            (function () {
                const log = [];
                const p = new Proxy({ next() { } }, {
                    get(t, k) { log.push('get:' + String(k)); return Reflect.get(t, k); },
                    getPrototypeOf(t) { log.push('getPrototypeOf'); return Reflect.getPrototypeOf(t); }
                });
                ('{{kind}}' === 'async' ? AsyncIterator : Iterator).from(p);
                return log.join(',');
            })()
            """).Should().Be(expected);
    }
}
