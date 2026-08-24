using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// What <c>[JsAccessible]</c> costs against the reflection path it replaces. Every row is paired: the two
/// model types carry byte-for-byte the same members and differ only in the attribute, so a
/// <c>Generated_*</c> row and its <c>Reflected_*</c> sibling measure the same script against the same shape.
/// <para>
/// Scripts are <b>prepared</b>, unlike <see cref="InteropAccessibleBenchmarks"/>'s. That class measures a
/// parse plus a member access and is a baseline for the reflection path as a whole; here the parse would be
/// most of the row and would compress exactly the difference the pair exists to show.
/// </para>
/// <para>
/// One engine per row, warmed with that row's own script and nothing else — the discipline
/// <see cref="InteropAccessibleBenchmarks"/> documents, and for the same reason: a row must not inherit the
/// member-resolution and wrapper state a sibling's warm-up established.
/// </para>
/// <para>
/// Note what the pair does <em>not</em> isolate on <c>net8.0</c> and above: the reflected side already has
/// the run-time compiled lanes (<c>CompiledMemberAccessor</c>), so the difference there is the cost of
/// building them and of the member resolution around them, not of reflection. On <c>net472</c>, and under
/// any runtime without dynamic code, those lanes decline outright and the reflected side is reflection —
/// which is where the pair says the most.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class InteropGeneratedAccessorBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    [JsAccessible]
    public sealed class GeneratedPlayer
    {
        public int Score { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Alive { get; set; }

        public JsValue AddPoints(JsValue delta)
        {
            Score += (int) delta.AsNumber();
            return Score;
        }

        public JsValue Describe() => Name + ":" + Score;
    }

    public sealed class ReflectedPlayer
    {
        public int Score { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Alive { get; set; }

        public JsValue AddPoints(JsValue delta)
        {
            Score += (int) delta.AsNumber();
            return Score;
        }

        public JsValue Describe() => Name + ":" + Score;
    }

    private readonly Dictionary<string, (Engine Engine, Prepared<Script> Script)> _rows = new(StringComparer.Ordinal);

    [GlobalSetup]
    public void GlobalSetup()
    {
        JsAccessibleRegistration.RegisterAll();

        Add("Generated_PropertyGet_Int", new GeneratedPlayer { Name = "alice", Score = 42, Alive = true }, "p.Score");
        Add("Reflected_PropertyGet_Int", new ReflectedPlayer { Name = "alice", Score = 42, Alive = true }, "p.Score");
        Add("Generated_PropertyGet_String", new GeneratedPlayer { Name = "alice", Score = 42, Alive = true }, "p.Name");
        Add("Reflected_PropertyGet_String", new ReflectedPlayer { Name = "alice", Score = 42, Alive = true }, "p.Name");
        Add("Generated_PropertySet_Int", new GeneratedPlayer { Name = "bob" }, "p.Score = 1");
        Add("Reflected_PropertySet_Int", new ReflectedPlayer { Name = "bob" }, "p.Score = 1");
        Add("Generated_MethodInvoke_OneArg", new GeneratedPlayer { Name = "carol" }, "p.AddPoints(1)");
        Add("Reflected_MethodInvoke_OneArg", new ReflectedPlayer { Name = "carol" }, "p.AddPoints(1)");
        Add("Generated_MethodInvoke_NoArgs", new GeneratedPlayer { Name = "carol" }, "p.Describe()");
        Add("Reflected_MethodInvoke_NoArgs", new ReflectedPlayer { Name = "carol" }, "p.Describe()");
    }

    private void Add(string row, object player, string source)
    {
        var engine = new Engine();
        engine.SetValue("p", player);
        var script = Engine.PrepareScript(source);
        engine.Execute(script);
        _rows[row] = (engine, script);
    }

    private void Run(string row)
    {
        var (engine, script) = _rows[row];
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            engine.Execute(script);
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Generated_PropertyGet_Int() => Run(nameof(Generated_PropertyGet_Int));

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Reflected_PropertyGet_Int() => Run(nameof(Reflected_PropertyGet_Int));

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Generated_PropertyGet_String() => Run(nameof(Generated_PropertyGet_String));

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Reflected_PropertyGet_String() => Run(nameof(Reflected_PropertyGet_String));

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Generated_PropertySet_Int() => Run(nameof(Generated_PropertySet_Int));

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Reflected_PropertySet_Int() => Run(nameof(Reflected_PropertySet_Int));

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Generated_MethodInvoke_OneArg() => Run(nameof(Generated_MethodInvoke_OneArg));

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Reflected_MethodInvoke_OneArg() => Run(nameof(Reflected_MethodInvoke_OneArg));

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Generated_MethodInvoke_NoArgs() => Run(nameof(Generated_MethodInvoke_NoArgs));

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Reflected_MethodInvoke_NoArgs() => Run(nameof(Reflected_MethodInvoke_NoArgs));
}
