#nullable enable

using System.Collections;
using Jint.Native;

namespace Jint.Tests.Runtime.Interop;

/// <summary>
/// A wrapped CLR collection's <c>@@iterator</c> hands script an iterator over a host enumerator, and the host enumerator
/// has to be disposed exactly once, however the iteration ends (sebastienros/jint#4410).
/// <para>
/// <a href="https://tc39.es/ecma262/#sec-iteratorclose">IteratorClose</a> runs only when the consumer leaves early, and
/// a built-in iterator has no <c>return</c> for it to call, so the CLR iterator's native close is what disposes. Two
/// gaps remained. A dictionary's iterator had no native close at all, so leaving a loop over a wrapped dictionary
/// early never disposed the key sequence, nor the host's own key enumerator inside it. And an iterator that ran to the
/// end was never closed, which is right for the specification and wrong for the host: a C# <c>foreach</c> disposes
/// its enumerator when <c>MoveNext</c> answers <see langword="false"/>, and an enumerator over a reader or a lock
/// releases it in <c>Dispose</c>. A C# iterator method cannot show that second gap, since running off its end runs its
/// <c>finally</c> without a <c>Dispose</c>; a hand-written enumerator counting its <c>Dispose</c> calls does.
/// </para>
/// <para>
/// Node has no counterpart. The values each consumer answers are what it answers in Node 26 over an array of the
/// same elements.
/// </para>
/// </summary>
public class ClrEnumeratorDisposalTests
{
    private const string RunToTheEndHarness = """
        // Consumers that run the iterator over `items` (1, 2, 3) until it reports done.
        const consume = {
            forOf: function () { const out = []; for (const x of items) { out.push(x); } return out.join(','); },
            spread: function () { return [...items].join(','); },
            arrayFrom: function () { return Array.from(items).join(','); },
            destructurePastTheEnd: function () { const [a, b, c, d] = items; return [a, b, c, String(d)].join(','); },
            restElement: function () { const [a, ...rest] = items; return a + '|' + rest.join(','); },
            setConstructor: function () { return [...new Set(items)].join(','); },
            generatorYieldStar: function () { function* g() { yield* items; } return [...g()].join(','); },
            iteratorHelper: function () { return Iterator.from(items).map(function (x) { return x * 10; }).toArray().join(','); },
            nextPastTheEnd: function () {
                const it = items[Symbol.iterator]();
                const out = [];
                for (let i = 0; i < 5; i++) { const r = it.next(); out.push(r.done ? 'done' : r.value); }
                return out.join(',');
            },
            forAwait: async function () { const out = []; for await (const x of items) { out.push(x); } return out.join(','); },
            asyncGeneratorForAwait: async function () {
                async function* g() { for await (const x of items) { yield x; } }
                const out = [];
                for await (const x of g()) { out.push(x); }
                return out.join(',');
            },
            asyncGeneratorYieldStar: async function () {
                async function* g() { yield* items; }
                const out = [];
                for await (const x of g()) { out.push(x); }
                return out.join(',');
            },
            fromAsync: async function () { return (await Array.fromAsync(items)).join(','); },
            fromAsyncMapped: async function () { return (await Array.fromAsync(items, function (x) { return x * 10; })).join(','); }
        };
        """;

