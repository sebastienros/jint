#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Which iterator helpers read a step result's <c>value</c>, and which do not. IteratorStepValue
/// (https://tc39.es/ecma262/#sec-iteratorstepvalue) calls <c>next</c>, reads <c>done</c> and then reads <c>value</c>;
/// IteratorStep (https://tc39.es/ecma262/#sec-iteratorstep) stops after <c>done</c>. Every helper steps with
/// IteratorStepValue except the skip loop of <c>Iterator.prototype.drop</c>, whose closure performs
/// <c>Let next be ? IteratorStep(iterated)</c> for each of the first <c>limit</c> elements and reads <c>value</c> only
/// for the elements it keeps (https://tc39.es/ecma262/#sec-iterator.prototype.drop), and the strict-mode check of
/// <c>Iterator.zip</c>, which asks each remaining iterator whether it is still open
/// (https://tc39.es/ecma262/#sec-iteratorzip). Jint stepped the dropped elements with the value read, so
/// <c>Iterator.from(it).drop(1).next()</c> over a result with logging <c>done</c> and <c>value</c> getters logged
/// <c>next,done,value,next,done,value</c> where Node logs <c>next,done,next,done,value</c>, and a <c>value</c> getter
/// that threw failed a <c>drop</c> that never kept that element.
/// <para>
/// The <c>drop</c> and control rows expect Node 26's answer to the same script: the iterator's own log of each
/// <c>next()</c> call, each <c>done</c> and <c>value</c> read on its result, and each <c>return()</c> close, then what
/// the script itself logged. Node has neither <c>AsyncIterator</c> nor <c>includes</c>, <c>join</c>, <c>chunks</c>,
/// <c>windows</c> or <c>Iterator.zip</c>, so those rows are derived from the algorithms
/// (https://tc39.es/proposal-iterator-includes/#sec-iterator.prototype.includes,
/// https://tc39.es/proposal-iterator-join/#sec-iterator.prototype.join,
/// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.chunks,
/// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.windows,
/// https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.drop), and they pin behaviour that was
/// already right: each is a control for the rule the <c>drop</c> rows break.
/// </para>
/// </summary>
public class IteratorHelperStepTests
{
    /// <summary>
    /// The logging iterables. <c>iterableOf</c> answers a result whose <c>done</c> and <c>value</c> are getters that log;
    /// <c>throwingDoneAt(n, ...)</c> throws from the <c>done</c> of its nth step, <c>throwingValue</c> from every
    /// <c>value</c>, and <c>proxyIterableOf</c> answers a Proxy whose <c>get</c> trap logs each property it is asked
    /// for. None of them inherits from %Iterator.prototype%, so <c>Iterator.from</c> wraps it and the helpers see the
    /// script's own result objects.
    /// </summary>
    private const string Prelude = """
        var log = [];
        function iterableOf(values) {
            var i = 0;
            return {
                [Symbol.iterator]() { return this; },
                next() {
                    log.push('next');
                    var has = i < values.length;
                    var v = has ? values[i++] : undefined;
                    return {
                        get done() { log.push('done'); return !has; },
                        get value() { log.push('value'); return v; }
                    };
                },
                return() { log.push('return'); return {}; }
            };
        }
        function throwingDoneAt(n, values) {
            var i = 0, step = 0;
            return {
                [Symbol.iterator]() { return this; },
                next() {
                    log.push('next');
                    step++;
                    var has = i < values.length;
                    var v = has ? values[i++] : undefined;
                    return {
                        get done() { log.push('done'); if (step === n) throw new Error('boom'); return !has; },
                        get value() { log.push('value'); return v; }
                    };
                },
                return() { log.push('return'); return {}; }
            };
        }
        function throwingValue(values) {
            var i = 0;
            return {
                [Symbol.iterator]() { return this; },
                next() {
                    log.push('next');
                    var has = i < values.length;
                    if (has) i++;
                    return {
                        get done() { log.push('done'); return !has; },
                        get value() { log.push('value'); throw new Error('value read'); }
                    };
                },
                return() { log.push('return'); return {}; }
            };
        }
        function proxyIterableOf(values) {
            var i = 0;
            return {
                [Symbol.iterator]() { return this; },
                next() {
                    log.push('next');
                    var has = i < values.length;
                    var v = has ? values[i++] : undefined;
                    return new Proxy({ done: !has, value: v }, { get(t, k) { log.push('get ' + String(k)); return t[k]; } });
                },
                return() { log.push('return'); return {}; }
            };
        }
        """;

