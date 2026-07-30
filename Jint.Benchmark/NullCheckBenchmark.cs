using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The idiomatic "is null or undefined" test over an array — `if (arr[i] != null) sum++;` and the
/// `== null` form — the shape the build-time loose-equality fusion targets. The array mixes
/// null/undefined holes with numbers and objects so the branch is not memorizable, exercising the
/// fused single type test against the generic evaluate-both-sides + IsLooselyEqual dispatch.
///
/// <para><b>Engine isolation.</b> Both rows get their own engine, built by <c>CreateEngine</c> (which
/// installs the shared <c>arr</c> fixture) and warmed with their own script and nothing else (see
/// <see cref="IsolatedScript"/>). It used to be one shared engine warmed with both scripts, so each row
/// was measured on an engine carrying the other's globals (both declare <c>f</c>) and its handler-tree
/// and call-site state. The rows still measure warm dispatch, and engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class NullCheckBenchmark
{
    private IsolatedScript _looseNotEqualNull;
    private IsolatedScript _looseEqualNull;

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

    /// <summary>Builds a fresh engine carrying the shared <c>arr</c> fixture every row needs.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(SetupSource);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _looseNotEqualNull = IsolatedScript.Warm("""
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
            """, CreateEngine);

        _looseEqualNull = IsolatedScript.Warm("""
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
            """, CreateEngine);
    }

    [Benchmark]
    public JsValue LooseNotEqualNull() => _looseNotEqualNull.Run();

    [Benchmark]
    public JsValue LooseEqualNull() => _looseEqualNull.Run();
}
