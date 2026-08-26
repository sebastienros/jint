namespace Jint.Tests.Runtime;

/// <summary>
/// Pins who closes an iterator and who does not.
/// <para>
/// <a href="https://tc39.es/ecma262/#sec-iteratorstepvalue">IteratorStepValue</a> — and the
/// IteratorStep/IteratorNext it is built from — sets <c>iteratorRecord.[[Done]]</c> on every abrupt
/// completion the step itself produces: <c>next()</c> throwing, <c>next()</c> answering a non-object,
/// and the <c>done</c>/<c>value</c> reads. Callers propagate such a completion with <c>?</c>, so
/// <a href="https://tc39.es/ecma262/#sec-iteratorclose">IteratorClose</a> is never reached for any of
/// them. Only the <em>consumer's</em> own abrupt completion — a mapper, an adder, a loop body —
/// closes. Reaching the end of the iteration closes nothing either.
/// </para>
/// <para>
/// Every consumer below used to wrap the step, the value read and the processing in one
/// <c>try</c>/<c>catch</c> that closed on anything, so all three step failures wrongly closed; and the
/// one consumer that did distinguish them (<c>AddEntriesFromIterable</c>, behind <c>Map</c>/
/// <c>WeakMap</c>) never re-armed that distinction per iteration, so a <c>next()</c> that threw on the
/// <em>second</em> step closed anyway. for-of additionally closed on normal exhaustion, which
/// <a href="https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset">ForIn/OfBodyEvaluation</a>
/// step 8.e ("If done is true, return iterationResult") does not do.
/// </para>
/// </summary>
public class IteratorCloseTests
{
    /// <summary>
    /// The verdict string every consumer must produce: one <c>true</c>/<c>false</c> per mode, in the
    /// order of <c>MODES</c> below — a step failure never closes, the consumer's own failure always
    /// does, and running out closes nothing.
    /// </summary>
    private const string Expected = "next-throws=false,done-throws=false,value-throws=false,consumer-throws=true,second-next-throws=false,exhausted=false";

    private const string Harness = """
        // An iterable whose iterator records whether return() was called, and which can be told to
        // fail at one specific point of the iteration protocol.
        function makeIterable(mode, values) {
            const record = { closed: false, closeCount: 0 };
            let i = 0;
            record.iterable = {
                [Symbol.iterator]() {
                    return {
                        next() {
                            i++;
                            if (mode === 'next-throws') { throw 'from next'; }
                            if (mode === 'second-next-throws' && i === 2) { throw 'from second next'; }
                            if (i > values.length) { return { done: true, value: undefined }; }
                            const value = values[i - 1];
                            if (mode === 'done-throws') { return { value: value, get done() { throw 'from done'; } }; }
                            if (mode === 'value-throws') { return { done: false, get value() { throw 'from value'; } }; }
                            return { done: false, value: value };
                        },
                        return() {
                            record.closed = true;
                            record.closeCount++;
                            return {};
                        }
                    };
                }
            };
            return record;
        }

        const MODES = ['next-throws', 'done-throws', 'value-throws', 'consumer-throws', 'second-next-throws', 'exhausted'];

        // Runs the consumer `consumerFor(mode)` over a fresh iterable for every mode and reports the
        // close verdict of each. 'consumer-throws' gets a perfectly well-behaved iterable — it is the
        // consumer that fails — and 'exhausted' gets one that simply runs out.
        function verdicts(consumerFor, values) {
            return MODES.map(function (mode) {
                const iterableMode = (mode === 'consumer-throws' || mode === 'exhausted') ? '' : mode;
                const record = makeIterable(iterableMode, values);
                try { consumerFor(mode)(record.iterable); } catch (e) { }
                if (record.closeCount > 1) { throw new Error(mode + ': closed ' + record.closeCount + ' times'); }
                return mode + '=' + record.closed;
            }).join(',');
        }

        const PAIRS = [[{}, {}], [{}, {}]];
        const OBJECTS = [{}, {}];
        """;

    private static string Run(string script) => new Engine().Evaluate(Harness + "\n" + script).AsString();

    [Test]
    public void ArrayFromClosesOnlyForTheMappersOwnFailure()
    {
        Run("""
            verdicts(function (mode) {
                if (mode === 'consumer-throws') {
                    return function (it) { Array.from(it, function () { throw 'from mapper'; }); };
                }
                return function (it) { Array.from(it); };
            }, OBJECTS);
            """).Should().Be(Expected);
    }