    /// <summary>
    /// The asynchronous counterparts, for the rows that run <c>AsyncIterator.prototype.drop</c> beside the synchronous
    /// ones: the same logging results, answered through a promise as an async iterator's <c>next</c> does.
    /// </summary>
    private const string AsyncPrelude = """
        function asyncIterableOf(values) {
            var i = 0;
            return {
                next() {
                    log.push('next');
                    var has = i < values.length;
                    var v = has ? values[i++] : undefined;
                    return Promise.resolve({
                        get done() { log.push('done'); return !has; },
                        get value() { log.push('value'); return v; }
                    });
                },
                return() { log.push('return'); return Promise.resolve({}); }
            };
        }
        function asyncThrowingValue(values) {
            var i = 0;
            return {
                next() {
                    log.push('next');
                    var has = i < values.length;
                    if (has) i++;
                    return Promise.resolve({
                        get done() { log.push('done'); return !has; },
                        get value() { log.push('value'); throw new Error('value read'); }
                    });
                },
                return() { log.push('return'); return Promise.resolve({}); }
            };
        }
        """;

    private static string Run(string script)
    {
        var engine = new Engine();
        engine.Execute(Prelude);
        engine.Execute(AsyncPrelude);
        engine.Execute(script);
        engine.Advanced.ProcessTasks();
        return engine.Evaluate("log.join(',')").AsString();
    }

    // ------------------------------------------------------------------ the rows

    /// <summary>
    /// <c>drop</c> over every kind of iterator and every ending: the skipped elements read <c>done</c> and never
    /// <c>value</c>, the kept ones read both, and a step that throws leaves the iterator unclosed.
    /// </summary>
    public static TheoryData<string, string, string> DropRows() => new()
    {
        { "DropZeroReadsTheFirstValue",
          "Iterator.from(iterableOf([1, 2])).drop(0).next();",
          "next,done,value" },
        { "DropOneSkipsWithoutReadingValue",
          "Iterator.from(iterableOf([1, 2])).drop(1).next();",
          "next,done,next,done,value" },
        { "DropTwoOverTwoElements",
          "Iterator.from(iterableOf([1, 2])).drop(2).next();",
          "next,done,next,done,next,done" },
        { "DropPastTheEnd",
          "Iterator.from(iterableOf([1, 2])).drop(5).next();",
          "next,done,next,done,next,done" },
        { "DropInfinity",
          "Iterator.from(iterableOf([1, 2])).drop(Infinity).next();",
          "next,done,next,done,next,done" },
        { "DropOverAnEmptyIterator",
          "Iterator.from(iterableOf([])).drop(1).next();",
          "next,done" },
        { "DropThenDrain",
          "var h = Iterator.from(iterableOf([1, 2, 3])).drop(1); h.next(); h.next(); h.next(); h.next();",
          "next,done,next,done,value,next,done,value,next,done" },
        { "DropYieldsTheFirstKeptValue",
          "var r = Iterator.from(iterableOf([1, 2, 3])).drop(1).next(); log.push('r=' + r.value + ',' + r.done);",
          "next,done,next,done,value,r=2,false" },
        { "DropOnAPlainReceiver",
          "Iterator.prototype.drop.call(iterableOf([1, 2]), 1).next();",
          "next,done,next,done,value" },
        { "DropOnAPlainReceiverPastTheEnd",
          "Iterator.prototype.drop.call(iterableOf([1, 2]), 3).next();",
          "next,done,next,done,next,done" },
        { "DropOverProxyStepResults",
          "Iterator.from(proxyIterableOf([1, 2])).drop(1).next();",
          "next,get done,next,get done,get value" },
        { "DropOverProxyStepResultsPastTheEnd",
          "Iterator.from(proxyIterableOf([1])).drop(2).next();",
          "next,get done,next,get done" },
        { "DropDoneGetterThrowsWhileSkipping",
          "try { Iterator.from(throwingDoneAt(1, [1, 2])).drop(1).next(); } catch (e) { log.push('caught ' + e.message); }",
          "next,done,caught boom" },
        { "DropDoneGetterThrowsAfterSkipping",
          "try { Iterator.from(throwingDoneAt(2, [1, 2])).drop(1).next(); } catch (e) { log.push('caught ' + e.message); }",
          "next,done,next,done,caught boom" },
        { "DropDoneGetterThrowsOnTheFirstStepOfDropZero",
          "try { Iterator.from(throwingDoneAt(1, [1, 2])).drop(0).next(); } catch (e) { log.push('caught ' + e.message); }",
          "next,done,caught boom" },
        { "DropReturnAfterAFailedSkipClosesNothing",
          "var h = Iterator.from(throwingDoneAt(1, [1, 2])).drop(1); try { h.next(); } catch (e) { log.push('caught ' + e.message); } h.return(); h.next();",
          "next,done,caught boom" },
        { "DropValueGetterThrowsOnTheKeptElementOnly",
          "try { Iterator.from(throwingValue([1, 2])).drop(1).next(); } catch (e) { log.push('caught ' + e.message); }",
          "next,done,next,done,value,caught value read" },
        { "DropValueGetterNeverRunsWhenEveryElementIsDropped",
          "var r = Iterator.from(throwingValue([1])).drop(1).next(); log.push('done=' + r.done);",
          "next,done,next,done,done=true" },
        { "DropValueGetterNeverRunsForDroppedElements",
          "var r = Iterator.from(throwingValue([1, 2, 3])).drop(3).next(); log.push('done=' + r.done);",
          "next,done,next,done,next,done,next,done,done=true" },
        { "DropReturnAfterPartialConsumption",
          "var h = Iterator.from(iterableOf([1, 2, 3])).drop(1); h.next(); h.return();",
          "next,done,next,done,value,return" },
        { "DropReturnBeforeTheFirstStep",
          "Iterator.from(iterableOf([1, 2])).drop(1).return();",
          "return" },
        { "DropOverABuiltInIteratorStillDrops",
          "log.push([1, 2, 3, 4].values().drop(1).toArray().join()); log.push(new Set([1, 2, 3]).values().drop(2).toArray().join()); log.push(Array.from('abc'[Symbol.iterator]().drop(1)).join(''));",
          "2,3,4,3,bc" },
        { "DropOverAGeneratorObject",
          "function* g() { log.push('a'); yield 1; log.push('b'); yield 2; log.push('c'); } var r = g().drop(1).next(); log.push(r.value + ',' + r.done);",
          "a,b,2,false" },
        { "DropPastTheEndOfAGeneratorObject",
          "function* g() { log.push('a'); yield 1; log.push('b'); } var r = g().drop(3).next(); log.push(String(r.value) + ',' + r.done);",
          "a,b,undefined,true" },
        { "DropConsumedByForOf",
          "for (var x of Iterator.from(iterableOf([1, 2, 3])).drop(1)) { log.push('x=' + x); }",
          "next,done,next,done,value,x=2,next,done,value,x=3,next,done" },
        { "DropConsumedByYieldStar",
          "function* g() { yield* Iterator.from(iterableOf([1, 2, 3])).drop(2); } var it = g(); it.next(); it.next();",
          "next,done,next,done,next,done,value,next,done" },
        { "DropConsumedByForAwaitInAnAsyncGenerator",
          "(async function* () { for await (var x of Iterator.from(iterableOf([1, 2, 3])).drop(2)) { log.push('x=' + x); } })().next();",
          "next,done,next,done,next,done,value,x=3,next,done" },
    };

