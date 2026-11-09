#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The one matrix that pins when a WebIDL sequence conversion closes its iterator, shared by the web-API lanes
/// (<c>WebIdlSequenceCloseTests</c>) and the <c>Jint.Browser</c> ones (<c>BrowserWebIdlSequenceCloseTests</c>). The
/// convention is sebastienros/jint#4431's: an element conversion that throws closes the iterator, which calls
/// <c>return</c> exactly once (<a href="https://tc39.es/ecma262/#sec-iteratorclose">IteratorClose</a> with a throw
/// completion, so the conversion's error is what propagates), while a step that throws (<c>next</c>, or the read that
/// produced the element) leaves the record done and closes nothing; neither does a sequence that runs out.
/// <para>
/// WebIDL as written has no close (<a href="https://webidl.spec.whatwg.org/#create-sequence-from-iterable">create a
/// sequence from an iterable</a>; reported as an omission in whatwg/webidl#895), and whatwg/webidl#1662 proposes
/// adding it, for async sequences as well. It is the behaviour of ECMAScript's own consumers, and Safari's. Chrome and
/// Firefox leave the iterator open.
/// </para>
/// </summary>
/// <remarks>
/// A suite evaluates <see cref="Harness"/>, registers its lanes on <c>lanes</c>, and runs
/// <c>probe(lane, probe, row)</c> for every row <see cref="Cases"/> yields. A lane is
/// <c>() =&gt; ({ goods, bad, run })</c>: <c>goods</c> a sequence the conversion accepts, <c>bad</c> an element whose
/// conversion throws, and <c>run</c> the operation, which may answer a promise. A lane's own iterables (the outer
/// sequence around an inner one under test, say) come from <c>plain</c>, whose iterator has no <c>return</c>, so the
/// counts belong to the probe alone. The engine is the case's own: the probes patch the iterator prototypes.
/// </remarks>
internal static class WebIdlSequenceCloseMatrix
{
    internal const string Harness = """
        // A recognisable error, so a row can tell which throw reached the caller.
        function marker(name) { const e = new Error(name); e.name = name; return e; }

        // An element whose string or numeric conversion throws a ConversionError.
        const throwsOnConversion = {
            toString() { throw marker('ConversionError'); },
            valueOf() { throw marker('ConversionError'); },
        };

        // An iterable whose iterator has no `return`, for a lane's own sequences: closing one is invisible.
        function plain(...values) {
            return {
                [Symbol.iterator]() {
                    let i = 0;
                    return { next: () => i < values.length ? { value: values[i++], done: false } : { value: undefined, done: true } };
                },
            };
        }

        const lanes = {};
        const counts = { returns: 0, finallies: 0 };
        const ArrayIteratorPrototype = Object.getPrototypeOf([][Symbol.iterator]());
        const GeneratorPrototype = Object.getPrototypeOf(function* () {}).prototype;
        const AsyncGeneratorPrototype = Object.getPrototypeOf(async function* () {}).prototype;

        // The iterable a probe hands the lane: `values` in order, then (when `throwsInNext`) a step that throws.
        //   array           a real Array; %ArrayIteratorPrototype%.return, which no built-in has, counts closes, and a
        //                   throwing step is an index getter that %ArrayIteratorPrototype%.next reads.
        //   generator       a generator object; its `finally` counts how it ended, a counting
        //                   %GeneratorPrototype%.return counts closes, and a throwing step is its body throwing.
        //   iterator        a hand-written iterator whose `next` throws and whose `return` counts.
        //   asyncGenerator  an async generator, for an async sequence alone.
        function make(probe, values, throwsInNext) {
            switch (probe) {
                case 'array': {
                    ArrayIteratorPrototype.return = function () { counts.returns++; return { value: undefined, done: true }; };
                    const array = values.slice();
                    if (throwsInNext) Object.defineProperty(array, array.length, { get() { throw marker('NextError'); } });
                    return array;
                }
                case 'generator': {
                    const original = GeneratorPrototype.return;
                    GeneratorPrototype.return = function (value) { counts.returns++; return original.call(this, value); };
                    return (function* () {
                        try {
                            for (let i = 0; i < values.length; i++) yield values[i];
                            if (throwsInNext) throw marker('NextError');
                        } finally {
                            counts.finallies++;
                        }
                    })();
                }
                case 'iterator':
                    return {
                        [Symbol.iterator]() {
                            let i = 0;
                            return {
                                next() {
                                    if (i === values.length && throwsInNext) throw marker('NextError');
                                    return i < values.length ? { value: values[i++], done: false } : { value: undefined, done: true };
                                },
                                return() { counts.returns++; return {}; },
                            };
                        },
                    };
                case 'asyncGenerator': {
                    const original = AsyncGeneratorPrototype.return;
                    AsyncGeneratorPrototype.return = function (value) { counts.returns++; return original.call(this, value); };
                    return (async function* () {
                        try {
                            for (let i = 0; i < values.length; i++) yield values[i];
                            if (throwsInNext) throw marker('NextError');
                        } finally {
                            counts.finallies++;
                        }
                    })();
                }
            }
        }

        // Runs one row of the matrix against a lane and answers "<outcome> return=<n>[ finally=<n>]", the outcome
        // being 'ok' or the name of what the operation threw or rejected with.
        //   conversion  the lane's first good element, then its bad one.
        //   next        the lane's first good element, then a step that throws a NextError.
        //   done        the lane's good elements, then the end of the sequence.
        async function probe(laneName, probeName, row) {
            const lane = lanes[laneName]();
            const values = row === 'conversion' ? [lane.goods[0], lane.bad] : row === 'next' ? [lane.goods[0]] : lane.goods;
            const input = make(probeName, values, row === 'next');
            let outcome;
            try {
                await lane.run(input);
                outcome = 'ok';
            } catch (e) {
                outcome = e !== null && typeof e === 'object' ? String(e.name) : String(e);
            }

            const generator = probeName === 'generator' || probeName === 'asyncGenerator';
            return outcome + ' return=' + counts.returns + (generator ? ' finally=' + counts.finallies : '');
        }
        """;