    /// <summary>
    /// The same, through the other <c>Array.from</c> branch: a subclass receiver drives the shared
    /// <c>IteratorProtocol.Execute</c> loop instead of the plain-array builder.
    /// </summary>
    [Test]
    public void ArraySubclassFromClosesOnlyForTheMappersOwnFailure()
    {
        Run("""
            class MyArray extends Array { }
            verdicts(function (mode) {
                if (mode === 'consumer-throws') {
                    return function (it) { MyArray.from(it, function () { throw 'from mapper'; }); };
                }
                return function (it) { MyArray.from(it); };
            }, OBJECTS);
            """).Should().Be(Expected);
    }

    [Test]
    public void MapConstructorClosesOnlyForTheAddersOwnFailure()
    {
        Run("""
            class ThrowingMap extends Map { set(k, v) { throw 'from set'; } }
            verdicts(function (mode) {
                const ctor = mode === 'consumer-throws' ? ThrowingMap : Map;
                return function (it) { new ctor(it); };
            }, PAIRS);
            """).Should().Be(Expected);
    }

    [Test]
    public void SetConstructorClosesOnlyForTheAddersOwnFailure()
    {
        Run("""
            class ThrowingSet extends Set { add(v) { throw 'from add'; } }
            verdicts(function (mode) {
                const ctor = mode === 'consumer-throws' ? ThrowingSet : Set;
                return function (it) { new ctor(it); };
            }, OBJECTS);
            """).Should().Be(Expected);
    }

    [Test]
    public void WeakMapConstructorClosesOnlyForTheAddersOwnFailure()
    {
        Run("""
            class ThrowingWeakMap extends WeakMap { set(k, v) { throw 'from set'; } }
            verdicts(function (mode) {
                const ctor = mode === 'consumer-throws' ? ThrowingWeakMap : WeakMap;
                return function (it) { new ctor(it); };
            }, PAIRS);
            """).Should().Be(Expected);
    }

    [Test]
    public void WeakSetConstructorClosesOnlyForTheAddersOwnFailure()
    {
        Run("""
            class ThrowingWeakSet extends WeakSet { add(v) { throw 'from add'; } }
            verdicts(function (mode) {
                const ctor = mode === 'consumer-throws' ? ThrowingWeakSet : WeakSet;
                return function (it) { new ctor(it); };
            }, OBJECTS);
            """).Should().Be(Expected);
    }

    [Test]
    public void ForOfClosesOnlyForTheBodysOwnFailure()
    {
        Run("""
            verdicts(function (mode) {
                if (mode === 'consumer-throws') {
                    return function (it) { for (const x of it) { throw 'from body'; } };
                }
                return function (it) { for (const x of it) { } };
            }, OBJECTS);
            """).Should().Be(Expected);
    }

    /// <summary>
    /// The control for the entry above: an abrupt completion of the loop <em>statement</em> still
    /// closes, which is what <c>staging/sm/statements/for-of-iterator-close.js</c> already asserted
    /// and what must not regress while the exhaustion case is fixed.
    /// </summary>
    [Test]
    public void ForOfStillClosesOnBreakReturnAndAnAbruptLhs()
    {
        Run("""
            function closedBy(consume) {
                const record = makeIterable('', OBJECTS);
                try { consume(record.iterable); } catch (e) { }
                return record.closed + '/' + record.closeCount;
            }

            [
                'break=' + closedBy(function (it) { for (const x of it) { break; } }),
                'return=' + closedBy(function (it) { (function () { for (const x of it) { return 1; } })(); }),
                'lhs=' + closedBy(function (it) { function bang() { throw 'from lhs'; } for ((bang().x) of it) { } }),
                'continue=' + closedBy(function (it) { outer: do { for (const x of it) { continue outer; } } while (false); })
            ].join(',');
            """).Should().Be("break=true/1,return=true/1,lhs=true/1,continue=true/1");
    }

    /// <summary>
    /// The <c>value</c> read is part of the step for a destructuring for-of too — a throwing getter
    /// there must not close, while the destructuring itself failing must.
    /// </summary>
    [Test]
    public void ForOfWithADestructuringTargetSeparatesTheStepFromThePattern()
    {
        Run("""
            function closedBy(mode, values, consume) {
                const record = makeIterable(mode, values);
                try { consume(record.iterable); } catch (e) { }
                return record.closed;
            }

            [
                'step=' + closedBy('value-throws', PAIRS, function (it) { for (const [a, b] of it) { } }),
                'pattern=' + closedBy('', [{ [Symbol.iterator]() { throw 'from pattern'; } }], function (it) { for (const [a] of it) { } })
            ].join(',');
            """).Should().Be("step=false,pattern=true");
    }
}