    /// <summary>
    /// Every consumer that runs a wrapped enumerable's iterator to the end leaves its enumerator disposed, once, and
    /// nothing steps it afterwards: the <c>next()</c> calls past the end answer done without reaching it.
    /// </summary>
    [TestCase("forOf", "1,2,3")]
    [TestCase("spread", "1,2,3")]
    [TestCase("arrayFrom", "1,2,3")]
    [TestCase("destructurePastTheEnd", "1,2,3,undefined")]
    [TestCase("restElement", "1|2,3")]
    [TestCase("setConstructor", "1,2,3")]
    [TestCase("generatorYieldStar", "1,2,3")]
    [TestCase("iteratorHelper", "10,20,30")]
    [TestCase("nextPastTheEnd", "1,2,3,done,done")]
    [TestCase("forAwait", "1,2,3")]
    [TestCase("asyncGeneratorForAwait", "1,2,3")]
    [TestCase("asyncGeneratorYieldStar", "1,2,3")]
    [TestCase("fromAsync", "1,2,3")]
    [TestCase("fromAsyncMapped", "10,20,30")]
    public void AnEnumeratorThatRunsToTheEndIsDisposed(string consumer, string expected)
    {
        var items = new CountingEnumerable();
        var engine = new Engine();
        engine.SetValue("items", items);

        engine.Evaluate(RunToTheEndHarness + "\n" + $"consume.{consumer}()").UnwrapIfPromise().AsString().Should().Be(expected);
        items.Disposals.Should().Be(1);
        items.StepsAfterDisposal.Should().Be(0);
    }

    /// <summary>
    /// Array.fromAsync over an <c>@@asyncIterator</c> that answers the CLR iterator itself, whose mapper throws: the
    /// close finds no <c>return</c> to call (<a href="https://tc39.es/ecma262/#sec-asynciteratorclose">AsyncIteratorClose</a>
    /// step 4.b), and still has to reach the CLR iterator's native close, as a <c>for await</c> loop's does. The
    /// untouched and the replaced <c>%ArrayIteratorPrototype%.next</c> make the record the CLR iterator itself and an
    /// ObjectIterator over it.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void ArrayFromAsyncWhoseMapperThrowsDisposesTheClrIteratorAnAsyncIteratorAnswered(bool replaceNext)
    {
        var items = new CountingEnumerable();
        var engine = new Engine();
        engine.SetValue("items", items);

        engine.Evaluate($$"""
            (async function () {
                const proto = Object.getPrototypeOf([].values());
                const original = proto.next;
                if ({{(replaceNext ? "true" : "false")}}) { proto.next = function () { return original.call(this); }; }
                try {
                    await Array.fromAsync({ [Symbol.asyncIterator]() { return items[Symbol.iterator](); } }, function () { throw 'M'; });
                    return 'no throw';
                } catch (e) {
                    return 'caught:' + e;
                } finally {
                    proto.next = original;
                }
            })()
            """).UnwrapIfPromise().AsString().Should().Be("caught:M");
        items.Disposals.Should().Be(1);
    }

    private const string DictionaryHarness = """
        // The ways a loop over the wrapped dictionary `entries` can end, each answering how many entries it saw.
        const leave = {
            forOfToTheEnd: function () { let n = 0; for (const [k, v] of entries) { n++; } return String(n); },
            forOfBreak: function () { let n = 0; for (const [k, v] of entries) { n++; break; } return String(n); },
            forOfThrow: function () { let n = 0; try { for (const [k, v] of entries) { n++; throw 'E'; } } catch (e) { return n + ',caught:' + e; } },
            forOfReturn: function () { let n = 0; (function () { for (const [k, v] of entries) { n++; return; } })(); return String(n); },
            destructureFirst: function () { const [first] = entries; return String(first.length); },
            generatorReturn: function () {
                function* g() { for (const e of entries) { yield e; } }
                const it = g();
                it.next();
                return String(it.return('R').value);
            },
            forAwaitBreak: async function () { let n = 0; for await (const [k, v] of entries) { n++; break; } return String(n); },
            asyncGeneratorReturn: async function () {
                async function* g() { for await (const e of entries) { yield e; } }
                const it = g();
                await it.next();
                return String((await it.return('R')).value);
            }
        };
        """;

