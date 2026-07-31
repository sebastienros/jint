using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The immutable-update idioms: object spread, <c>Object.assign</c>, rest destructuring and
/// <c>Object.fromEntries</c>, all copying from stable source objects in a hot loop.
/// <see cref="LiteralClone"/> is the baseline — a plain shaped literal with the same four
/// properties — so every other row reads as a multiple of the layout-equivalent literal cost.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, built by <c>CreateEngine</c> (which
/// installs the shared <see cref="SetupSource"/> fixture every row needs) and warmed with its own script
/// and nothing else (see <see cref="IsolatedScript"/>). It used to be one shared engine warmed with all
/// nine scripts, so each row was measured on an engine carrying its siblings' globals (every script
/// declares <c>f</c>, <c>c</c> and <c>i</c>) and their handler-tree, call-site and object-shape state —
/// and <see cref="AssignExistingTarget"/> mutates the shared <c>acc</c> fixture, which every other row's
/// warm-up then saw. The rows still measure warm dispatch, and engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ObjectSpreadBenchmark
{
    private IsolatedScript _literalClone;
    private IsolatedScript _spreadSmall;
    private IsolatedScript _spreadSmallOverride;
    private IsolatedScript _spreadLarge;
    private IsolatedScript _spreadTwoSources;
    private IsolatedScript _assignFreshTarget;
    private IsolatedScript _assignExistingTarget;
    private IsolatedScript _restDestructuring;
    private IsolatedScript _fromEntriesPairs;

    internal const string SetupSource = """
        var o = { a: 1, b: 2, c: 3, d: 4 };
        var six = { a: 1, b: 2, c: 3, d: 4, e: 5, f: 6 };
        var half1 = { a: 1, b: 2 };
        var half2 = { c: 3, d: 4 };
        var acc = { a: 0, b: 0, c: 0, d: 0 };
        var pairs = [['a', 1], ['b', 2], ['c', 3], ['d', 4]];
        var wide = {};
        (function () {
            for (var i = 0; i < 24; i++) { wide['p' + (i < 10 ? '0' + i : i)] = i; }
        })();
        """;

    internal const string SpreadSmallSource = """
        function f() { var c; for (var i = 0; i < 100000; i++) { c = { ...o }; } return c.d; }
        f();
        """;

    internal const string AssignFreshTargetSource = """
        function f() { var c; for (var i = 0; i < 100000; i++) { c = Object.assign({}, o); } return c.d; }
        f();
        """;

    internal const string RestDestructuringSource = """
        function f() { var rest; for (var i = 0; i < 100000; i++) { var { a, ...r } = six; rest = r; } return rest.f; }
        f();
        """;

    /// <summary>Builds a fresh engine carrying the shared <see cref="SetupSource"/> fixture every row needs.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(SetupSource);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        // the layout-equivalent shaped literal: what a 4-prop copy costs when built as a literal
        _literalClone = IsolatedScript.Warm("""
            function f() { var c; for (var i = 0; i < 100000; i++) { c = { a: o.a, b: o.b, c: o.c, d: o.d }; } return c.d; }
            f();
            """, CreateEngine);

        _spreadSmall = IsolatedScript.Warm(SpreadSmallSource, CreateEngine);

        // spread + trailing static override — the {...defaults, x} config-merge shape
        _spreadSmallOverride = IsolatedScript.Warm("""
            function f() { var c; for (var i = 0; i < 100000; i++) { c = { ...o, d: i }; } return c.d; }
            f();
            """, CreateEngine);

        // 24-prop source: beyond the 4-slot inline capacity and the 16-key linear-scan limit
        _spreadLarge = IsolatedScript.Warm("""
            function f() { var c; for (var i = 0; i < 10000; i++) { c = { ...wide }; } return c.p23; }
            f();
            """, CreateEngine);

        // the two-source options-merge shape
        _spreadTwoSources = IsolatedScript.Warm("""
            function f() { var c; for (var i = 0; i < 100000; i++) { c = { ...half1, ...half2 }; } return c.d; }
            f();
            """, CreateEngine);

        _assignFreshTarget = IsolatedScript.Warm(AssignFreshTargetSource, CreateEngine);

        // assign onto a long-lived target: pure overwrite, no object creation
        _assignExistingTarget = IsolatedScript.Warm("""
            function f() { for (var i = 0; i < 100000; i++) { Object.assign(acc, o); } return acc.d; }
            f();
            """, CreateEngine);

        _restDestructuring = IsolatedScript.Warm(RestDestructuringSource, CreateEngine);

        _fromEntriesPairs = IsolatedScript.Warm("""
            function f() { var c; for (var i = 0; i < 10000; i++) { c = Object.fromEntries(pairs); } return c.d; }
            f();
            """, CreateEngine);
    }

    [Benchmark(Baseline = true)]
    public JsValue LiteralClone() => _literalClone.Run();

    [Benchmark]
    public JsValue SpreadSmall() => _spreadSmall.Run();

    [Benchmark]
    public JsValue SpreadSmallOverride() => _spreadSmallOverride.Run();

    [Benchmark]
    public JsValue SpreadLarge() => _spreadLarge.Run();

    [Benchmark]
    public JsValue SpreadTwoSources() => _spreadTwoSources.Run();

    [Benchmark]
    public JsValue AssignFreshTarget() => _assignFreshTarget.Run();

    [Benchmark]
    public JsValue AssignExistingTarget() => _assignExistingTarget.Run();

    [Benchmark]
    public JsValue RestDestructuring() => _restDestructuring.Run();

    [Benchmark]
    public JsValue FromEntriesPairs() => _fromEntriesPairs.Run();
}
