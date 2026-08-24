using System.Reflection;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// CLR interop bench for the reflection path: a parse plus a member access, per row.
///
/// <para>It is deliberately <b>not</b> the [JsAccessible] comparison. Annotating Player here would change
/// what three of these rows measure while leaving the two method rows reflected — the generator declines an
/// int-parameter method — so the class would stop being a baseline for anything. The paired comparison lives
/// in <see cref="InteropGeneratedAccessorBenchmarks"/>, where the two model types differ only in the
/// attribute and every row has a sibling.</para>
///
/// The benchmark types live inside this class — same pattern as InteropBenchmark.Person.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with that row's own script and
/// nothing else. It used to be four engines for seven rows: the three property-get rows shared one
/// engine warmed with <c>p.Score; p.Name; p.Alive</c> and the two method rows shared one warmed with
/// <c>p.AddPoints(1); p.Describe()</c>, so each of those rows was measured on an engine whose
/// member-resolution and wrapper state had been established by its siblings. The rows still measure warm
/// dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement.
/// The per-scenario Player values are preserved exactly. <b>Numbers from this class are not comparable
/// to any published before the harness changed.</b></para>
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

    private Engine _engineGetInt = null!;
    private Engine _engineGetString = null!;
    private Engine _engineGetBool = null!;
    private Engine _engineSet = null!;
    private Engine _engineMethodOneArg = null!;
    private Engine _engineMethodNoArgs = null!;
    private Engine _engineMixed = null!;

    /// <summary>
    /// Builds a fresh engine exposing one Player as <c>p</c>. One Engine per row keeps property-access
    /// caches monomorphic on the same Player instance — eliminates accidental polymorphism noise across
    /// benchmarks — and stops a row inheriting the state a sibling row's warm-up established.
    /// </summary>
    private static Engine CreateEngine(Player player)
    {
        var engine = new Engine(cfg => cfg.AllowClr(typeof(Player).GetTypeInfo().Assembly));
        engine.SetValue("p", player);
        return engine;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engineGetInt = CreateEngine(new Player { Name = "alice", Score = 42, Alive = true });
        _engineGetString = CreateEngine(new Player { Name = "alice", Score = 42, Alive = true });
        _engineGetBool = CreateEngine(new Player { Name = "alice", Score = 42, Alive = true });
        _engineSet = CreateEngine(new Player { Name = "bob", Score = 0, Alive = true });
        _engineMethodOneArg = CreateEngine(new Player { Name = "carol", Score = 0, Alive = true });
        _engineMethodNoArgs = CreateEngine(new Player { Name = "carol", Score = 0, Alive = true });
        _engineMixed = CreateEngine(new Player { Name = "dave", Score = 0, Alive = true });

        // Warm the access caches — each engine with its own row's script and nothing else.
        _engineGetInt.Execute("p.Score");
        _engineGetString.Execute("p.Name");
        _engineGetBool.Execute("p.Alive");
        _engineSet.Execute("p.Score = 1");
        _engineMethodOneArg.Execute("p.AddPoints(1)");
        _engineMethodNoArgs.Execute("p.Describe()");
        _engineMixed.Execute("p.Name; p.Describe()");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertyGet_Int()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineGetInt.Execute("p.Score");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertyGet_String()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineGetString.Execute("p.Name");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertyGet_Bool()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineGetBool.Execute("p.Alive");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void PropertySet_Int()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineSet.Execute("p.Score = 1");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void MethodInvoke_OneArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineMethodOneArg.Execute("p.AddPoints(1)");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void MethodInvoke_NoArgs()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineMethodNoArgs.Execute("p.Describe()");
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
