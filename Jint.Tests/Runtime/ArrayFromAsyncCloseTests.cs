#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins how <a href="https://tc39.es/ecma262/#sec-array.fromasync">Array.fromAsync</a> closes its source when the
/// mapper or storing an element fails: <c>? AsyncIteratorClose(iteratorRecord, completion)</c> with a throw completion
/// (<a href="https://tc39.es/ecma262/#sec-asynciteratorclose">AsyncIteratorClose</a>), which awaits what <c>return</c>
/// answered (step 4.d) before the throw completion's error rejects the result (step 5). So a thenable's <c>then</c> is
/// called, and the rejection settles a tick after a plain answer has. Jint called <c>return</c> and rejected at once
/// (sebastienros/jint#4526).
/// <para>
/// Each row logs its source's calls beside a ten-turn chain of plain promise reactions started first, so the log is the
/// tick order. Expected values are Node 26's, bar the rows where V8 itself departs from the specification, which are
/// derived from the steps cited on each: a <c>return</c> that throws, which V8 never settles; one that rejects, which V8
/// calls again forever; and a <c>done</c> or <c>value</c> getter that throws, where V8 closes a source the
/// specification leaves open. A mapper is handed the value the iterator answered without an Await of its own
/// (https://tc39.es/ecma262/#sec-array.fromasync step 8.d.ix.1), which every mapper row's tick count depends on.
/// </para>
/// </summary>
public class ArrayFromAsyncCloseTests
{
    private const string Harness = """
        var log = [];
        function startTicks() {
            let i = 0;
            const step = () => { log.push('t' + (++i)); if (i < 10) Promise.resolve().then(step); };
            Promise.resolve().then(step);
        }
        function describe(e) { return e instanceof TypeError ? 'TypeError' : String(e); }
        function asyncSource(ret) {
            return { [Symbol.asyncIterator]() { return { next() { log.push('next'); return { value: 1, done: false }; }, return: ret }; } };
        }
        function syncSource(ret) {
            return { [Symbol.iterator]() { return { next() { log.push('next'); return { value: 1, done: false }; }, return: ret }; } };
        }
        const thenable = () => { log.push('return'); return { then(r) { log.push('then'); r({}); } }; };
        const plain = () => { log.push('return'); return {}; };
        const mapperThrows = () => { throw 'M'; };
        const rows = {
            thenable: () => Array.fromAsync(asyncSource(thenable), mapperThrows),
            plainObject: () => Array.fromAsync(asyncSource(plain), mapperThrows),
            noReturn: () => Array.fromAsync(asyncSource(undefined), mapperThrows),
            primitive: () => Array.fromAsync(asyncSource(() => { log.push('return'); return 5; }), mapperThrows),
            returnThrows: () => Array.fromAsync(asyncSource(() => { log.push('return'); throw 'R'; }), mapperThrows),
            returnRejects: () => Array.fromAsync(asyncSource(() => { log.push('return'); return Promise.reject('R'); }), mapperThrows),
            mapperRejects: () => Array.fromAsync(asyncSource(plain), () => Promise.reject('M')),
            mapperRejectsThenable: () => Array.fromAsync(asyncSource(thenable), () => Promise.reject('M')),
            asyncMapperThrows: () => Array.fromAsync(asyncSource(plain), async () => { throw 'M'; }),
            syncSourceThenable: () => Array.fromAsync(syncSource(thenable), mapperThrows),
            syncSourcePlain: () => Array.fromAsync(syncSource(plain), mapperThrows),
            unsettableThenable: () => Array.fromAsync.call(
                function () { return Object.defineProperty({}, '0', { value: 0, writable: false, configurable: false }); },
                asyncSource(thenable)),
            asyncGenerator: () => {
                async function* g() { try { log.push('g'); yield 1; yield 2; } finally { log.push('fin'); } }
                return Array.fromAsync(g(), mapperThrows);
            },
            asyncGeneratorAwaitsInFinally: () => {
                async function* g() { try { yield 1; } finally { await null; log.push('fin'); } }
                return Array.fromAsync(g(), mapperThrows);
            },
            mapperSeesPromise: () => Array.fromAsync({ [Symbol.asyncIterator]() { let i = 0; return {
                next() { log.push('next'); return { value: Promise.resolve(7), done: i++ > 0 }; },
                return: plain }; } }, v => { log.push('map:' + (v instanceof Promise ? 'promise' : v)); return v; }),
            mapperSeesRejectedPromise: () => Array.fromAsync({ [Symbol.asyncIterator]() { let i = 0; return {
                next() { log.push('next'); const p = Promise.reject('V'); p.catch(() => {}); return { value: p, done: i++ > 0 }; },
                return: plain }; } }, () => { log.push('map'); return 1; }),
            doneThrows: () => Array.fromAsync({ [Symbol.asyncIterator]() { return {
                next() { log.push('next'); return { get done() { throw 'D'; } }; }, return: plain }; } }),
            valueThrows: () => Array.fromAsync({ [Symbol.asyncIterator]() { return {
                next() { log.push('next'); return { done: false, get value() { throw 'V'; } }; }, return: plain }; } }, () => 1),
            mapped: () => Array.fromAsync({ [Symbol.asyncIterator]() { let i = 0; return {
                next() { log.push('next'); return { value: i, done: i++ > 1 }; } }; } }, v => { log.push('map' + v); return v * 2; }),
        };
        """;

