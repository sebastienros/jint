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
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class SingletonAccessBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    private Engine _warm = null!;
    private Prepared<Script> _mathPi;
    private Prepared<Script> _mathPiTightLoop;
    private Prepared<Script> _jsonStringifyTightLoop;
    private Prepared<Script> _reflectHasTightLoop;
    private Prepared<Script> _symbolIteratorTightLoop;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _mathPi = Engine.PrepareScript("Math.PI");

        _mathPiTightLoop         = Engine.PrepareScript("var s = 0; for (var i = 0; i < 1000; i++) s += Math.PI; s");
        _jsonStringifyTightLoop  = Engine.PrepareScript("var s = ''; for (var i = 0; i < 1000; i++) s = JSON.stringify(i); s");
        _reflectHasTightLoop     = Engine.PrepareScript("var o = {a:1}; var s = 0; for (var i = 0; i < 1000; i++) if (Reflect.has(o, 'a')) s++; s");
        _symbolIteratorTightLoop = Engine.PrepareScript("var s = 0; for (var i = 0; i < 1000; i++) if (Symbol.iterator) s++; s");

        _warm = new Engine();
        _warm.Evaluate(_mathPi);
        _warm.Evaluate(_mathPiTightLoop);
        _warm.Evaluate(_jsonStringifyTightLoop);
        _warm.Evaluate(_reflectHasTightLoop);
        _warm.Evaluate(_symbolIteratorTightLoop);
    }

    [Benchmark] public JsValue Warm_MathPi() => _warm.Evaluate(_mathPi);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_MathPi_TightLoop() => _warm.Evaluate(_mathPiTightLoop);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_JsonStringify_TightLoop() => _warm.Evaluate(_jsonStringifyTightLoop);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ReflectHas_TightLoop() => _warm.Evaluate(_reflectHasTightLoop);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_SymbolIterator_TightLoop() => _warm.Evaluate(_symbolIteratorTightLoop);
}
