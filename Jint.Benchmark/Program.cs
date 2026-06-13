using System.Reflection;
using BenchmarkDotNet.Running;
using Jint.Benchmark;

if (args.Length > 0 && args[0] == "--profile-memory")
{
    return MemoryProbe.Run(args);
}

if (args.Length > 0 && args[0] == "--profile-cpu")
{
    return CpuProfileDriver.Run(args);
}

if (args.Length > 0 && args[0] == "--profile-alloc-types")
{
    return AllocTypeProbe.Run(args);
}

BenchmarkSwitcher
    .FromAssembly(typeof(ArrayBenchmark).GetTypeInfo().Assembly)
    .Run(args);

return 0;