    /// <summary>
    /// The other helpers, which perform IteratorStepValue for every element and so read <c>value</c> right after each
    /// <c>done</c>. Node answers all of them, and none changes with the fix.
    /// </summary>
    public static TheoryData<string, string, string> ControlRows() => new()
    {
        { "TakeReadsEachValueAndClosesAtTheLimit",
          "var h = Iterator.from(iterableOf([1, 2])).take(1); h.next(); h.next();",
          "next,done,value,return" },
        { "TakeZeroReadsNothingAndCloses",
          "Iterator.from(iterableOf([1, 2])).take(0).next();",
          "return" },
        { "FilterReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).filter(function (x) { return x === 2; }).next();",
          "next,done,value,next,done,value" },
        { "MapReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).map(function (x) { return x; }).next();",
          "next,done,value" },
        { "FlatMapReadsEachOuterAndInnerValue",
          "var h = Iterator.from(iterableOf([1, 2])).flatMap(function (x) { return iterableOf([x]); }); h.next(); h.next();",
          "next,done,value,next,done,value,next,done,next,done,value,next,done,value" },
        { "SomeReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).some(function (x) { return x === 2; });",
          "next,done,value,next,done,value,return" },
        { "EveryReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).every(function (x) { return x < 3; });",
          "next,done,value,next,done,value,next,done" },
        { "FindReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).find(function (x) { return x === 2; });",
          "next,done,value,next,done,value,return" },
        { "ForEachReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).forEach(function () {});",
          "next,done,value,next,done,value,next,done" },
        { "ReduceWithAnInitialValueReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).reduce(function (a, b) { return a + b; }, 0);",
          "next,done,value,next,done,value,next,done" },
        { "ReduceWithoutAnInitialValueReadsTheFirstValue",
          "Iterator.from(iterableOf([1, 2])).reduce(function (a, b) { return a + b; });",
          "next,done,value,next,done,value,next,done" },
        { "ReduceOfAnEmptyIteratorReadsNoValue",
          "try { Iterator.from(iterableOf([])).reduce(function (a, b) { return a; }); } catch (e) { log.push(e.constructor.name); }",
          "next,done,TypeError" },
        { "ToArrayReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).toArray();",
          "next,done,value,next,done,value,next,done" },
        { "ConcatReadsEachValue",
          "var h = Iterator.concat(iterableOf([1]), iterableOf([2])); h.next(); h.next(); h.next();",
          "next,done,value,next,done,next,done,value,next,done" },
    };

