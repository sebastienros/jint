#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins step 7.a.i of <c>yield*</c>
/// (<a href="https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation">YieldExpression evaluation</a>):
/// every step calls the iterator record's [[NextMethod]], which
/// <a href="https://tc39.es/ecma262/#sec-getiteratorfrommethod">GetIteratorFromMethod</a> read off the iterator once, and
/// nothing reads <c>next</c> again.
/// <para>
/// A built-in iterator whose <c>next</c> read found the built-in function is a record Jint steps natively, and such a
/// record has no [[NextMethod]] to hand over, so <c>yield*</c> read <c>next</c> a second time (sebastienros/jint#4424).
/// A getter on <c>next</c> ran twice, a <c>next</c> replaced while the first read ran was the one called, and an
/// iterator object whose <c>next</c> read answered <c>undefined</c> had it read again, so a getter that installed a
/// real <c>next</c> as it answered let the delegation run where its first step should have thrown. Every row runs in a
/// generator and, beside it, in an async generator. The expected values are Node 26's; the CLR rows have no Node
/// counterpart and expect what the array rows do.
/// </para>
/// </summary>
public class YieldStarNextMethodTests
{
    private const string Harness = """
        // A generator of each kind delegating to what makeOperand() answers, and the values it yielded.
        const consumers = {
            sync: async function (makeOperand) {
                function* g() { yield* makeOperand(); }
                const out = [];
                for (const x of g()) { out.push(x); }
                return out.join(',');
            },
            async: async function (makeOperand) {
                async function* g() { yield* makeOperand(); }
                const out = [];
                for await (const x of g()) { out.push(x); }
                return out.join(',');
            }
        };

        const AIP = Object.getPrototypeOf([][Symbol.iterator]());
        const SIP = Object.getPrototypeOf(new Set()[Symbol.iterator]());
        const MIP = Object.getPrototypeOf(new Map()[Symbol.iterator]());
        const StrIP = Object.getPrototypeOf(''[Symbol.iterator]());
        // Built before any `next` is counted: the constructor iterates its argument.
        const typed = new Uint8Array([1, 2]);

        // Each operand, with the prototype whose `next` its iterator finds.
        const operands = {
            array: [AIP, () => [1, 2]],
            arrayIteratorFromIterable: [AIP, () => ({ [Symbol.iterator]() { return [1, 2].values(); } })],
            arrayIteratorFromAsyncIterable: [AIP, () => ({ [Symbol.asyncIterator]() { return [1, 2].values(); } })],
            arrayIteratorObject: [AIP, () => [1, 2].values()],
            argumentsObject: [AIP, () => (function () { return arguments; })(1, 2)],
            typedArray: [AIP, () => typed],
            setIteratorWithOwnProperty: [SIP, () => { const it = new Set([1, 2]).values(); it.tag = 1; return it; }],
            mapIteratorWithOwnProperty: [MIP, () => { const it = new Map([[1, 1], [2, 2]]).keys(); it.tag = 1; return it; }],
            stringIteratorWithOwnProperty: [StrIP, () => { const it = 'ab'[Symbol.iterator](); it.tag = 1; return it; }],
            string: [StrIP, () => 'ab'],
            set: [SIP, () => new Set([1, 2])],
            clrList: [AIP, () => list],
            clrIteratorWithOwnProperty: [AIP, () => { const it = list[Symbol.iterator](); it.tag = 1; return it; }]
        };

        // Runs `run` with `proto.next` behind a getter answering the original, and answers the reads it counted.
        async function countReads(proto, run) {
            const original = Object.getOwnPropertyDescriptor(proto, 'next');
            let reads = 0;
            Object.defineProperty(proto, 'next', { configurable: true, get() { reads++; return original.value; } });
            try { const values = await run(); return reads + ':' + values; }
            finally { Object.defineProperty(proto, 'next', original); }
        }
        """;

    private static string Run(string script)
    {
        var engine = new Engine();
        engine.SetValue("list", new List<int> { 1, 2 });
        return engine.Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();
    }

