using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Exception-path costs that validation/parse-style code pays constantly: try scaffolding without
/// a throw (read against the LoopDispatch CounterAdd floor), throw+catch round-trips, Error
/// construction with and without touching <c>.stack</c>, and unwinding through a deep call chain.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ErrorHandlingBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _tryNoThrow;
    private Prepared<Script> _throwCatchLoop;
    private Prepared<Script> _throwCatchReuseError;
    private Prepared<Script> _errorConstructOnly;
    private Prepared<Script> _errorStackAccess;
    private Prepared<Script> _deepStackThrow;

    internal const string TryNoThrowSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                try { s += 1; } catch (e) { }
            }
            return s;
        }
        f();
        """;

    internal const string ThrowCatchLoopSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 10000; i++) {
                try { throw new Error('x'); } catch (e) { s++; }
            }
            return s;
        }
        f();
        """;

    internal const string ErrorStackAccessSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 10000; i++) {
                var e = new Error('m');
                s += e.stack.length;
            }
            return s;
        }
        f();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute("var reusedError = new Error('r');");

        _tryNoThrow = Engine.PrepareScript(TryNoThrowSource);
        _throwCatchLoop = Engine.PrepareScript(ThrowCatchLoopSource);

        // unwind cost isolated from Error construction
        _throwCatchReuseError = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    try { throw reusedError; } catch (e) { s++; }
                }
                return s;
            }
            f();
            """);

        // construction only: no throw, no stack read
        _errorConstructOnly = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    var e = new Error('m' + (i & 15));
                    if (e) { s++; }
                }
                return s;
            }
            f();
            """);

        _errorStackAccess = Engine.PrepareScript(ErrorStackAccessSource);

        // throw from 50 frames down, catch at the top
        _deepStackThrow = Engine.PrepareScript("""
            function d(n) {
                if (n === 0) { throw new Error('deep'); }
                return d(n - 1);
            }
            function f() {
                var s = 0;
                for (var i = 0; i < 2000; i++) {
                    try { d(50); } catch (e) { s++; }
                }
                return s;
            }
            f();
            """);

        _engine.Evaluate(_tryNoThrow);
        _engine.Evaluate(_throwCatchLoop);
        _engine.Evaluate(_throwCatchReuseError);
        _engine.Evaluate(_errorConstructOnly);
        _engine.Evaluate(_errorStackAccess);
        _engine.Evaluate(_deepStackThrow);
    }

    [Benchmark]
    public JsValue TryNoThrow() => _engine.Evaluate(_tryNoThrow);

    [Benchmark]
    public JsValue ThrowCatchLoop() => _engine.Evaluate(_throwCatchLoop);

    [Benchmark]
    public JsValue ThrowCatchReuseError() => _engine.Evaluate(_throwCatchReuseError);

    [Benchmark]
    public JsValue ErrorConstructOnly() => _engine.Evaluate(_errorConstructOnly);

    [Benchmark]
    public JsValue ErrorStackAccess() => _engine.Evaluate(_errorStackAccess);

    [Benchmark]
    public JsValue DeepStackThrow() => _engine.Evaluate(_deepStackThrow);
}
