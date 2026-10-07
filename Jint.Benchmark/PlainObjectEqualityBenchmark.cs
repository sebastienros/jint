using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Measures generic object/null equality with a strict-comparison control.
/// </summary>
/// <remarks>
/// <para>The variable right operand prevents literal-null fusion from hiding the HTMLDDA check.
/// The strict comparison cannot execute that check.</para>
/// <para>Each row owns an engine warmed only with its own script. Engine construction and warmup stay
/// outside measurement. Results include loop and operand dispatch, not an isolated bit-test cost.</para>
/// </remarks>
[MemoryDiagnoser]
public class PlainObjectEqualityBenchmark
{
    private IsolatedScript _loose;
    private IsolatedScript _strict;

    private static IsolatedScript Create(string comparison)
    {
        var workload = IsolatedScript.Warm("""
            var objectValue = {}, nullValue = null;
            function run() {
                var sum = 0;
                for (var i = 0; i < 10000; i++) {
            """ + "sum += (objectValue " + comparison + " nullValue) ? 1 : 2;" + """
                }
                return sum;
            }
            run();
            """);
        if (workload.Run().AsNumber() != 20000)
        {
            throw new InvalidOperationException("Equality workload checksum failed.");
        }

        return workload;
    }

    [GlobalSetup]
    public void Setup()
    {
        _loose = Create("==");
        _strict = Create("===");
    }

    [Benchmark]
    public JsValue GenericLooseObjectNull() => _loose.Run();

    [Benchmark]
    public JsValue StrictObjectNullControl() => _strict.Run();
}
