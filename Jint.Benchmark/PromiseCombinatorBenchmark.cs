using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// <c>Promise.all</c>, <c>allSettled</c> and <c>any</c> over <see cref="Count"/> already-settled promises, one
/// evaluation each: build the promises, call the combinator, and drain the event loop until it settles. These
/// three decide when they are done with the spec's remainingElementsCount; a scan of the result list on every
/// settle used to make <see cref="Count"/> elements cost O(n^2) (issue #4459), which is what the 10,000 rows show
/// and the 100 rows barely see.
///
/// <para><see cref="Race"/> is the control: the same promises and the same event-loop drain, but a combinator
/// that never counts its elements, so a change to that counting cannot reach it.</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own default engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>); the scripts keep their state in a function scope, so a warmed engine
/// carries no globals from the row's earlier invocations. Engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class PromiseCombinatorBenchmark
{
    private IsolatedScript _all;
    private IsolatedScript _allSettled;
    private IsolatedScript _any;
    private IsolatedScript _race;

    [Params(100, 10_000)]
    public int Count { get; set; }

    private string Source(string combine) => $$"""
        (function () {
            var ps = [];
            for (var i = 0; i < {{Count}}; i++) { ps.push(Promise.resolve(i)); }
            return {{combine}};
        })();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _all = IsolatedScript.Warm(Source("Promise.all(ps)"));
        _allSettled = IsolatedScript.Warm(Source("Promise.allSettled(ps)"));
        _any = IsolatedScript.Warm(Source("Promise.any(ps.map(function (p) { return p.then(function (x) { throw x; }); }))['catch'](function (e) { return e.errors.length; })"));
        _race = IsolatedScript.Warm(Source("Promise.race(ps)"));
    }

    [Benchmark]
    public void All() => _all.Execute();

    [Benchmark]
    public void AllSettled() => _allSettled.Execute();

    /// <summary>Every element rejects, so the combinator counts all of them down to the AggregateError.</summary>
    [Benchmark]
    public void Any() => _any.Execute();

    /// <summary>Control: the change under test cannot reach it.</summary>
    [Benchmark]
    public void Race() => _race.Execute();
}
