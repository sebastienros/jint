using System.Reflection;
using BenchmarkDotNet.Attributes;
using Jint;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// CLR interop bench. Gates [JsAccessible] (Phase 3 of the source-gen plan), which replaces the
/// reflection-based PropertyAccessor / FieldAccessor / MethodDescriptor paths with directly-
/// generated typed accessors and registers them via [ModuleInitializer].
///
/// Scripts are pre-compiled with <see cref="Engine.PrepareScript"/> and executed via
/// <see cref="Engine.Evaluate(Acornima.Prepared{Acornima.Ast.Script})"/> — the parse cost paid
/// once at <c>GlobalSetup</c>. Per-iteration time isolates the property-access / method-invocation
/// hot path, which is exactly what [JsAccessible] targets.
/// </summary>
[MemoryDiagnoser]
public class InteropAccessibleBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    [JsAccessible]
    public sealed class Player
    {
        public int Score { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Alive { get; set; }

        public int AddPoints(int delta) { Score += delta; return Score; }
        public string Describe() => $"{Name}:{Score}";
    }

    private Engine _engineGet = null!;
    private Engine _engineSet = null!;
    private Engine _engineMethod = null!;
    private Engine _engineMixed = null!;

    private Prepared<Script> _scriptGetInt;
    private Prepared<Script> _scriptGetString;
    private Prepared<Script> _scriptGetBool;
    private Prepared<Script> _scriptSetInt;
    private Prepared<Script> _scriptInvokeOneArg;
    private Prepared<Script> _scriptInvokeNoArgs;
    private Prepared<Script> _scriptMixed;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // One Engine per scenario keeps property-access caches monomorphic on the same Player
        // instance — eliminates accidental polymorphism noise across benchmarks.
        _engineGet = new Engine(cfg => cfg.AllowClr(typeof(Player).GetTypeInfo().Assembly));
        _engineGet.SetValue("p", new Player { Name = "alice", Score = 42, Alive = true });

        _engineSet = new Engine(cfg => cfg.AllowClr(typeof(Player).GetTypeInfo().Assembly));
        _engineSet.SetValue("p", new Player { Name = "bob", Score = 0, Alive = true });

        _engineMethod = new Engine(cfg => cfg.AllowClr(typeof(Player).GetTypeInfo().Assembly));
        _engineMethod.SetValue("p", new Player { Name = "carol", Score = 0, Alive = true });

        _engineMixed = new Engine(cfg => cfg.AllowClr(typeof(Player).GetTypeInfo().Assembly));
        _engineMixed.SetValue("p", new Player { Name = "dave", Score = 0, Alive = true });

        _scriptGetInt = Engine.PrepareScript("p.Score");
        _scriptGetString = Engine.PrepareScript("p.Name");
        _scriptGetBool = Engine.PrepareScript("p.Alive");
        _scriptSetInt = Engine.PrepareScript("p.Score = 1");
        _scriptInvokeOneArg = Engine.PrepareScript("p.AddPoints(1)");
        _scriptInvokeNoArgs = Engine.PrepareScript("p.Describe()");
        _scriptMixed = Engine.PrepareScript("p.Name; p.Describe()");

        // Warm the access caches.
        _engineGet.Evaluate(_scriptGetInt);
        _engineGet.Evaluate(_scriptGetString);
        _engineGet.Evaluate(_scriptGetBool);
        _engineSet.Evaluate(_scriptSetInt);
        _engineMethod.Evaluate(_scriptInvokeOneArg);
        _engineMethod.Evaluate(_scriptInvokeNoArgs);
        _engineMixed.Evaluate(_scriptMixed);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertyGet_Int()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineGet.Evaluate(_scriptGetInt);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertyGet_String()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineGet.Evaluate(_scriptGetString);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertyGet_Bool()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineGet.Evaluate(_scriptGetBool);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertySet_Int()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineSet.Evaluate(_scriptSetInt);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void MethodInvoke_OneArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineMethod.Evaluate(_scriptInvokeOneArg);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void MethodInvoke_NoArgs()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineMethod.Evaluate(_scriptInvokeNoArgs);
    }

    /// <summary>Mixed get + method-invoke pattern — the realistic interop call site. The script
    /// reads p.Name (fast property get), then invokes p.Describe() (method call returning a string).
    /// Both touch the dispatch paths but neither mutates state, so the bench is safe to run
    /// repeatedly without accumulating.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Mixed_GetMethodInvoke()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineMixed.Evaluate(_scriptMixed);
    }
}
