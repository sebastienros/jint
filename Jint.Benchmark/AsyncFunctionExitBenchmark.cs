using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Measures async-function and async-generator EXIT cost in two shapes:
///   - "Plain": no `await using` declarations in scope; the dispose state machine
///     is skipped via the HasDisposeResources fast path. Goal: zero allocation
///     beyond the function/promise plumbing itself.
///   - "WithSyncUsing": one `using` declaration with a sync dispose method; the
///     state machine runs but never suspends.
/// Compares against the pre-PR-2478 "everything inline-disposes" code path by
/// running many iterations so per-call allocation differences dominate.
/// </summary>
[MemoryDiagnoser]
public class AsyncFunctionExitBenchmark
{
    private Prepared<Script> _plainAsyncExits;
    private Prepared<Script> _asyncExitsWithSyncUsing;
    private Prepared<Script> _asyncGenExitsPlain;

    [GlobalSetup]
    public void Setup()
    {
        // Plain async function exit, no using — exercises the HasDisposeResources fast path.
        _plainAsyncExits = Engine.PrepareScript("""
            async function f(n) { return n + 1; }
            for (let i = 0; i < 1000; i++) f(i);
            """);

        // Async function with a sync `using` — state machine runs but never suspends.
        _asyncExitsWithSyncUsing = Engine.PrepareScript("""
            const sentinel = { [Symbol.dispose]() {} };
            async function f(n) {
                using d = sentinel;
                return n + 1;
            }
            for (let i = 0; i < 1000; i++) f(i);
            """);

        // Plain async generator exit, no using — covers the AsyncGenerator fast path.
        _asyncGenExitsPlain = Engine.PrepareScript("""
            async function* g(n) { yield n; yield n + 1; }
            for (let i = 0; i < 500; i++) {
                const it = g(i);
                it.next();
                it.next();
                it.next();
            }
            """);
    }

    [Benchmark]
    public void PlainAsyncFunctionExit_1000() => new Engine().Execute(_plainAsyncExits);

    [Benchmark]
    public void AsyncFunctionWithSyncUsing_1000() => new Engine().Execute(_asyncExitsWithSyncUsing);

    [Benchmark]
    public void PlainAsyncGeneratorExit_500() => new Engine().Execute(_asyncGenExitsPlain);
}
