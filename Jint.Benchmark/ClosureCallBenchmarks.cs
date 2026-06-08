using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the per-call cost of closure-style methods (the stopwatch.js shape):
/// EmptyClosureCall = 0-param/0-var closures where FunctionDeclarationInstantiation should be a no-op;
/// CapturedVarReadWrite = closures reading/writing captured variables (environment-chain resolution);
/// ParamLocalCall = guard for the existing fixed-slot fast-FDI path.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ClosureCallBenchmarks
{
    private Engine _engine = null!;
    private Prepared<Script> _emptyClosureCall;
    private Prepared<Script> _capturedVarReadWrite;
    private Prepared<Script> _paramLocalCall;

    [GlobalSetup]
    public void Setup()
    {
        _emptyClosureCall = Engine.PrepareScript("""
            (function() {
                function Box() {
                    this.nop = function () { };
                }
                var b = new Box();
                for (var i = 0; i < 100000; i++) {
                    b.nop();
                    b.nop();
                    b.nop();
                }
                return b;
            })();
            """, strict: true);

        _capturedVarReadWrite = Engine.PrepareScript("""
            (function() {
                function Box() {
                    var on = false;
                    var count = 0;
                    this.enable = function () { on = true; };
                    this.toggle = function () { on = !on; };
                    this.bump = function () { count = count + 1; return on; };
                }
                var b = new Box();
                for (var i = 0; i < 100000; i++) {
                    b.enable();
                    b.toggle();
                    b.bump();
                }
                return b;
            })();
            """, strict: true);

        _paramLocalCall = Engine.PrepareScript("""
            (function() {
                function add(a, b) {
                    var sum = a + b;
                    return sum;
                }
                var acc = 0;
                for (var i = 0; i < 100000; i++) {
                    acc = add(acc, 1);
                    acc = add(acc, 2);
                    acc = add(acc, 3);
                }
                return acc;
            })();
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        _engine.Evaluate(_emptyClosureCall);
        _engine.Evaluate(_capturedVarReadWrite);
        _engine.Evaluate(_paramLocalCall);
    }

    [Benchmark]
    public JsValue EmptyClosureCall() => _engine.Evaluate(_emptyClosureCall);

    [Benchmark]
    public JsValue CapturedVarReadWrite() => _engine.Evaluate(_capturedVarReadWrite);

    [Benchmark]
    public JsValue ParamLocalCall() => _engine.Evaluate(_paramLocalCall);
}