    /// <summary>
    /// A getter on the <c>next</c> the operand's iterator finds runs once. The rows whose record is not stepped
    /// natively are controls: an iterator object with no own property, a string and a set, whose record reads
    /// <c>next</c> through the getter and holds what it read.
    /// </summary>
    [TestCase("array", "sync")]
    [TestCase("array", "async")]
    [TestCase("arrayIteratorFromIterable", "sync")]
    [TestCase("arrayIteratorFromIterable", "async")]
    [TestCase("arrayIteratorFromAsyncIterable", "async")]
    [TestCase("arrayIteratorObject", "sync")]
    [TestCase("arrayIteratorObject", "async")]
    [TestCase("argumentsObject", "sync")]
    [TestCase("argumentsObject", "async")]
    [TestCase("typedArray", "sync")]
    [TestCase("typedArray", "async")]
    [TestCase("setIteratorWithOwnProperty", "sync")]
    [TestCase("setIteratorWithOwnProperty", "async")]
    [TestCase("mapIteratorWithOwnProperty", "sync")]
    [TestCase("mapIteratorWithOwnProperty", "async")]
    [TestCase("stringIteratorWithOwnProperty", "sync")]
    [TestCase("stringIteratorWithOwnProperty", "async")]
    [TestCase("string", "sync")]
    [TestCase("string", "async")]
    [TestCase("set", "sync")]
    [TestCase("set", "async")]
    [TestCase("clrList", "sync")]
    [TestCase("clrList", "async")]
    [TestCase("clrIteratorWithOwnProperty", "sync")]
    [TestCase("clrIteratorWithOwnProperty", "async")]
    public void TheNextOfTheDelegatedIteratorIsReadOnce(string operand, string kind)
    {
        var expected = operand.StartsWith("string", StringComparison.Ordinal) ? "1:a,b" : "1:1,2";
        Run($"countReads(operands.{operand}[0], () => consumers.{kind}(operands.{operand}[1]))").Should().Be(expected);
    }

    /// <summary>
    /// A getter that replaces <c>%ArrayIteratorPrototype%.next</c> while it is being read, and answers the original: the
    /// record holds the original, so every step is the built-in one and the replacement is never called.
    /// </summary>
    [TestCase("array", "sync")]
    [TestCase("array", "async")]
    [TestCase("arrayIteratorFromIterable", "sync")]
    [TestCase("arrayIteratorFromAsyncIterable", "async")]
    public void ANextReplacedWhileItIsReadIsNotCalled(string operand, string kind)
    {
        Run($$"""
            (async function () {
                const original = Object.getOwnPropertyDescriptor(AIP, 'next');
                Object.defineProperty(AIP, 'next', {
                    configurable: true,
                    get() {
                        Object.defineProperty(AIP, 'next', { configurable: true, writable: true, value: function () { return { value: 'replaced', done: true }; } });
                        return original.value;
                    }
                });
                try { return await consumers.{{kind}}(operands.{{operand}}[1]); }
                finally { Object.defineProperty(AIP, 'next', original); }
            })()
            """).Should().Be("1,2");
    }

    /// <summary>
    /// An iterator whose <c>next</c> getter answers <c>undefined</c> and installs a real <c>next</c> as it does: the
    /// record holds <c>undefined</c>, so the first step's Call throws a <c>TypeError</c> rather than finding the
    /// installed method.
    /// </summary>
    [TestCase("sync")]
    [TestCase("async")]
    public void ANextInstalledAfterTheRecordWasBuiltIsNotCalled(string kind)
    {
        Run($$"""
            (async function () {
                const sync = '{{kind}}' === 'sync';
                const source = {
                    [sync ? Symbol.iterator : Symbol.asyncIterator]() {
                        let i = 0;
                        return {
                            get next() {
                                Object.defineProperty(this, 'next', { value: function () { i++; const r = { value: i, done: i > 2 }; return sync ? r : Promise.resolve(r); } });
                                return undefined;
                            }
                        };
                    }
                };
                try { return 'values:' + await consumers.{{kind}}(() => source); } catch (e) { return 'caught:' + e.constructor.name; }
            })()
            """).Should().Be("caught:TypeError");
    }

    /// <summary>
    /// The control for the record that holds what it read: its <c>next</c> is called with the value each resumption
    /// sent, one argument every time, <c>undefined</c> for the step that starts the delegation.
    /// </summary>
    [TestCase("sync")]
    [TestCase("async")]
    public void TheNextOfAnIteratorObjectIsCalledWithTheValueSent(string kind)
    {
        Run($$"""
            (async function () {
                const sync = '{{kind}}' === 'sync';
                const log = [];
                const source = {
                    [sync ? Symbol.iterator : Symbol.asyncIterator]() {
                        let i = 0;
                        return { next(...args) { log.push(args.length + ':' + args[0]); i++; const r = { value: i, done: i > 2 }; return sync ? r : Promise.resolve(r); } };
                    }
                };
                if (sync) {
                    const it = (function* () { yield* source; })();
                    it.next('a'); it.next('b'); it.next('c');
                } else {
                    const it = (async function* () { yield* source; })();
                    await it.next('a'); await it.next('b'); await it.next('c');
                }
                return log.join(' ');
            })()
            """).Should().Be("1:undefined 1:b 1:c");
    }
}
