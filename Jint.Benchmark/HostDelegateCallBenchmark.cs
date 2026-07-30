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
/// <para>
/// <b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which registers
/// the identical seven-delegate host surface every row saw before — and warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all seven
/// scripts, so each row was measured on an engine carrying the other six rows' globals (every script
/// declares <c>s</c> and <c>i</c>, so they collided outright) plus their handler-tree, call-site and
/// delegate-wrapper state — which for a class about per-call-site argument-binding lanes is exactly the
/// state a row must own. The rows still measure the warm lane, and engine construction and warm-up stay
/// in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b>
/// </para>
/// </summary>
[MemoryDiagnoser]
public class HostDelegateCallBenchmark
{
    private const int OperationsPerInvoke = 1_000;

    private const string Loop = "var s = 0; for (var i = 0; i < 1000; i++) { s = ";

    private IsolatedScript _arity0;
    private IsolatedScript _arity1Int;
    private IsolatedScript _arity1String;
    private IsolatedScript _arity1JsValue;
    private IsolatedScript _arity1Nullable;
    private IsolatedScript _arity2IntString;
    private IsolatedScript _arity3Ints;

    /// <summary>
    /// Builds a fresh engine carrying the host surface every row needs, and nothing else. All seven
    /// delegates are registered on every engine, exactly as the one shared engine carried them, so a row
    /// still resolves its callee out of a global object of the same size and shape.
    /// </summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.SetValue("zero", new Func<int>(() => 1));
        engine.SetValue("oneInt", new Func<int, int>(x => x + 1));
        engine.SetValue("oneString", new Func<string, int>(x => x.Length));
        engine.SetValue("oneJsValue", new Func<JsValue, int>(_ => 1));
        engine.SetValue("oneNullable", new Func<int?, int>(x => x ?? 0));
        engine.SetValue("twoIntString", new Func<int, string, int>((x, y) => x + y.Length));
        engine.SetValue("threeInts", new Func<int, int, int, int>((x, y, z) => x + y + z));
        return engine;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // each row's engine is warmed with that row's script only, so the handler-tree caches the
        // measured runs read are its own and the runs are still steady-state
        _arity0 = IsolatedScript.Warm(Engine.PrepareScript(Loop + "zero(); } s"), CreateEngine);
        _arity1Int = IsolatedScript.Warm(Engine.PrepareScript(Loop + "oneInt(i); } s"), CreateEngine);
        _arity1String = IsolatedScript.Warm(Engine.PrepareScript(Loop + "oneString('abc'); } s"), CreateEngine);
        _arity1JsValue = IsolatedScript.Warm(Engine.PrepareScript(Loop + "oneJsValue(i); } s"), CreateEngine);
        _arity1Nullable = IsolatedScript.Warm(Engine.PrepareScript(Loop + "oneNullable(i); } s"), CreateEngine);
        _arity2IntString = IsolatedScript.Warm(Engine.PrepareScript(Loop + "twoIntString(i, 'abc'); } s"), CreateEngine);
        _arity3Ints = IsolatedScript.Warm(Engine.PrepareScript(Loop + "threeInts(i, 1, 2); } s"), CreateEngine);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity0() => _arity0.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity1_Int() => _arity1Int.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity1_String() => _arity1String.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity1_JsValue() => _arity1JsValue.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity1_Nullable() => _arity1Nullable.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity2_IntString() => _arity2IntString.Run();

    /// <summary>Control: three parameters exceed the two argument registers, so this row never leaves the generic path.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Arity3_Ints() => _arity3Ints.Run();
}
