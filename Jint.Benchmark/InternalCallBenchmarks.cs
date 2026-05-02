using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Bench for code paths where the runtime calls a built-in method internally with a statically-known
/// receiver type — for-of/spread/destructuring all hit iterator.next() this way. Gates [Concrete]
/// (Phase 2a of the source-gen plan), which lets the generator emit an unchecked cast in the
/// dispatcher (eliding the `as Type + null check + TypeError` precondition the spec demands for
/// user-callable methods).
///
/// for-of over a JsArray invokes ArrayIteratorPrototype.next() on every iteration; that's a tight
/// loop where the precondition cost is measurable. Map/Set iteration uses the same pattern.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class InternalCallBenchmarks
{
    private const int OperationsPerInvoke = 100;

    private Engine _warm = null!;
    private Prepared<Script> _forOfArray;
    private Prepared<Script> _forOfMap;
    private Prepared<Script> _forOfSet;
    private Prepared<Script> _spreadArray;
    private Prepared<Script> _arrayFromIterable;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _forOfArray = Engine.PrepareScript("var a = [1,2,3,4,5,6,7,8,9,10]; var s = 0; for (var i = 0; i < 100; i++) for (var x of a) s += x; s");
        _forOfMap   = Engine.PrepareScript("var m = new Map([['a',1],['b',2],['c',3],['d',4],['e',5]]); var s = 0; for (var i = 0; i < 100; i++) for (var [k,v] of m) s += v; s");
        _forOfSet   = Engine.PrepareScript("var z = new Set([1,2,3,4,5,6,7,8,9,10]); var s = 0; for (var i = 0; i < 100; i++) for (var x of z) s += x; s");
        _spreadArray      = Engine.PrepareScript("var a = [1,2,3,4,5,6,7,8,9,10]; var b = [...a]; b.length");
        _arrayFromIterable = Engine.PrepareScript("var a = new Set([1,2,3,4,5,6,7,8,9,10]); Array.from(a).length");

        _warm = new Engine();
        _warm.Evaluate(_forOfArray);
        _warm.Evaluate(_forOfMap);
        _warm.Evaluate(_forOfSet);
        _warm.Evaluate(_spreadArray);
        _warm.Evaluate(_arrayFromIterable);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ForOf_Array() => _warm.Evaluate(_forOfArray);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ForOf_Map() => _warm.Evaluate(_forOfMap);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ForOf_Set() => _warm.Evaluate(_forOfSet);

    [Benchmark] public JsValue Warm_SpreadArray() => _warm.Evaluate(_spreadArray);
    [Benchmark] public JsValue Warm_ArrayFromIterable() => _warm.Evaluate(_arrayFromIterable);
}
