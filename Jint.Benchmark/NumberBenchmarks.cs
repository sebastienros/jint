using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Source-gen sentinel for Number.prototype methods. Pre-source-gen the prototype used handwritten
/// PropertyDictionary + ClrFunction registration; post-source-gen it's [JsObject] + [JsFunction] +
/// [ToInteger] for digit args. The warm-path numbers should be equivalent.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class NumberBenchmarks
{
    private Engine _warm = null!;
    private Prepared<Script> _toFixed;
    private Prepared<Script> _toString10;
    private Prepared<Script> _toString2;
    private Prepared<Script> _valueOf;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _toFixed = Engine.PrepareScript("(3.14159).toFixed(2)");
        _toString10 = Engine.PrepareScript("(255).toString()");
        _toString2 = Engine.PrepareScript("(255).toString(2)");
        _valueOf = Engine.PrepareScript("(42).valueOf()");

        _warm = new Engine();
        _warm.Evaluate(_toFixed);
        _warm.Evaluate(_toString10);
        _warm.Evaluate(_toString2);
        _warm.Evaluate(_valueOf);
    }

    // Cold paths exercise lazy NumberPrototype init + method dispatcher allocation.

    [Benchmark]
    public JsValue Cold_EngineThenToFixed()
    {
        var e = new Engine();
        return e.Evaluate(_toFixed);
    }

    [Benchmark]
    public JsValue Cold_EngineThenToStringRadix()
    {
        var e = new Engine();
        return e.Evaluate(_toString2);
    }

    // Warm paths exercise the inline cache hot path; should be equivalent before/after.

    [Benchmark]
    public JsValue Warm_ToFixed() => _warm.Evaluate(_toFixed);

    [Benchmark]
    public JsValue Warm_ToString10() => _warm.Evaluate(_toString10);

    [Benchmark]
    public JsValue Warm_ToString2() => _warm.Evaluate(_toString2);

    [Benchmark]
    public JsValue Warm_ValueOf() => _warm.Evaluate(_valueOf);
}
