using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Per-call cost of invoking a host delegate registered through <c>Engine.SetValue(string, Delegate)</c>.
/// Each row runs one call site in a tight loop inside a single prepared script, so the site warms on its
/// first dispatch and every later call takes the wrapper's arity-specialized lane — the lane that binds
/// its arguments from two registers instead of a <c>JsCallArguments</c> array.
/// <para>
/// The rows are chosen so the argument-binding shapes are separable. <c>Arity1_Int</c> and
/// <c>Arity2_IntString</c> box a value-typed argument, <c>Arity1_String</c> and <c>Arity1_JsValue</c> do
/// not (a <see cref="JsValue"/> parameter is handed its argument straight through), and
/// <c>Arity1_Nullable</c> exercises the boxed-<see cref="Nullable{T}"/> representation. <c>Arity0</c> and
/// <c>Arity3_Ints</c> are controls: the former binds nothing, the latter exceeds the two argument
/// registers and therefore stays on the generic <c>Call</c> path for the whole run.
/// </para>
/// <para>
/// <c>[MemoryDiagnoser]</c> is the point of the lane rows — the per-call argument array is directly
/// visible in <c>Allocated</c>, while the boxes of the value-typed rows are not removable (the invocation
/// contract is <c>object?</c>) and should stay put.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class HostDelegateCallBenchmark
{
    private const int OperationsPerInvoke = 1_000;

    private const string Loop = "var s = 0; for (var i = 0; i < 1000; i++) { s = ";

    private Engine _engine = null!;

    private Prepared<Script> _arity0;
    private Prepared<Script> _arity1Int;
    private Prepared<Script> _arity1String;
    private Prepared<Script> _arity1JsValue;
    private Prepared<Script> _arity1Nullable;
    private Prepared<Script> _arity2IntString;
    private Prepared<Script> _arity3Ints;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine();
        _engine.SetValue("zero", new Func<int>(() => 1));
        _engine.SetValue("oneInt", new Func<int, int>(x => x + 1));
        _engine.SetValue("oneString", new Func<string, int>(x => x.Length));
        _engine.SetValue("oneJsValue", new Func<JsValue, int>(_ => 1));
        _engine.SetValue("oneNullable", new Func<int?, int>(x => x ?? 0));
        _engine.SetValue("twoIntString", new Func<int, string, int>((x, y) => x + y.Length));
        _engine.SetValue("threeInts", new Func<int, int, int, int>((x, y, z) => x + y + z));

        _arity0 = Engine.PrepareScript(Loop + "zero(); } s");
        _arity1Int = Engine.PrepareScript(Loop + "oneInt(i); } s");
        _arity1String = Engine.PrepareScript(Loop + "oneString('abc'); } s");
        _arity1JsValue = Engine.PrepareScript(Loop + "oneJsValue(i); } s");
        _arity1Nullable = Engine.PrepareScript(Loop + "oneNullable(i); } s");
        _arity2IntString = Engine.PrepareScript(Loop + "twoIntString(i, 'abc'); } s");
        _arity3Ints = Engine.PrepareScript(Loop + "threeInts(i, 1, 2); } s");

        // populate the handler-tree caches so the measured runs are steady-state
        _engine.Evaluate(_arity0);
        _engine.Evaluate(_arity1Int);
        _engine.Evaluate(_arity1String);
        _engine.Evaluate(_arity1JsValue);
        _engine.Evaluate(_arity1Nullable);
        _engine.Evaluate(_arity2IntString);
        _engine.Evaluate(_arity3Ints);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity0() => _engine.Evaluate(_arity0);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity1_Int() => _engine.Evaluate(_arity1Int);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity1_String() => _engine.Evaluate(_arity1String);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity1_JsValue() => _engine.Evaluate(_arity1JsValue);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity1_Nullable() => _engine.Evaluate(_arity1Nullable);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity2_IntString() => _engine.Evaluate(_arity2IntString);

    /// <summary>Control: three parameters exceed the two argument registers, so this row never leaves the generic path.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity3_Ints() => _engine.Evaluate(_arity3Ints);
}