    /// <summary>
    /// The helpers Node 26 does not have. Each expected log is derived from the algorithm cited in the class summary, and
    /// each was already right: every one of them performs IteratorStepValue, except the strict mode of
    /// <c>Iterator.zip</c>, which performs IteratorStep on the iterators after the first.
    /// </summary>
    public static TheoryData<string, string, string> SpecDerivedRows() => new()
    {
        { "IncludesReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).includes(2);",
          "next,done,value,next,done,value,return" },
        { "IncludesReadsTheValueOfASkippedElement",
          "Iterator.from(iterableOf([1, 2])).includes(1, 1);",
          "next,done,value,next,done,value,next,done" },
        { "JoinReadsEachValue",
          "Iterator.from(iterableOf([1, 2])).join();",
          "next,done,value,next,done,value,next,done" },
        { "ChunksReadsEachValue",
          "Iterator.from(iterableOf([1, 2, 3])).chunks(2).next();",
          "next,done,value,next,done,value" },
        { "WindowsReadsEachValue",
          "Iterator.from(iterableOf([1, 2, 3])).windows(2).next();",
          "next,done,value,next,done,value" },
        { "ZipReadsEachValue",
          "Iterator.zip([iterableOf([1]), iterableOf([2])]).next();",
          "next,done,value,next,done,value" },
        { "ZipLongestReadsEachValueUntilTheLastIteratorIsDone",
          "Array.from(Iterator.zip([iterableOf([1]), iterableOf([1, 2])], { mode: 'longest' }));",
          "next,done,value,next,done,value,next,done,next,done,value,next,done" },
        { "ZipStrictAsksTheRemainingIteratorsWithoutReadingValue",
          "var z = Iterator.zip([iterableOf([1]), iterableOf([2])], { mode: 'strict' }); z.next(); z.next();",
          "next,done,value,next,done,value,next,done,next,done" },
        { "ZipStrictClosesTheIteratorThatIsStillOpenWithoutReadingValue",
          "var z = Iterator.zip([iterableOf([1]), iterableOf([1, 2])], { mode: 'strict' }); z.next(); try { z.next(); } catch (e) { log.push(e.constructor.name); }",
          "next,done,value,next,done,value,next,done,next,done,return,TypeError" },
    };

    /// <summary>
    /// <c>AsyncIterator.prototype.drop</c>, which steps its skipped elements with <c>? Await(? IteratorNext(iterated))</c>
    /// and <c>? IteratorComplete(next)</c> and reads <c>value</c> through IteratorValue only for the elements it yields
    /// (https://tc39.es/proposal-async-iterator-helpers/#sec-asynciteratorprototype.drop). The rows mirror the
    /// synchronous <c>drop</c> rows of nearly the same names, and were right before the fix.
    /// </summary>
    public static TheoryData<string, string, string> AsyncDropRows() => new()
    {
        { "AsyncDropZeroReadsTheFirstValue",
          "AsyncIterator.prototype.drop.call(asyncIterableOf([1, 2]), 0).next().then(function (r) { log.push('r=' + r.value + ',' + r.done); });",
          "next,done,value,r=1,false" },
        { "AsyncDropOneSkipsWithoutReadingValue",
          "AsyncIterator.prototype.drop.call(asyncIterableOf([1, 2]), 1).next().then(function (r) { log.push('r=' + r.value + ',' + r.done); });",
          "next,done,next,done,value,r=2,false" },
        { "AsyncDropPastTheEnd",
          "AsyncIterator.prototype.drop.call(asyncIterableOf([1, 2]), 5).next().then(function (r) { log.push('r=' + r.value + ',' + r.done); });",
          "next,done,next,done,next,done,r=undefined,true" },
        { "AsyncDropValueGetterNeverRunsWhenEveryElementIsDropped",
          "AsyncIterator.prototype.drop.call(asyncThrowingValue([1]), 1).next().then(function (r) { log.push('done=' + r.done); });",
          "next,done,next,done,done=true" },
    };

    // ------------------------------------------------------------------ the tests

    [Theory]
    [MemberData(nameof(DropRows))]
    public void DropReadsValueOnlyForTheElementsItKeeps(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);

    [Theory]
    [MemberData(nameof(ControlRows))]
    public void EveryOtherHelperReadsValueAfterEachDone(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);

    [Theory]
    [MemberData(nameof(SpecDerivedRows))]
    public void TheHelpersNodeLacksReadValueAsTheirAlgorithmsSay(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);

    [Theory]
    [MemberData(nameof(AsyncDropRows))]
    public void AsyncDropReadsValueOnlyForTheElementsItKeeps(string name, string script, string expected)
        => Run(script).Should().Be(expected, name);
}
