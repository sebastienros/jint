using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The immutable-update idioms: object spread, <c>Object.assign</c>, rest destructuring and
/// <c>Object.fromEntries</c>, all copying from stable source objects in a hot loop.
/// <see cref="LiteralClone"/> is the baseline — a plain shaped literal with the same four
/// properties — so every other row reads as a multiple of the layout-equivalent literal cost.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ObjectSpreadBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _literalClone;
    private Prepared<Script> _spreadSmall;
    private Prepared<Script> _spreadSmallOverride;
    private Prepared<Script> _spreadLarge;
    private Prepared<Script> _spreadTwoSources;
    private Prepared<Script> _assignFreshTarget;
    private Prepared<Script> _assignExistingTarget;
    private Prepared<Script> _restDestructuring;
    private Prepared<Script> _fromEntriesPairs;

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

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        // the layout-equivalent shaped literal: what a 4-prop copy costs when built as a literal
        _literalClone = Engine.PrepareScript("""
            function f() { var c; for (var i = 0; i < 100000; i++) { c = { a: o.a, b: o.b, c: o.c, d: o.d }; } return c.d; }
            f();
            """);

        _spreadSmall = Engine.PrepareScript(SpreadSmallSource);

        // spread + trailing static override — the {...defaults, x} config-merge shape
        _spreadSmallOverride = Engine.PrepareScript("""
            function f() { var c; for (var i = 0; i < 100000; i++) { c = { ...o, d: i }; } return c.d; }
            f();
            """);

        // 24-prop source: beyond the 4-slot inline capacity and the 16-key linear-scan limit
        _spreadLarge = Engine.PrepareScript("""
            function f() { var c; for (var i = 0; i < 10000; i++) { c = { ...wide }; } return c.p23; }
            f();
            """);

        // the two-source options-merge shape
        _spreadTwoSources = Engine.PrepareScript("""
            function f() { var c; for (var i = 0; i < 100000; i++) { c = { ...half1, ...half2 }; } return c.d; }
            f();
            """);

        _assignFreshTarget = Engine.PrepareScript(AssignFreshTargetSource);

        // assign onto a long-lived target: pure overwrite, no object creation
        _assignExistingTarget = Engine.PrepareScript("""
            function f() { for (var i = 0; i < 100000; i++) { Object.assign(acc, o); } return acc.d; }
            f();
            """);

        _restDestructuring = Engine.PrepareScript(RestDestructuringSource);

        _fromEntriesPairs = Engine.PrepareScript("""
            function f() { var c; for (var i = 0; i < 10000; i++) { c = Object.fromEntries(pairs); } return c.d; }
            f();
            """);

        _engine.Evaluate(_literalClone);
        _engine.Evaluate(_spreadSmall);
        _engine.Evaluate(_spreadSmallOverride);
        _engine.Evaluate(_spreadLarge);
        _engine.Evaluate(_spreadTwoSources);
        _engine.Evaluate(_assignFreshTarget);
        _engine.Evaluate(_assignExistingTarget);
        _engine.Evaluate(_restDestructuring);
        _engine.Evaluate(_fromEntriesPairs);
    }

    [Benchmark(Baseline = true)]
    public JsValue LiteralClone() => _engine.Evaluate(_literalClone);

    [Benchmark]
    public JsValue SpreadSmall() => _engine.Evaluate(_spreadSmall);

    [Benchmark]
    public JsValue SpreadSmallOverride() => _engine.Evaluate(_spreadSmallOverride);

    [Benchmark]
    public JsValue SpreadLarge() => _engine.Evaluate(_spreadLarge);

    [Benchmark]
    public JsValue SpreadTwoSources() => _engine.Evaluate(_spreadTwoSources);

    [Benchmark]
    public JsValue AssignFreshTarget() => _engine.Evaluate(_assignFreshTarget);

    [Benchmark]
    public JsValue AssignExistingTarget() => _engine.Evaluate(_assignExistingTarget);

    [Benchmark]
    public JsValue RestDestructuring() => _engine.Evaluate(_restDestructuring);

    [Benchmark]
    public JsValue FromEntriesPairs() => _engine.Evaluate(_fromEntriesPairs);
}
