#nullable enable
using BenchmarkDotNet.Attributes;
using Jint.Runtime.Interop;

namespace Jint.Benchmark;

/// <summary>
/// CLR constructor invocation from JS via TypeReference — the MethodDescriptor.Call path
/// (TypeReference.Construct → InteropHelper.FindBestMatch → MethodDescriptor.Call), which has
/// its own per-call object[] parameter array and reflective ConstructorInfo.Invoke.
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

    private Engine _engine = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine(cfg => cfg.AllowClr(typeof(RefTarget).Assembly));
        _engine.SetValue("RefTarget", TypeReference.CreateTypeReference<RefTarget>(_engine));
        _engine.SetValue("ValueTarget", TypeReference.CreateTypeReference<ValueTarget>(_engine));

        // Warm constructor caches.
        _engine.Execute("new RefTarget(); new RefTarget(1, 'x'); new ValueTarget(1)");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Construct_NoArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engine.Execute("new RefTarget()");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Construct_TwoArgs()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engine.Execute("new RefTarget(1, 'x')");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Construct_ValueType_OneArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engine.Execute("new ValueTarget(1)");
    }
}
