using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Exception-path costs that validation/parse-style code pays constantly: try scaffolding without
/// a throw (read against the LoopDispatch CounterAdd floor), throw+catch round-trips, Error
/// construction with and without touching <c>.stack</c>, and unwinding through a deep call chain.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all six scripts, so a row
/// was measured on an engine carrying the other five rows' globals (every script declares
/// <c>function f</c>, so they overwrite one another outright) and their handler-tree, call-site and
/// Error-intrinsic state — which makes a row's number depend on what a change did to its siblings. The
/// <c>reusedError</c> fixture is still shared, but now by being re-created per engine rather than by the
/// engine being re-used. The rows still measure warm exception handling, and engine construction and
/// warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not
/// comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ErrorHandlingBenchmark
{
    private IsolatedScript _tryNoThrow;
    private IsolatedScript _throwCatchLoop;
    private IsolatedScript _throwCatchReuseError;
    private IsolatedScript _errorConstructOnly;
    private IsolatedScript _errorStackAccess;
    private IsolatedScript _deepStackThrow;

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

    /// <summary>Builds a fresh engine carrying the <c>reusedError</c> fixture the unwind-only row throws.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute("var reusedError = new Error('r');");
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _tryNoThrow = IsolatedScript.Warm(Engine.PrepareScript(TryNoThrowSource), CreateEngine);
        _throwCatchLoop = IsolatedScript.Warm(Engine.PrepareScript(ThrowCatchLoopSource), CreateEngine);

        // unwind cost isolated from Error construction
        _throwCatchReuseError = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    try { throw reusedError; } catch (e) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        // construction only: no throw, no stack read
        _errorConstructOnly = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    var e = new Error('m' + (i & 15));
                    if (e) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        _errorStackAccess = IsolatedScript.Warm(Engine.PrepareScript(ErrorStackAccessSource), CreateEngine);

        // throw from 50 frames down, catch at the top
        _deepStackThrow = IsolatedScript.Warm(Engine.PrepareScript("""
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
            """), CreateEngine);
    }

    [Benchmark]
    public JsValue TryNoThrow() => _tryNoThrow.Run();

    [Benchmark]
    public JsValue ThrowCatchLoop() => _throwCatchLoop.Run();

    [Benchmark]
    public JsValue ThrowCatchReuseError() => _throwCatchReuseError.Run();

    [Benchmark]
    public JsValue ErrorConstructOnly() => _errorConstructOnly.Run();

    [Benchmark]
    public JsValue ErrorStackAccess() => _errorStackAccess.Run();

    [Benchmark]
    public JsValue DeepStackThrow() => _deepStackThrow.Run();
}
