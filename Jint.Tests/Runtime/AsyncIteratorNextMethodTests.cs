#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins step 1 of <a href="https://tc39.es/ecma262/#sec-iteratornext">IteratorNext</a> for the two async consumers:
/// a <c>for await</c> loop (<a href="https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset">ForIn/OfBodyEvaluation</a>)
/// and <a href="https://tc39.es/ecma262/#sec-array.fromasync">Array.fromAsync</a> call the iterator record's
/// [[NextMethod]], which <a href="https://tc39.es/ecma262/#sec-getiteratorfrommethod">GetIteratorFromMethod</a> read
/// off the iterator once, and never read <c>next</c> again.
/// <para>
/// Both read <c>next</c> off the record's iterator on every step instead (sebastienros/jint#4410). So a getter on
/// <c>next</c> ran once when the record was built and once more per step, a <c>next</c> replaced mid-loop was called
/// from the next step on, and a getter answering <c>undefined</c> could install a real <c>next</c> that the first step
/// then found. A sync iterable is the control: its record is the sync one inside CreateAsyncFromSyncIterator, which
/// already called the captured method. Every loop runs in an async function and, beside it, in an async generator
/// that yields each value. Every expected value is Node 26's, bar the two Array.fromAsync rows noted on
/// <see cref="ANextReachedThroughAGetterIsReadOnce"/>.
/// </para>
/// </summary>
public class AsyncIteratorNextMethodTests
{
    private const string Harness = """
        // An iterable whose iterator answers 1, 2, 3 and then done, through a `next` getter counted in reads.n.
        // 'async' puts it behind @@asyncIterator, 'sync' behind @@iterator.
        function getterSource(reads, kind) {
            const sync = kind === 'sync';
            return {
                [sync ? Symbol.iterator : Symbol.asyncIterator]() {
                    let i = 0;
                    return {
                        get next() {
                            reads.n++;
                            return function () { i++; const r = { value: i, done: i > 3 }; return sync ? r : Promise.resolve(r); };
                        }
                    };
                }
            };
        }

        // An iterable over `it`, whose next answers v1, v2, v3 and then done.
        function replaceableSource(kind) {
            const sync = kind === 'sync';
            const it = {
                i: 0,
                next() { this.i++; const r = { value: 'v' + this.i, done: this.i > 3 }; return sync ? r : Promise.resolve(r); }
            };
            return { it: it, source: { [sync ? Symbol.iterator : Symbol.asyncIterator]() { return it; } } };
        }

        // A next that answers done at once, so a consumer that calls it stops after the value it replaced it at.
        function replacedNext() { return Promise.resolve({ value: 'replaced', done: true }); }

        // The consumers, each running onValue() once per value and answering the values it saw.
        const loops = {
            asyncFunction: async function (source, onValue) {
                const log = [];
                for await (const x of source) { log.push(x); if (onValue) onValue(); }
                return log.join(',');
            },
            asyncGenerator: async function (source, onValue) {
                async function* g() { for await (const x of source) { if (onValue) onValue(); yield x; } }
                const log = [];
                for await (const x of g()) { log.push(x); }
                return log.join(',');
            },
            fromAsync: async function (source, onValue) {
                const values = await Array.fromAsync(source, function (x) { if (onValue) onValue(); return x; });
                return values.join(',');
            },
            fromAsyncWithoutMapper: async function (source) {
                return (await Array.fromAsync(source)).join(',');
            }
        };

        function describe(e) { return e instanceof Error ? e.name : String(e); }
        """;

    private static string Run(string script) =>
        new Engine().Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    /// <summary>
    /// A getter on <c>next</c> runs once, whatever the number of steps. The <c>sync</c> rows are the control. Node 26
    /// reads it twice on Array.fromAsync's sync path; the specification reads it once there too (GetIteratorFromMethod,
    /// then CreateAsyncFromSyncIterator reads the wrapper's own <c>next</c>), and so does Node's <c>for await</c>.
    /// </summary>
    [TestCase("asyncFunction", "async")]
    [TestCase("asyncFunction", "sync")]
    [TestCase("asyncGenerator", "async")]
    [TestCase("asyncGenerator", "sync")]
    [TestCase("fromAsync", "async")]
    [TestCase("fromAsync", "sync")]
    [TestCase("fromAsyncWithoutMapper", "async")]
    [TestCase("fromAsyncWithoutMapper", "sync")]
    public void ANextReachedThroughAGetterIsReadOnce(string loop, string kind)
    {
        Run($$"""
            (async function () {
                const reads = { n: 0 };
                const values = await loops.{{loop}}(getterSource(reads, '{{kind}}'));
                return reads.n + ':' + values;
            })()
            """).Should().Be("1:1,2,3");
    }