    /// <summary>
    /// A wrapped dictionary's iterator steps a key sequence that pulls from the host's key enumerator. Leaving the loop
    /// early has to dispose it, which reaches the host's: here a <c>yield</c> method's <c>finally</c> behind
    /// <c>SortedList.Keys</c>. Running to the end is the control — the key sequence ran off its end and disposed it.
    /// </summary>
    [TestCase("forOfToTheEnd", "2")]
    [TestCase("forOfBreak", "1")]
    [TestCase("forOfThrow", "1,caught:E")]
    [TestCase("forOfReturn", "1")]
    [TestCase("destructureFirst", "2")]
    [TestCase("generatorReturn", "R")]
    [TestCase("forAwaitBreak", "1")]
    [TestCase("asyncGeneratorReturn", "R")]
    public void LeavingALoopOverAWrappedDictionaryDisposesItsKeyEnumerator(string way, string expected)
    {
        var entries = new KeyTrackingSortedList { ["a"] = 1, ["b"] = 2 };
        var engine = new Engine();
        engine.SetValue("entries", entries);

        engine.Evaluate(DictionaryHarness + "\n" + $"leave.{way}()").UnwrapIfPromise().AsString().Should().Be(expected);
        entries.KeyEnumeratorsFinished.Should().Be(1);
    }

    /// <summary>
    /// The same, for keys a host reports through <see cref="Options.InteropOptions.ObjectWrapperReportedPropertyKeys"/>:
    /// the dictionary's iterator steps that sequence, and leaving early has to dispose it too.
    /// </summary>
    [TestCase("forOfToTheEnd", "2")]
    [TestCase("forOfBreak", "1")]
    [TestCase("forOfThrow", "1,caught:E")]
    [TestCase("destructureFirst", "2")]
    [TestCase("forAwaitBreak", "1")]
    public void LeavingALoopOverAWrappedDictionaryDisposesTheReportedKeys(string way, string expected)
    {
        var finished = 0;

        IEnumerable<JsValue> ReportedKeys()
        {
            try
            {
                yield return "a";
                yield return "b";
            }
            finally
            {
                finished++;
            }
        }

        var engine = new Engine(options => options.Interop.ObjectWrapperReportedPropertyKeys =
            (_, target) => target is Dictionary<string, int> ? ReportedKeys() : null);
        engine.SetValue("entries", new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });

        engine.Evaluate(DictionaryHarness + "\n" + $"leave.{way}()").UnwrapIfPromise().AsString().Should().Be(expected);
        finished.Should().Be(1);
    }

    /// <summary>A host collection of 1, 2 and 3 whose enumerators count their <see cref="IDisposable.Dispose"/> calls.</summary>
    private sealed class CountingEnumerable : IEnumerable<int>
    {
        public int Disposals;
        public int StepsAfterDisposal;

        public IEnumerator<int> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator(CountingEnumerable owner) : IEnumerator<int>
        {
            private int _current;
            private bool _disposed;

            public int Current => _current;

            object IEnumerator.Current => _current;

            public bool MoveNext()
            {
                if (_disposed)
                {
                    owner.StepsAfterDisposal++;
                }

                if (_current >= 3)
                {
                    return false;
                }

                _current++;
                return true;
            }

            public void Reset() => _current = 0;

            public void Dispose()
            {
                _disposed = true;
                owner.Disposals++;
            }
        }
    }

    /// <summary>A <see cref="SortedList"/> whose key enumerators are a <c>yield</c> method counting the times its <c>finally</c> ran.</summary>
    private sealed class KeyTrackingSortedList : SortedList
    {
        public int KeyEnumeratorsFinished;

        public override ICollection Keys => new TrackedKeys(this, base.Keys);

        private sealed class TrackedKeys(KeyTrackingSortedList owner, ICollection keys) : ICollection
        {
            public int Count => keys.Count;

            public bool IsSynchronized => false;

            public object SyncRoot => this;

            public void CopyTo(Array array, int index) => keys.CopyTo(array, index);

            public IEnumerator GetEnumerator()
            {
                try
                {
                    foreach (var key in keys)
                    {
                        yield return key;
                    }
                }
                finally
                {
                    owner.KeyEnumeratorsFinished++;
                }
            }
        }
    }
}
