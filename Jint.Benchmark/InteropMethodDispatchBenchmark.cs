using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// CLR interop method-dispatch micro-benchmarks. These isolate the per-call cost of
/// MethodInfoFunction.Call: FindBestMatch iterator + overload scoring, the per-call
/// object[] parameter array, argument coercion and the reflective MethodBase.Invoke.
///
/// Warm engines (caches populated in GlobalSetup) so the numbers reflect the steady-state
/// dispatch cost, not first-touch type resolution (see InteropColdTypeBenchmark for that).
/// </summary>
[MemoryDiagnoser]
public class InteropMethodDispatchBenchmark
{
    private const int OperationsPerInvoke = 1_000;

    public sealed class Target
    {
        private int _counter;

        // single-overload methods (the dominant real-world shape)
        public int Ping() => _counter;
        public int AddPoints(int delta) { _counter += delta; return _counter; }
        public double SetRatio(double ratio) => ratio;
        public string SetName(string name) => name;
        public string Combine(int count, string name) => name;

        // overload group with mixed argument types
        public string Do(int x) => "int";
        public string Do(string x) => "string";
        public string Do(double x, double y) => "double,double";
        public string Do(object x) => "object";

        // params method
        public int Sum(params int[] values)
        {
            var sum = 0;
            foreach (var value in values)
            {
                sum += value;
            }

            return sum;
        }
    }

    private Engine _engineNoArg = null!;
    private Engine _engineOneInt = null!;
    private Engine _engineOneDouble = null!;
    private Engine _engineOneString = null!;
    private Engine _engineTwoArgs = null!;
    private Engine _engineOverloaded = null!;
    private Engine _engineParams = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // One engine per scenario keeps member-access caches monomorphic per benchmark.
        static Engine CreateEngine()
        {
            var engine = new Engine();
            engine.SetValue("t", new Target());
            return engine;
        }

        _engineNoArg = CreateEngine();
        _engineOneInt = CreateEngine();
        _engineOneDouble = CreateEngine();
        _engineOneString = CreateEngine();
        _engineTwoArgs = CreateEngine();
        _engineOverloaded = CreateEngine();
        _engineParams = CreateEngine();

        // Warm the dispatch caches.
        _engineNoArg.Execute("t.Ping()");
        _engineOneInt.Execute("t.AddPoints(1)");
        _engineOneDouble.Execute("t.SetRatio(1.5)");
        _engineOneString.Execute("t.SetName('x')");
        _engineTwoArgs.Execute("t.Combine(1, 'x')");
        _engineOverloaded.Execute("t.Do(1); t.Do('x'); t.Do(1.5, 2.5); t.Do({})");
        _engineParams.Execute("t.Sum(1, 2, 3)");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SingleOverload_NoArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineNoArg.Execute("t.Ping()");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SingleOverload_OneIntArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineOneInt.Execute("t.AddPoints(1)");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SingleOverload_OneDoubleArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineOneDouble.Execute("t.SetRatio(1.5)");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SingleOverload_OneStringArg()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineOneString.Execute("t.SetName('x')");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SingleOverload_TwoArgs_IntString()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineTwoArgs.Execute("t.Combine(1, 'x')");
    }

    /// <summary>
    /// Calls into a 4-method overload group with alternating argument types; guards that
    /// single-overload fast paths don't change multi-overload resolution behavior or speed.
    /// </summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Overloaded_MixedArgs()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineOverloaded.Execute("t.Do(1); t.Do('x'); t.Do(1.5, 2.5)");
    }

    /// <summary>Guards the HasParams / ProcessParamsArrays path.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void ParamsMethod_Spread()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineParams.Execute("t.Sum(1, 2, 3)");
    }
}
