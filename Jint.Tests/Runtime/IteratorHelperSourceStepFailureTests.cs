#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins what an iterator helper does when the source iterator's own step fails: it fails too, and it does not close the
/// source. Every step of
/// <a href="https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.map">AsyncIterator.prototype.map</a>
/// and its siblings is <c>? Await(? IteratorNext(iterated))</c>, then <c>? IteratorComplete(next)</c> and
/// <c>? IteratorValue(next)</c>. A <c>?</c> returns the abrupt completion as it is.
/// <a href="https://tc39.es/proposal-async-iterator-helpers/#sec-ifabruptcloseasynciterator">IfAbruptCloseAsyncIterator</a>,
/// which closes the source, guards only what the helper calls with a value: the mapper, the predicate, the await of
/// what they answered, and the yield. An iterator whose own <c>next</c> failed is not closed.
/// <para>
/// The async <c>map</c>, <c>filter</c>, <c>take</c>, <c>drop</c> and <c>flatMap</c> called the source's <c>return</c> for
/// every one of those failures (sebastienros/jint#4439): <c>next</c> throwing, the promise it answered rejecting or
/// settling to something other than an object, and a throwing <c>done</c> or <c>value</c> getter. The sync helpers, the
/// async consuming methods and a generator doing the same with <c>for</c>/<c>for await</c> are the control, run side by
/// side. Expected values are Node 26's: natively for the sync rows, and with the core-js 3.51 polyfill for the async
/// ones, since Node has no <c>AsyncIterator</c>.
/// </para>
/// </summary>
public class IteratorHelperSourceStepFailureTests
{
    private const string Harness = """
        function describe(e) { return e instanceof Error ? e.name : String(e); }
        function result(kind, r) { return kind === 'async' ? Promise.resolve(r) : r; }

        // A source answering 1 for its first `good` steps, then failing `how`. Calls to its return count in counts.closed.
        function failingSource(kind, how, good, counts) {
            let i = 0;
            return {
                next() {
                    i++;
                    if (i <= good) return result(kind, { value: i, done: false });
                    switch (how) {
                        case 'throws': throw new RangeError('next');
                        case 'rejects': return Promise.reject(new RangeError('next'));
                        case 'settlesToPrimitive': return Promise.resolve(5);
                        case 'returnsPrimitive': return 5;
                        case 'doneThrows': return result(kind, { get done() { throw new RangeError('done'); } });
                        case 'valueThrows': return result(kind, { done: false, get value() { throw new RangeError('value'); } });
                    }
                },
                return() { counts.closed++; return result(kind, { value: undefined, done: true }); }
            };
        }

        // A helper over `src`, or the generator that does the same with for/for await.
        function helper(kind, name, src) {
            const P = kind === 'async' ? AsyncIterator.prototype : Iterator.prototype;
            switch (name) {
                case 'map': return P.map.call(src, x => x * 10);
                case 'filter': return P.filter.call(src, () => false);
                case 'take': return P.take.call(src, 5);
                case 'drop': return P.drop.call(src, 1);
                case 'flatMap': return P.flatMap.call(src, x => [x]);
                case 'generator':
                    return kind === 'async'
                        ? (async function* () { for await (const x of { [Symbol.asyncIterator]() { return src; } }) yield x * 10; })()
                        : (function* () { for (const x of { [Symbol.iterator]() { return src; } }) yield x * 10; })();
            }
        }

        // Steps the helper until it is done or fails, then calls its return(): answers the values it gave, what it
        // failed with, and how often the source was closed after the failure and after the return().
        function stepHelper(kind, name, how, good) {
            const counts = { closed: 0 };
            const it = helper(kind, name, failingSource(kind, how, good, counts));
            const out = [];
            let caught = 'none';
            let closedAfterFailure = -1;
            const answer = () => out.join('|') + ':' + caught + ':' + closedAfterFailure + ',' + counts.closed;
            if (kind === 'sync') {
                try { for (;;) { const r = it.next(); if (r.done) break; out.push(r.value); } } catch (e) { caught = describe(e); }
                closedAfterFailure = counts.closed;
                it.return();
                return answer();
            }
            return (async function () {
                try { for (;;) { const r = await it.next(); if (r.done) break; out.push(r.value); } } catch (e) { caught = describe(e); }
                closedAfterFailure = counts.closed;
                await it.return();
                return answer();
            })();
        }

        // A consuming method over `src`, answering what it failed with and how often the source was closed.
        function consume(kind, name, how, good) {
            const counts = { closed: 0 };
            const P = kind === 'async' ? AsyncIterator.prototype : Iterator.prototype;
            const src = failingSource(kind, how, good, counts);
            const call = () => {
                switch (name) {
                    case 'reduce': return P.reduce.call(src, (a, x) => a + x, 0);
                    case 'toArray': return P.toArray.call(src);
                    case 'forEach': return P.forEach.call(src, () => { });
                    case 'some': return P.some.call(src, () => false);
                    case 'every': return P.every.call(src, () => true);
                    case 'find': return P.find.call(src, () => false);
                }
            };
            if (kind === 'sync') {
                try { call(); return 'none:' + counts.closed; } catch (e) { return describe(e) + ':' + counts.closed; }
            }
            return (async function () {
                try { await call(); return 'none:' + counts.closed; } catch (e) { return describe(e) + ':' + counts.closed; }
            })();
        }
        """;