    private static string Run(string row)
    {
        var engine = new Engine();
        engine.Execute(Harness);

        // Execute drains the event loop before it returns, so the chain starts in the same Execute as the row, and the
        // whole log is written by the time it returns.
        engine.Execute($"startTicks(); rows['{row}']().then(v => log.push('res:' + v), e => log.push('rej:' + describe(e)));");
        return engine.Evaluate("log.join(',')").AsString();
    }

    // Node 26.
    [TestCase("thenable", "next,t1,return,t2,then,t3,t4,rej:M,t5,t6,t7,t8,t9,t10")]
    [TestCase("plainObject", "next,t1,return,t2,t3,rej:M,t4,t5,t6,t7,t8,t9,t10")]
    [TestCase("noReturn", "next,t1,t2,rej:M,t3,t4,t5,t6,t7,t8,t9,t10")]
    [TestCase("primitive", "next,t1,return,t2,t3,rej:M,t4,t5,t6,t7,t8,t9,t10")]
    [TestCase("mapperRejects", "next,t1,t2,return,t3,t4,rej:M,t5,t6,t7,t8,t9,t10")]
    [TestCase("mapperRejectsThenable", "next,t1,t2,return,t3,then,t4,t5,rej:M,t6,t7,t8,t9,t10")]
    [TestCase("asyncMapperThrows", "next,t1,t2,return,t3,t4,rej:M,t5,t6,t7,t8,t9,t10")]
    [TestCase("syncSourceThenable", "next,t1,t2,return,t3,t4,t5,rej:M,t6,t7,t8,t9,t10")]
    [TestCase("syncSourcePlain", "next,t1,t2,return,t3,t4,t5,rej:M,t6,t7,t8,t9,t10")]
    [TestCase("unsettableThenable", "next,t1,return,t2,then,t3,t4,rej:TypeError,t5,t6,t7,t8,t9,t10")]
    [TestCase("asyncGenerator", "g,t1,t2,t3,fin,t4,t5,rej:M,t6,t7,t8,t9,t10")]
    [TestCase("asyncGeneratorAwaitsInFinally", "t1,t2,t3,t4,fin,t5,t6,rej:M,t7,t8,t9,t10")]
    [TestCase("mapperSeesPromise", "next,t1,map:promise,t2,next,t3,t4,res:7,t5,t6,t7,t8,t9,t10")]
    [TestCase("mapperSeesRejectedPromise", "next,t1,map,t2,next,t3,t4,res:1,t5,t6,t7,t8,t9,t10")]
    [TestCase("mapped", "next,t1,map0,t2,next,t3,map1,t4,next,t5,t6,res:0,2,t7,t8,t9,t10")]
    // AsyncIteratorClose step 4.c: calling `return` threw, so there is nothing to await (step 4.d awaits only a normal
    // innerResult), and step 5 answers the throw completion at once. V8 never settles this promise.
    [TestCase("returnThrows", "next,t1,return,t2,rej:M,t3,t4,t5,t6,t7,t8,t9,t10")]
    // Step 4.d awaits the rejected promise, one reaction like a fulfilled one, and step 5 keeps the mapper's error over
    // the rejection. `return` is called once; V8 calls it again each time the close rejects.
    [TestCase("returnRejects", "next,t1,return,t2,t3,rej:M,t4,t5,t6,t7,t8,t9,t10")]
    // Steps 8.d.vi and 8.d.viii: ? IteratorComplete(nextResult) and ? IteratorValue(nextResult) reject the result in the
    // job that took the step, and close nothing. V8 calls `return` first. Jint used to let the error escape the reaction,
    // and the result never settled.
    [TestCase("doneThrows", "next,t1,t2,rej:D,t3,t4,t5,t6,t7,t8,t9,t10")]
    [TestCase("valueThrows", "next,t1,t2,rej:V,t3,t4,t5,t6,t7,t8,t9,t10")]
    public void ClosesTheSourceAndAwaitsWhatReturnAnswered(string row, string expected)
    {
        Run(row).Should().Be(expected);
    }
}
