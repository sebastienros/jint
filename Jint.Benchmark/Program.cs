using System.Reflection;
using BenchmarkDotNet.Running;
using Jint.Benchmark;

BenchmarkSwitcher
    .FromAssembly(typeof(ArrayBenchmark).GetTypeInfo().Assembly)
    .Run(args);
