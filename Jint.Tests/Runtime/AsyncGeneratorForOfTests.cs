using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Regression coverage for a defect where a synchronous <c>for...of</c> loop inside an
/// async generator lost its iterator position across a <c>yield</c>/<c>await</c> suspension:
/// suspend state was saved for sync generators (ExecutionContext.Generator) and async
/// functions (ExecutionContext.AsyncFunction) but never for async generators
/// (ExecutionContext.AsyncGenerator), so every resume re-entered the statement with no
/// saved state, re-evaluated the head into a fresh iterator, replayed the first step into
/// the consumed resume value, and re-yielded the second element forever (a,b,b,b,...).
/// </summary>
public class AsyncGeneratorForOfTests
{
    // Every generator drain is bounded by a guard so a regression fails loudly with the
    // RUNAWAY marker in the produced sequence instead of hanging the test run forever.
    private static string EvaluateWithGuard(string script)
    {
        var engine = new Engine();
        var result = engine.Evaluate(script);
        return result.UnwrapIfPromise().AsString();
    }

    private static string DrainAsyncGenerator(string generatorBody, string arrayLiteral = "['a', 'b', 'c', 'd']")
    {
        return EvaluateWithGuard($$"""
            (async function () {
                async function* gen(arr) { {{generatorBody}} }
                const arr = {{arrayLiteral}};
                const seen = [];
                let guard = 0;
                for await (const x of gen(arr)) {
                    seen.push(x);
                    if (++guard > 12) { seen.push('RUNAWAY'); break; }
                }
                return seen.join(',');
            })()
            """);
    }

