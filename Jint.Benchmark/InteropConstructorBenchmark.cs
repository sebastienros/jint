#nullable enable
using BenchmarkDotNet.Attributes;
using Jint.Runtime.Interop;

namespace Jint.Benchmark;

/// <summary>
/// CLR constructor invocation from JS via TypeReference — the MethodDescriptor.Call path
/// (TypeReference.Construct → InteropHelper.FindBestMatch → MethodDescriptor.Call), which has
/// its own per-call object[] parameter array and reflective ConstructorInfo.Invoke.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <see cref="CreateEngine"/>,
/// which installs both type references — warmed with that row's own script and nothing else. It used to
/// be one engine warmed with <c>new RefTarget(); new RefTarget(1, 'x'); new ValueTarget(1)</c>, so each
/// row was measured with all three rows' overload-resolution caches already resolved, and the
/// no-arg/two-arg rows in particular shared the same <c>RefTarget</c> constructor group. The rows still
/// measure warm construction, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside
/// the measurement.</para>
/// </summary>
[MemoryDiagnoser]
public class InteropConstructorBenchmark
{
    private const int OperationsPerInvoke = 1_000;

    public sealed class RefTarget
    {
        public RefTarget()
        {
        }

        public RefTarget(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string? Name { get; }
    }

    public readonly struct ValueTarget
    {
        public ValueTarget(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    private Engine _engineNoArg = null!;
    private Engine _engineTwoArgs = null!;
    private Engine _engineValueType = null!;

    /// <summary>Builds a fresh engine carrying the type references every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine(cfg => cfg.AllowClr(typeof(RefTarget).Assembly));
        engine.SetValue("RefTarget", TypeReference.CreateTypeReference<RefTarget>(engine));
        engine.SetValue("ValueTarget", TypeReference.CreateTypeReference<ValueTarget>(engine));
        return engine;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Warm constructor caches — each engine with its own row's script and nothing else.
        _engineNoArg = CreateEngine();
        _engineNoArg.Execute("new RefTarget()");

        _engineTwoArgs = CreateEngine();
        _engineTwoArgs.Execute("new RefTarget(1, 'x')");

        _engineValueType = CreateEngine();
        _engineValueType.Execute("new ValueTarget(1)");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Construct_NoArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineNoArg.Execute("new RefTarget()");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Construct_TwoArgs()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineTwoArgs.Execute("new RefTarget(1, 'x')");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Construct_ValueType_OneArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineValueType.Execute("new ValueTarget(1)");
    }
}
