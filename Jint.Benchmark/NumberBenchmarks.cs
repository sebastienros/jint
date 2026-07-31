using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Source-gen sentinel for Number.prototype methods. Pre-source-gen the prototype used handwritten
/// PropertyDictionary + ClrFunction registration; post-source-gen it's [JsObject] + [JsFunction] +
/// [ToInteger] for digit args. The warm-path numbers should be equivalent.
///
/// <para><b>Engine isolation.</b> Each Warm_ row gets its own engine, warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one shared <c>_warm</c> engine warmed
/// with all four scripts, so every warm row was measured on an engine already carrying the other rows'
/// handler-tree and call-site state. The Cold_ rows are unchanged — they build their engine inside the
/// benchmark method, which is what they are for. <b>Numbers from the warm rows are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class NumberBenchmarks
{
    // Shared by the Cold_ rows, which build their own engine per invocation.
    private Prepared<Script> _toFixed;
    private Prepared<Script> _toString2;

    private IsolatedScript _warmToFixed;
    private IsolatedScript _warmToString10;
    private IsolatedScript _warmToString2;
    private IsolatedScript _warmValueOf;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _toFixed = Engine.PrepareScript("(3.14159).toFixed(2)");
        _toString2 = Engine.PrepareScript("(255).toString(2)");

        _warmToFixed = IsolatedScript.Warm(_toFixed);
        _warmToString10 = IsolatedScript.Warm("(255).toString()");
        _warmToString2 = IsolatedScript.Warm(_toString2);
        _warmValueOf = IsolatedScript.Warm("(42).valueOf()");
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
    public JsValue Warm_ToFixed() => _warmToFixed.Run();

    [Benchmark]
    public JsValue Warm_ToString10() => _warmToString10.Run();

    [Benchmark]
    public JsValue Warm_ToString2() => _warmToString2.Run();

    [Benchmark]
    public JsValue Warm_ValueOf() => _warmValueOf.Run();
}