    [Fact]
    public void ForOfWithYieldPreservesIteratorPositionAcrossSuspension()
    {
        // The core defect: for...of over an array, yielding each element.
        var result = DrainAsyncGenerator("for (const x of arr) { yield x; }");
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void IndexBasedForWithYieldStaysCorrect()
    {
        // Control: index-based iteration never depended on iterator suspend state.
        var result = DrainAsyncGenerator("for (let i = 0; i < arr.length; i++) { yield arr[i]; }");
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void ForOfWithoutYieldInBodyStaysCorrect()
    {
        // Control: no suspension inside the loop; yields happen after it completes.
        var result = DrainAsyncGenerator("""
            const collected = [];
            for (const x of arr) { collected.push(x); }
            for (let i = 0; i < collected.length; i++) { yield collected[i]; }
            """);
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void ForOfWithAwaitInBodyPreservesIteratorPosition()
    {
        // Await (not just yield) suspends the async generator inside the loop body too.
        var result = DrainAsyncGenerator("for (const x of arr) { await Promise.resolve(); yield x; }");
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void ForOfWithYieldOverMapPreservesIteratorPosition()
    {
        var result = EvaluateWithGuard("""
            (async function () {
                async function* gen(map) { for (const [k, v] of map) { yield k + '=' + v; } }
                const map = new Map([['a', 1], ['b', 2], ['c', 3]]);
                const seen = [];
                let guard = 0;
                for await (const x of gen(map)) {
                    seen.push(x);
                    if (++guard > 12) { seen.push('RUNAWAY'); break; }
                }
                return seen.join(',');
            })()
            """);
        result.Should().Be("a=1,b=2,c=3");
    }

    [Fact]
    public void ForOfWithYieldOverSetPreservesIteratorPosition()
    {
        var result = EvaluateWithGuard("""
            (async function () {
                async function* gen(set) { for (const x of set) { yield x; } }
                const set = new Set(['a', 'b', 'c', 'd']);
                const seen = [];
                let guard = 0;
                for await (const x of gen(set)) {
                    seen.push(x);
                    if (++guard > 12) { seen.push('RUNAWAY'); break; }
                }
                return seen.join(',');
            })()
            """);
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void ForOfWithYieldOverStringPreservesIteratorPosition()
    {
        var result = EvaluateWithGuard("""
            (async function () {
                async function* gen(s) { for (const ch of s) { yield ch; } }
                const seen = [];
                let guard = 0;
                for await (const x of gen('abcd')) {
                    seen.push(x);
                    if (++guard > 12) { seen.push('RUNAWAY'); break; }
                }
                return seen.join(',');
            })()
            """);
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void ForOfWithYieldOverSyncGeneratorIterablePreservesIteratorPosition()
    {
        // A one-shot iterator makes any hidden head re-evaluation fatal rather than silent:
        // a fresh iterator could not even replay earlier elements.
        var result = EvaluateWithGuard("""
            (async function () {
                function* source() { yield 'a'; yield 'b'; yield 'c'; yield 'd'; }
                async function* gen(iterable) { for (const x of iterable) { yield x; } }
                const seen = [];
                let guard = 0;
                for await (const x of gen(source())) {
                    seen.push(x);
                    if (++guard > 12) { seen.push('RUNAWAY'); break; }
                }
                return seen.join(',');
            })()
            """);
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void ForOfWithYieldSupportsBreakAcrossSuspension()
    {
        var result = DrainAsyncGenerator("""
            for (const x of arr) {
                if (x === 'c') { break; }
                yield x;
            }
            yield 'end';
            """);
        result.Should().Be("a,b,end");
    }

    [Fact]
    public void ForOfWithYieldSupportsContinueAcrossSuspension()
    {
        var result = DrainAsyncGenerator("""
            for (const x of arr) {
                if (x === 'b') { continue; }
                yield x;
            }
            """);
        result.Should().Be("a,c,d");
    }

    [Fact]
    public void ForOfWithYieldSupportsEarlyReturnAcrossSuspension()
    {
        var result = DrainAsyncGenerator("""
            for (const x of arr) {
                yield x;
                if (x === 'b') { return; }
            }
            yield 'unreachable';
            """);
        result.Should().Be("a,b");
    }

    [Fact]
    public void NestedForOfWithYieldPreservesBothIteratorPositions()
    {
        var result = EvaluateWithGuard("""
            (async function () {
                async function* gen(outer, inner) {
                    for (const o of outer) {
                        for (const i of inner) {
                            yield o + i;
                        }
                    }
                }
                const seen = [];
                let guard = 0;
                for await (const x of gen(['a', 'b'], ['1', '2'])) {
                    seen.push(x);
                    if (++guard > 12) { seen.push('RUNAWAY'); break; }
                }
                return seen.join(',');
            })()
            """);
        result.Should().Be("a1,a2,b1,b2");
    }

    [Fact]
    public void SequentialForOfLoopsWithYieldEachPreserveTheirIterator()
    {
        var result = DrainAsyncGenerator("""
            for (const x of arr) { yield '1' + x; }
            for (const x of arr) { yield '2' + x; }
            """, "['a', 'b']");
        result.Should().Be("1a,1b,2a,2b");
    }

    [Fact]
    public void ForOfWithYieldOverEmptyIterableCompletesImmediately()
    {
        var result = DrainAsyncGenerator("""
            for (const x of arr) { yield x; }
            yield 'end';
            """, "[]");
        result.Should().Be("end");
    }

    [Fact]
    public void ForOfWithYieldOverSingleElementIterableYieldsOnce()
    {
        var result = DrainAsyncGenerator("""
            for (const x of arr) { yield x; }
            yield 'end';
            """, "['a']");
        result.Should().Be("a,end");
    }

    [Fact]
    public void ForOfWithMultipleYieldsPerIterationPreservesIteratorPosition()
    {
        var result = DrainAsyncGenerator("""
            for (const x of arr) {
                yield x + '1';
                yield x + '2';
            }
            """, "['a', 'b']");
        result.Should().Be("a1,a2,b1,b2");
    }

    [Fact]
    public void ForAwaitOfWithYieldOverAsyncIterablePreservesIteratorPosition()
    {
        // for-await-of over an async iterable takes the ForAwaitSuspendData path;
        // pinned here so both suspend-state routes stay covered.
        var result = EvaluateWithGuard("""
            (async function () {
                async function* source() { yield 'a'; yield 'b'; yield 'c'; yield 'd'; }
                async function* gen(iterable) { for await (const x of iterable) { yield x; } }
                const seen = [];
                let guard = 0;
                for await (const x of gen(source())) {
                    seen.push(x);
                    if (++guard > 12) { seen.push('RUNAWAY'); break; }
                }
                return seen.join(',');
            })()
            """);
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void SyncGeneratorForOfWithYieldStaysCorrect()
    {
        // The sync-generator route (ExecutionContext.Generator) already saved suspend
        // state before the fix; pinned so the async-generator change cannot regress it.
        var result = EvaluateWithGuard("""
            (function () {
                function* gen(arr) { for (const x of arr) { yield x; } }
                const seen = [];
                let guard = 0;
                for (const x of gen(['a', 'b', 'c', 'd'])) {
                    seen.push(x);
                    if (++guard > 12) { seen.push('RUNAWAY'); break; }
                }
                return seen.join(',');
            })()
            """);
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void AsyncFunctionForOfWithAwaitStaysCorrect()
    {
        // The async-function route (ExecutionContext.AsyncFunction) already saved suspend
        // state before the fix; pinned so the async-generator change cannot regress it.
        var result = EvaluateWithGuard("""
            (async function () {
                const seen = [];
                let guard = 0;
                for (const x of ['a', 'b', 'c', 'd']) {
                    await Promise.resolve();
                    seen.push(x);
                    if (++guard > 12) { seen.push('RUNAWAY'); break; }
                }
                return seen.join(',');
            })()
            """);
        result.Should().Be("a,b,c,d");
    }

    [Fact]
    public void ForOfWithYieldAccumulatesBodyCompletionValue()
    {
        // The generator's overall completion value flows through the loop's accumulated
        // value bookkeeping, which the fix also extends to async generators.
        var result = EvaluateWithGuard("""
            (async function () {
                async function* gen(arr) { for (const x of arr) { yield x; } return 'done'; }
                const g = gen(['a', 'b']);
                const parts = [];
                let step = await g.next();
                let guard = 0;
                while (!step.done) {
                    parts.push(step.value);
                    if (++guard > 12) { parts.push('RUNAWAY'); break; }
                    step = await g.next();
                }
                parts.push('final=' + step.value);
                return parts.join(',');
            })()
            """);
        result.Should().Be("a,b,final=done");
    }
}