    /// <summary>The probes every lane runs under.</summary>
    internal static readonly string[] SyncProbes = ["array", "generator", "iterator"];

    /// <summary>The probes an async sequence additionally runs under.</summary>
    internal static readonly string[] AsyncProbes = [.. SyncProbes, "asyncGenerator"];

    internal static readonly string[] Rows = ["conversion", "next", "done"];

    /// <summary>
    /// What a row answers. <paramref name="error"/> is the name of what the lane's bad element throws. A sync iterable
    /// is closed once by a failing conversion and never otherwise; a generator's <c>finally</c> runs in every row,
    /// through that close, its own throw, or its end. Under an async sequence a sync iterable's bad element is a
    /// rejection, and CreateAsyncFromSyncIterator closes it
    /// (<a href="https://tc39.es/ecma262/#sec-asyncfromsynciteratorcontinuation">AsyncFromSyncIteratorContinuation</a>);
    /// an async generator awaits its own rejection, which ends it as its own throw would, and is not closed.
    /// </summary>
    internal static string Expected(string probe, string row, string error)
    {
        var closes = row == "conversion" && probe != "asyncGenerator" ? 1 : 0;
        var outcome = row switch
        {
            "conversion" => error,
            "next" => "NextError",
            _ => "ok",
        };

        var generator = probe is "generator" or "asyncGenerator";
        return outcome + " return=" + closes + (generator ? " finally=1" : "");
    }

    /// <summary>
    /// Every row of the matrix for <paramref name="lanes"/>, each lane named with the error its bad element throws;
    /// <paramref name="asyncLanes"/> also run under <see cref="AsyncProbes"/>.
    /// </summary>
    internal static TestCases<string, string, string, string> Cases(
        IReadOnlyDictionary<string, string> lanes,
        IReadOnlyDictionary<string, string>? asyncLanes = null)
    {
        var rows = new TestCases<string, string, string, string>();
        Add(rows, lanes, SyncProbes);
        if (asyncLanes is not null)
        {
            Add(rows, asyncLanes, AsyncProbes);
        }

        return rows;
    }

    private static void Add(TestCases<string, string, string, string> rows, IReadOnlyDictionary<string, string> lanes, string[] probes)
    {
        foreach (var (lane, error) in lanes)
        {
            foreach (var probe in probes)
            {
                foreach (var row in Rows)
                {
                    rows.Add(lane, probe, row, Expected(probe, row, error));
                }
            }
        }
    }
}
#endif
