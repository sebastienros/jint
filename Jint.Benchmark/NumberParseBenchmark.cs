using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Number parsing and formatting in loops — the CSV/JSON-ingestion and UI-formatting shapes:
/// parseInt/parseFloat/Number() over prebuilt LCG-varied numeric strings, and
/// toFixed/toString(radix) over varied doubles. 100k operations per op.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class NumberParseBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _parseIntLoop;
    private Prepared<Script> _parseFloatLoop;
    private Prepared<Script> _numberCoerce;
    private Prepared<Script> _toFixedLoop;
    private Prepared<Script> _toStringRadix;

    internal const string SetupSource = """
        var intStrs = [];
        var fltStrs = [];
        var nums = [];
        (function () {
            var seed = 20260711;
            for (var i = 0; i < 1024; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                intStrs.push('' + ((seed >>> 4) & 1048575));
                fltStrs.push(((seed >>> 4) & 65535) + '.' + ((seed >>> 8) & 99));
                nums.push(((seed >>> 4) & 65535) + ((seed >>> 8) & 99) / 100);
            }
        })();
        """;

    internal const string ParseIntLoopSource = """
        function f() {
            var seed = 20260711;
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                s += parseInt(intStrs[(seed >>> 4) & 1023], 10);
            }
            return s;
        }
        f();
        """;

    internal const string ToFixedLoopSource = """
        function f() {
            var seed = 20260711;
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                s += nums[(seed >>> 4) & 1023].toFixed(2).length;
            }
            return s;
        }
        f();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        _parseIntLoop = Engine.PrepareScript(ParseIntLoopSource);

        _parseFloatLoop = Engine.PrepareScript("""
            function f() {
                var seed = 20260711;
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    seed = (seed * 1664525 + 1013904223) | 0;
                    s += parseFloat(fltStrs[(seed >>> 4) & 1023]);
                }
                return s;
            }
            f();
            """);

        _numberCoerce = Engine.PrepareScript("""
            function f() {
                var seed = 20260711;
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    seed = (seed * 1664525 + 1013904223) | 0;
                    s += Number(intStrs[(seed >>> 4) & 1023]);
                }
                return s;
            }
            f();
            """);

        _toFixedLoop = Engine.PrepareScript(ToFixedLoopSource);

        _toStringRadix = Engine.PrepareScript("""
            function f() {
                var seed = 20260711;
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    seed = (seed * 1664525 + 1013904223) | 0;
                    s += (seed >>> 8).toString(16).length;
                }
                return s;
            }
            f();
            """);

        _engine.Evaluate(_parseIntLoop);
        _engine.Evaluate(_parseFloatLoop);
        _engine.Evaluate(_numberCoerce);
        _engine.Evaluate(_toFixedLoop);
        _engine.Evaluate(_toStringRadix);
    }

    [Benchmark]
    public JsValue ParseIntLoop() => _engine.Evaluate(_parseIntLoop);

    [Benchmark]
    public JsValue ParseFloatLoop() => _engine.Evaluate(_parseFloatLoop);

    [Benchmark]
    public JsValue NumberCoerce() => _engine.Evaluate(_numberCoerce);

    [Benchmark]
    public JsValue ToFixedLoop() => _engine.Evaluate(_toFixedLoop);

    [Benchmark]
    public JsValue ToStringRadix() => _engine.Evaluate(_toStringRadix);
}
