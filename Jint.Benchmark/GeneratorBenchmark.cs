using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Measures generator instantiation and iteration cost. Generator bodies share their
/// compiled statement handler tree across instances (resume positions live on the
/// instance), so creating many short-lived generators from the same declaration must
/// not pay a per-instantiation handler tree rebuild. The large-body case is the worst
/// case for such rebuilds; the interleaved case pins cross-instance state isolation.
/// </summary>
[MemoryDiagnoser]
public class GeneratorBenchmark
{
    private Prepared<Script> _smallGenerators;
    private Prepared<Script> _largeBodyGenerators;
    private Prepared<Script> _interleavedGenerators;

    [GlobalSetup]
    public void Setup()
    {
        _smallGenerators = Engine.PrepareScript("""
            function* g(n) { yield n; yield n + 1; }
            let sum = 0;
            for (let i = 0; i < 1000; i++) {
                for (const v of g(i)) sum += v;
            }
            sum;
            """);

        _largeBodyGenerators = Engine.PrepareScript("""
            function* g(n) {
                let a0 = n + 0; let a1 = n + 1; let a2 = n + 2; let a3 = n + 3; let a4 = n + 4;
                let a5 = n + 5; let a6 = n + 6; let a7 = n + 7; let a8 = n + 8; let a9 = n + 9;
                if (a0 >= 0) {
                    a1 += a0; a2 += a1; a3 += a2; a4 += a3;
                    if (a4 >= 0) {
                        a5 += a4; a6 += a5; a7 += a6;
                    }
                }
                yield a7;
                for (let j = 0; j < 3; j++) {
                    a8 += j;
                    yield a8;
                }
                switch (n % 2) {
                    case 0:
                        a9 += 1;
                        break;
                    default:
                        a9 += 2;
                        break;
                }
                yield a9;
                return a0 + a9;
            }
            let sum = 0;
            for (let i = 0; i < 500; i++) {
                for (const v of g(i)) sum += v;
            }
            sum;
            """);

        _interleavedGenerators = Engine.PrepareScript("""
            function* g(n) {
                yield n;
                { yield n + 1; }
                yield n + 2;
            }
            let sum = 0;
            for (let i = 0; i < 500; i++) {
                const g1 = g(i), g2 = g(i + 1);
                sum += g1.next().value + g2.next().value;
                sum += g1.next().value + g2.next().value;
                sum += g1.next().value + g2.next().value;
            }
            sum;
            """);
    }

    [Benchmark]
    public void SmallGenerators_1000() => new Engine().Execute(_smallGenerators);

    [Benchmark]
    public void LargeBodyGenerators_500() => new Engine().Execute(_largeBodyGenerators);

    [Benchmark]
    public void InterleavedGenerators_500() => new Engine().Execute(_interleavedGenerators);
}
