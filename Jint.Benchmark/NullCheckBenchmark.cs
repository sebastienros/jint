using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The idiomatic "is null or undefined" test over an array — `if (arr[i] != null) sum++;` and the
/// `== null` form — the shape the build-time loose-equality fusion targets. The array mixes
/// null/undefined holes with numbers and objects so the branch is not memorizable, exercising the
/// fused single type test against the generic evaluate-both-sides + IsLooselyEqual dispatch.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class NullCheckBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _looseNotEqualNull;
    private Prepared<Script> _looseEqualNull;

    private const string SetupSource = """
        var arr = [];
        (function () {
            var seed = 20260715;
            for (var i = 0; i < 1024; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                var pick = (seed >>> 4) & 3;
                if (pick === 0) { arr.push(null); }
                else if (pick === 1) { arr.push(undefined); }
                else if (pick === 2) { arr.push(seed & 255); }
                else { arr.push({ v: i }); }
            }
        })();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        _looseNotEqualNull = Engine.PrepareScript("""
            function f() {
                var sum = 0;
                for (var pass = 0; pass < 100; pass++) {
                    for (var i = 0; i < arr.length; i++) {
                        if (arr[i] != null) { sum++; }
                    }
                }
                return sum;
            }
            f();
            """);

        _looseEqualNull = Engine.PrepareScript("""
            function f() {
                var sum = 0;
                for (var pass = 0; pass < 100; pass++) {
                    for (var i = 0; i < arr.length; i++) {
                        if (arr[i] == null) { sum++; }
                    }
                }
                return sum;
            }
            f();
            """);

        _engine.Evaluate(_looseNotEqualNull);
        _engine.Evaluate(_looseEqualNull);
    }

    [Benchmark]
    public JsValue LooseNotEqualNull() => _engine.Evaluate(_looseNotEqualNull);

    [Benchmark]
    public JsValue LooseEqualNull() => _engine.Evaluate(_looseEqualNull);
}