    private static readonly string[] _helpers = ["map", "filter", "take", "drop", "flatMap", "generator"];

    private static readonly string[] _consumers = ["reduce", "toArray", "forEach", "some", "every", "find"];

    /// <summary>
    /// The ways a source step fails, with the error each one fails with. A promise is a sync step's value, not a failure,
    /// so the two promise rows are async only, and a sync step answering a primitive is <c>returnsPrimitive</c>.
    /// </summary>
    private static readonly (string Kind, string How, string Error)[] _failures =
    [
        ("async", "throws", "RangeError"),
        ("async", "rejects", "RangeError"),
        ("async", "settlesToPrimitive", "TypeError"),
        ("async", "returnsPrimitive", "TypeError"),
        ("async", "doneThrows", "RangeError"),
        ("async", "valueThrows", "RangeError"),
        ("sync", "throws", "RangeError"),
        ("sync", "returnsPrimitive", "TypeError"),
        ("sync", "doneThrows", "RangeError"),
        ("sync", "valueThrows", "RangeError"),
    ];

    /// <summary>
    /// Every helper against every failure, bar two rows of the async generator control: <c>for await</c> calls
    /// <c>next</c> again when a step settles to a primitive instead of throwing the <c>TypeError</c>, without end
    /// (sebastienros/jint#4489).
    /// </summary>
    private static IEnumerable<TestCaseData> HelperCases() =>
        from failure in _failures
        from name in _helpers
        where !(name == "generator" && failure.Kind == "async" && failure.How.EndsWith("Primitive", StringComparison.Ordinal))
        select new TestCaseData(failure.Kind, name, failure.How, failure.Error);

    private static IEnumerable<TestCaseData> ConsumerCases() =>
        from failure in _failures
        from name in _consumers
        select new TestCaseData(failure.Kind, name, failure.How, failure.Error);

    private static string Run(string script) =>
        new Engine().Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    /// <summary>
    /// The source's first step fails: the helper's first <c>next()</c> fails with that error, and neither it nor a
    /// <c>return()</c> afterwards closes the source, since the helper completed with the failure.
    /// </summary>
    [TestCaseSource(nameof(HelperCases))]
    public void AHelperWhoseSourcesFirstStepFailsDoesNotCloseIt(string kind, string name, string how, string error)
    {
        Run($"stepHelper('{kind}', '{name}', '{how}', 0)").Should().Be(":" + error + ":0,0");
    }

    /// <summary>
    /// The source answers 1 and then fails. <c>map</c>, <c>take</c>, <c>flatMap</c> and the generator give a value first;
    /// <c>filter</c> (rejecting everything) and <c>drop(1)</c> take their second step from inside the first one's
    /// continuation, as <c>flatMap</c> does once its inner iterator is spent. None of them closes the source.
    /// </summary>
    [TestCaseSource(nameof(HelperCases))]
    public void AHelperWhoseSourceFailsOnALaterStepDoesNotCloseIt(string kind, string name, string how, string error)
    {
        var values = name switch
        {
            "map" or "generator" => "10",
            "take" or "flatMap" => "1",
            _ => "",
        };

        Run($"stepHelper('{kind}', '{name}', '{how}', 1)").Should().Be(values + ":" + error + ":0,0");
    }

    /// <summary>
    /// The consuming methods already left a failed source open, async and sync alike.
    /// </summary>
    [TestCaseSource(nameof(ConsumerCases))]
    public void AConsumingMethodWhoseSourceStepFailsDoesNotCloseIt(string kind, string name, string how, string error)
    {
        Run($"consume('{kind}', '{name}', '{how}', 0)").Should().Be(error + ":0");
    }

    /// <summary>
    /// What the helper calls with a value still closes the source when it throws (IfAbruptCloseAsyncIterator): the
    /// mapper, and a promise the mapper answered that rejects. One close, and a <c>return()</c> afterwards adds none.
    /// </summary>
    [TestCase("async", "mapperThrows")]
    [TestCase("async", "mapperRejects")]
    [TestCase("sync", "mapperThrows")]
    public void AMapperThatFailsStillClosesTheSource(string kind, string how)
    {
        Run($$"""
            (async function () {
                const kind = '{{kind}}';
                const counts = { closed: 0 };
                const src = failingSource(kind, 'throws', 3, counts);
                const P = kind === 'async' ? AsyncIterator.prototype : Iterator.prototype;
                const it = P.map.call(src, () => {
                    if ('{{how}}' === 'mapperThrows') throw new RangeError('mapper');
                    return Promise.reject(new RangeError('mapper'));
                });
                let caught = 'none';
                try { await it.next(); } catch (e) { caught = describe(e); }
                const afterNext = counts.closed;
                await it.return();
                return caught + ':' + afterNext + ',' + counts.closed;
            })()
            """).Should().Be("RangeError:1,1");
    }
}
