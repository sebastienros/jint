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
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all five scripts, so
/// each row was measured on an engine carrying the other four rows' globals (<c>a</c> is declared by two
/// of them with different values, and <c>s</c>/<c>i</c> by three) plus their handler-tree and iterator
/// call-site state — which, for a class about the receiver type an internal <c>next()</c> call site
/// sees, is exactly the state a row must own. The rows still measure the warm path, and engine
/// construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this
/// class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class InternalCallBenchmarks
{
    private const int OperationsPerInvoke = 100;

    private IsolatedScript _forOfArray;
    private IsolatedScript _forOfMap;
    private IsolatedScript _forOfSet;
    private IsolatedScript _spreadArray;
    private IsolatedScript _arrayFromIterable;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _forOfArray = IsolatedScript.Warm("var a = [1,2,3,4,5,6,7,8,9,10]; var s = 0; for (var i = 0; i < 100; i++) for (var x of a) s += x; s");
        _forOfMap   = IsolatedScript.Warm("var m = new Map([['a',1],['b',2],['c',3],['d',4],['e',5]]); var s = 0; for (var i = 0; i < 100; i++) for (var [k,v] of m) s += v; s");
        _forOfSet   = IsolatedScript.Warm("var z = new Set([1,2,3,4,5,6,7,8,9,10]); var s = 0; for (var i = 0; i < 100; i++) for (var x of z) s += x; s");
        _spreadArray      = IsolatedScript.Warm("var a = [1,2,3,4,5,6,7,8,9,10]; var b = [...a]; b.length");
        _arrayFromIterable = IsolatedScript.Warm("var a = new Set([1,2,3,4,5,6,7,8,9,10]); Array.from(a).length");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ForOf_Array() => _forOfArray.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ForOf_Map() => _forOfMap.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ForOf_Set() => _forOfSet.Run();

    [Benchmark] public JsValue Warm_SpreadArray() => _spreadArray.Run();
    [Benchmark] public JsValue Warm_ArrayFromIterable() => _arrayFromIterable.Run();
}