    /// <summary>
    /// A <c>next</c> the body replaces after the first value is not the record's, so every step still calls the original.
    /// </summary>
    [TestCase("asyncFunction", "async")]
    [TestCase("asyncFunction", "sync")]
    [TestCase("asyncGenerator", "async")]
    [TestCase("asyncGenerator", "sync")]
    [TestCase("fromAsync", "async")]
    [TestCase("fromAsync", "sync")]
    public void ANextReplacedMidLoopIsNotCalled(string loop, string kind)
    {
        Run($$"""
            (function () {
                const r = replaceableSource('{{kind}}');
                return loops.{{loop}}(r.source, function () { r.it.next = replacedNext; });
            })()
            """).Should().Be("v1,v2,v3");
    }

    /// <summary>
    /// A getter that answers <c>undefined</c> and installs a real <c>next</c> as it does: the record holds
    /// <c>undefined</c>, so the first step's Call throws the TypeError rather than finding the installed method.
    /// </summary>
    [TestCase("asyncFunction")]
    [TestCase("asyncGenerator")]
    [TestCase("fromAsync")]
    public void ANextInstalledAfterTheRecordWasBuiltIsNotCalled(string loop)
    {
        Run($$"""
            (async function () {
                const source = {
                    [Symbol.asyncIterator]() {
                        let i = 0;
                        return {
                            get next() {
                                Object.defineProperty(this, 'next', { value: function () { i++; return Promise.resolve({ value: i, done: i > 2 }); } });
                                return undefined;
                            }
                        };
                    }
                };
                try { return 'values:' + await loops.{{loop}}(source); } catch (e) { return 'caught:' + describe(e); }
            })()
            """).Should().Be("caught:TypeError");
    }

    /// <summary>
    /// An <c>@@asyncIterator</c> that answers a built-in array iterator: Jint steps that one natively as the record, and
    /// still has to call the <c>next</c> the record was built with — not an own <c>next</c> assigned mid-loop, and not a
    /// getter on <c>%ArrayIteratorPrototype%.next</c> once per step.
    /// </summary>
    [TestCase("asyncFunction")]
    [TestCase("asyncGenerator")]
    [TestCase("fromAsync")]
    public void ABuiltInIteratorAnAsyncIteratorAnswersKeepsTheNextItWasBuiltWith(string loop)
    {
        Run($$"""
            (async function () {
                const it = [1, 2, 3].values();
                const own = await loops.{{loop}}({ [Symbol.asyncIterator]() { return it; } }, function () { it.next = replacedNext; });

                const proto = Object.getPrototypeOf([].values());
                const original = Object.getOwnPropertyDescriptor(proto, 'next');
                let reads = 0;
                Object.defineProperty(proto, 'next', { configurable: true, get() { reads++; return original.value; } });
                try {
                    const viaGetter = await loops.{{loop}}({ [Symbol.asyncIterator]() { return [1, 2, 3].values(); } });
                    return own + ' ' + reads + ':' + viaGetter;
                } finally {
                    Object.defineProperty(proto, 'next', original);
                }
            })()
            """).Should().Be("1,2,3 1:1,2,3");
    }

    /// <summary>
    /// The common shape: an async generator object, whose <c>next</c> the loop reads once from
    /// <c>%AsyncGeneratorPrototype%</c>, so an own <c>next</c> assigned mid-loop is never called.
    /// </summary>
    [TestCase("asyncFunction")]
    [TestCase("asyncGenerator")]
    [TestCase("fromAsync")]
    public void AnAsyncGeneratorsNextReplacedMidLoopIsNotCalled(string loop)
    {
        Run($$"""
            (function () {
                const gen = (async function* () { yield 1; yield 2; yield 3; })();
                return loops.{{loop}}(gen, function () { gen.next = replacedNext; });
            })()
            """).Should().Be("1,2,3");
    }
}
