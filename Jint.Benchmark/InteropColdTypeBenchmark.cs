using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Cold-cache first-touch of a CLR type: fresh engine per operation, single property read.
/// Captures TypeResolver.ResolvePropertyDescriptorFactory + TypeDescriptor analysis cost.
/// Regression guard so warm-path caching changes don't inflate first-touch cost.
/// (TypeDescriptor's static per-process cache stays warm across iterations; the per-engine
/// reflection-accessor cache is what gets rebuilt here.)
/// </summary>
[MemoryDiagnoser]
public class InteropColdTypeBenchmark
{
    public sealed class ColdPoco
    {
        public int Number { get; set; }
        public string Text { get; set; } = string.Empty;

        public int Bump(int delta) => Number + delta;
    }

    private readonly ColdPoco _instance = new() { Number = 1, Text = "x" };

    [Benchmark]
    public void ColdFirstTouch_NewType()
    {
        var engine = new Engine();
        engine.SetValue("p", _instance);
        engine.Execute("p.Number; p.Text; p.Bump(1);");
    }
}
