using System.Reflection;
using BenchmarkDotNet.Running;

namespace Jint.Benchmark
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<UncacheableExpressionsBenchmark>();
            //BenchmarkSwitcher.FromAssembly(typeof(Program).GetTypeInfo().Assembly).RunAll();
        }
    }
}