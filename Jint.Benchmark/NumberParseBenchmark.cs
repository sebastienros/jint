using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Number parsing and formatting in loops — the CSV/JSON-ingestion and UI-formatting shapes:
/// parseInt/parseFloat/Number() over prebuilt LCG-varied numeric strings, and
/// toFixed/toString(radix) over varied doubles. 100k operations per op.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, built by <c>CreateEngine</c> (which
/// installs the shared <see cref="SetupSource"/> fixture every row needs) and warmed with its own script
/// and nothing else (see <see cref="IsolatedScript"/>). It used to be one shared engine warmed with all
/// five scripts, so each row was measured on an engine carrying its siblings' globals (every script
/// declares <c>f</c>, <c>s</c> and <c>i</c>) and their handler-tree and call-site state. The rows still
/// measure warm dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the
/// measurement. <b>Numbers from this class are not comparable to any published before the harness
/// changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class NumberParseBenchmark
{
    private IsolatedScript _parseIntLoop;
    private IsolatedScript _parseFloatLoop;
    private IsolatedScript _numberCoerce;
    private IsolatedScript _toFixedLoop;
    private IsolatedScript _toStringRadix;

    // Mixing is precomputed at setup into `order` (see ModernOperatorsBenchmark note): a
    // per-iteration JS LCG boxes JsNumber transients that would dominate these rows.
    internal const string SetupSource = """
        var intStrs = [];
        var fltStrs = [];
        var nums = [];
        var radixNums = [];
        var order = [];
        (function () {
            var seed = 20260711;
            for (var i = 0; i < 1024; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                intStrs.push('' + ((seed >>> 4) & 1048575));
                fltStrs.push(((seed >>> 4) & 65535) + '.' + ((seed >>> 8) & 99));
                nums.push(((seed >>> 4) & 65535) + ((seed >>> 8) & 99) / 100);
                radixNums.push((seed >>> 8) & 1048575);
            }
            for (var i = 0; i < 8192; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                order.push((seed >>> 7) & 1023);
            }
        })();
        """;

    internal const string ParseIntLoopSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                s += parseInt(intStrs[order[i & 8191]], 10);
            }
            return s;
        }
        f();
        """;

    internal const string ToFixedLoopSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                s += nums[order[i & 8191]].toFixed(2).length;
            }
            return s;
        }
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
        _parseIntLoop = IsolatedScript.Warm(ParseIntLoopSource, CreateEngine);

        _parseFloatLoop = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += parseFloat(fltStrs[order[i & 8191]]);
                }
                return s;
            }
            f();
            """, CreateEngine);

        _numberCoerce = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += Number(intStrs[order[i & 8191]]);
                }
                return s;
            }
            f();
            """, CreateEngine);

        _toFixedLoop = IsolatedScript.Warm(ToFixedLoopSource, CreateEngine);

        _toStringRadix = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += radixNums[order[i & 8191]].toString(16).length;
                }
                return s;
            }
            f();
            """, CreateEngine);
    }

    [Benchmark]
    public JsValue ParseIntLoop() => _parseIntLoop.Run();

    [Benchmark]
    public JsValue ParseFloatLoop() => _parseFloatLoop.Run();

    [Benchmark]
    public JsValue NumberCoerce() => _numberCoerce.Run();

    [Benchmark]
    public JsValue ToFixedLoop() => _toFixedLoop.Run();

    [Benchmark]
    public JsValue ToStringRadix() => _toStringRadix.Run();
}
