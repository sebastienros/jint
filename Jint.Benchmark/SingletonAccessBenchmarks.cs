using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Property-read bench against built-in singletons. Gates [JsObject(Frozen=true)] (Phase 2g of the
/// source-gen plan), which would let the inline cache in JintMemberExpression skip one of the two
/// fast-path checks (`baseObject._propertiesVersion == _cachedReadVersion`) for hosts whose shape
/// is stable post-Initialize. Eligible: Math, JSON, Reflect, Atomics, Symbol — singletons whose
/// properties are virtually never mutated in user code. NOT eligible: Array.prototype,
/// Object.prototype, String.prototype (libraries do extend those).
///
/// Each scenario reads the same property in a tight loop so the inline cache's hit path dominates.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all five row
/// scripts, so each row was measured on an engine carrying the other four rows' globals (four of the
/// five declare <c>s</c> and <c>i</c>, so they collide outright), their handler-tree entries and their
/// per-call-site inline-cache state — which is exactly the state these rows exist to measure, so a
/// row's number depended on which siblings existed and on what a change did to <em>them</em>. The rows
/// still measure warm reads, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside
/// the measurement. <b>Numbers from this class are not comparable to any published before the harness
/// changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class SingletonAccessBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    private IsolatedScript _mathPi;
    private IsolatedScript _mathPiTightLoop;
    private IsolatedScript _jsonStringifyTightLoop;
    private IsolatedScript _reflectHasTightLoop;
    private IsolatedScript _symbolIteratorTightLoop;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _mathPi = IsolatedScript.Warm("Math.PI");

        _mathPiTightLoop         = IsolatedScript.Warm("var s = 0; for (var i = 0; i < 1000; i++) s += Math.PI; s");
        _jsonStringifyTightLoop  = IsolatedScript.Warm("var s = ''; for (var i = 0; i < 1000; i++) s = JSON.stringify(i); s");
        _reflectHasTightLoop     = IsolatedScript.Warm("var o = {a:1}; var s = 0; for (var i = 0; i < 1000; i++) if (Reflect.has(o, 'a')) s++; s");
        _symbolIteratorTightLoop = IsolatedScript.Warm("var s = 0; for (var i = 0; i < 1000; i++) if (Symbol.iterator) s++; s");
    }

    [Benchmark] public JsValue Warm_MathPi() => _mathPi.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_MathPi_TightLoop() => _mathPiTightLoop.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_JsonStringify_TightLoop() => _jsonStringifyTightLoop.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ReflectHas_TightLoop() => _reflectHasTightLoop.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_SymbolIterator_TightLoop() => _symbolIteratorTightLoop.Run();
}
