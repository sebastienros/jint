using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the per-call cost of closure-style methods (the stopwatch.js shape):
/// EmptyClosureCall = 0-param/0-var closures where FunctionDeclarationInstantiation should be a no-op;
/// CapturedVarReadWrite = closures reading/writing captured variables (environment-chain resolution);
/// ParamLocalCall = guard for the existing fixed-slot fast-FDI path;
/// ManyLocalCall = a function with many locals (2 params + 18 vars = 20 bindings, the DrawLine shape from
/// 3d-cube) that stresses per-call environment setup — exercises whichever FunctionDeclarationInstantiation
/// path the fixed-slot cap routes that binding count to.
/// The Sloppy rows run the method-call shapes non-strict, where OrdinaryCallBindThis additionally
/// pays TypeConverter.ToObject on the receiver every call — a cost the strict rows never see.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be two shared engines — one strict, one sloppy —
/// each warmed with every one of its rows' scripts, so a row was measured on an engine carrying its
/// siblings' globals (all six scripts declare <c>i</c>, four declare <c>b</c>) and their handler-tree,
/// call-site and environment-reuse state; a change affecting one row could then move another. The
/// strict/sloppy split is preserved per row, the rows still measure warm dispatch, and engine
/// construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this
/// class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ClosureCallBenchmarks
{
    private IsolatedScript _emptyClosureCall;
    private IsolatedScript _capturedVarReadWrite;
    private IsolatedScript _paramLocalCall;
    private IsolatedScript _manyLocalCall;
    private IsolatedScript _sloppyEmptyClosureCall;
    private IsolatedScript _sloppyCapturedVarReadWrite;

    private static Engine CreateStrictEngine() => new(static options => options.Strict());

    private static Engine CreateSloppyEngine() => new();

    [GlobalSetup]
    public void Setup()
    {
        _emptyClosureCall = IsolatedScript.Warm(Engine.PrepareScript("""
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
            """, strict: true), CreateStrictEngine);

        _capturedVarReadWrite = IsolatedScript.Warm(Engine.PrepareScript("""
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
            """, strict: true), CreateStrictEngine);

        _paramLocalCall = IsolatedScript.Warm(Engine.PrepareScript("""
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
            """, strict: true), CreateStrictEngine);

        // 2 params + 18 var locals = 20 bindings. The body has no inner closures, so the environment is
        // pooled and reused across calls (mirrors 3d-cube's DrawLine). Whether per-call setup uses the
        // array-backed fixed-slot path or the dictionary path depends on the fixed-slot cap.
        _manyLocalCall = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                function compute(a, b) {
                    var v0 = a + b;
                    var v1 = v0 + 1;
                    var v2 = v1 + v0;
                    var v3 = v2 - 1;
                    var v4 = v3 + v1;
                    var v5 = v4 + 2;
                    var v6 = v5 + v2;
                    var v7 = v6 - 3;
                    var v8 = v7 + v3;
                    var v9 = v8 + 4;
                    var v10 = v9 + v4;
                    var v11 = v10 - 5;
                    var v12 = v11 + v5;
                    var v13 = v12 + 6;
                    var v14 = v13 + v6;
                    var v15 = v14 - 7;
                    var v16 = v15 + v7;
                    var v17 = v16 + b;
                    return v0 + v8 + v17;
                }
                var acc = 0;
                for (var i = 0; i < 100000; i++) {
                    acc = compute(acc, i) - acc;
                }
                return acc;
            })();
            """, strict: true), CreateStrictEngine);

        _sloppyEmptyClosureCall = IsolatedScript.Warm(Engine.PrepareScript("""
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
            """), CreateSloppyEngine);

        _sloppyCapturedVarReadWrite = IsolatedScript.Warm(Engine.PrepareScript("""
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
            """), CreateSloppyEngine);
    }

    [Benchmark]
    public JsValue EmptyClosureCall() => _emptyClosureCall.Run();

    [Benchmark]
    public JsValue CapturedVarReadWrite() => _capturedVarReadWrite.Run();

    [Benchmark]
    public JsValue ParamLocalCall() => _paramLocalCall.Run();

    [Benchmark]
    public JsValue ManyLocalCall() => _manyLocalCall.Run();

    [Benchmark]
    public JsValue SloppyEmptyClosureCall() => _sloppyEmptyClosureCall.Run();

    [Benchmark]
    public JsValue SloppyCapturedVarReadWrite() => _sloppyCapturedVarReadWrite.Run();
}
