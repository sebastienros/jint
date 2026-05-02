using System.Reflection;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// CLR interop bench. Gates [JsAccessible] (Phase 3 of the source-gen plan), which will replace
/// the reflection-based PropertyAccessor / FieldAccessor / MethodDescriptor paths with directly-
/// generated typed accessors and a pre-built TypeDescriptor seeded via [ModuleInitializer].
///
/// Pre-Phase-3 baseline cost (per the runtime survey in plans/we-have-created-source-wise-falcon.md):
///   - Property get/set: 2-5µs (PropertyInfo.GetValue/SetValue + boxing)
///   - Method invocation (1-arg): 5-10µs (MethodInfo.Invoke + arg boxing)
///   - Cold first-touch on a new CLR type: 50-200µs (TypeDescriptor reflection scan)
///
/// Phase 3 target: 30-80ns / 100-200ns / 0 respectively. This file establishes the baseline so
/// PR9-10 can demonstrate the win.
///
/// The benchmark types live inside this class — same pattern as InteropBenchmark.Person —
/// so when [JsAccessible] lands the same source files can grow `[JsAccessible]` on Player and
/// the bench runs both paths.
/// </summary>
[MemoryDiagnoser]
public class InteropAccessibleBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

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

        // Warm the access caches.
        _engineGet.Execute("p.Score; p.Name; p.Alive");
        _engineSet.Execute("p.Score = 1");
        _engineMethod.Execute("p.AddPoints(1); p.Describe()");
        _engineMixed.Execute("p.Score; p.AddPoints(1); p.Describe()");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertyGet_Int()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineGet.Execute("p.Score");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertyGet_String()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineGet.Execute("p.Name");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertyGet_Bool()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineGet.Execute("p.Alive");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertySet_Int()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineSet.Execute("p.Score = 1");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void MethodInvoke_OneArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineMethod.Execute("p.AddPoints(1)");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void MethodInvoke_NoArgs()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineMethod.Execute("p.Describe()");
    }

    /// <summary>Mixed get + method-invoke pattern — the realistic interop call site. The script
    /// reads p.Name (fast property get), then invokes p.Describe() (method call returning a string).
    /// Both touch the reflection-based dispatch paths but neither mutates state, so the bench is
    /// safe to run repeatedly without accumulating.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Mixed_GetMethodInvoke()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineMixed.Execute("p.Name; p.Describe()");
    }
}
