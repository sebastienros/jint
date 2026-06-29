using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the per-realm allocation a lazily-initialized built-in pays when first touched — the cost
/// that generated built-in shapes (B1) target. <see cref="EngineOnly"/> constructs a bare engine (the
/// eagerly-built intrinsics); <see cref="EngineTouchMath"/> additionally forces MathInstance.Initialize.
/// The Allocated delta is Math's storage cost (its property dictionary + lazy descriptors).
/// </summary>
[MemoryDiagnoser]
public class BuiltinShapeBenchmark
{
    private readonly Prepared<Script> _touchMath;

    public BuiltinShapeBenchmark()
    {
        // Reference every Math member so the whole table materializes.
        _touchMath = Engine.PrepareScript(
            "Math.abs(Math.PI+Math.E+Math.LN2+Math.LN10+Math.LOG2E+Math.LOG10E+Math.SQRT1_2+Math.SQRT2);"
            + "Math.max(Math.min(Math.floor(1.5),Math.ceil(1.5)),Math.round(1.5),Math.sqrt(4),Math.pow(2,3));"
            + "Math.log(Math.exp(Math.atan2(1,1)+Math.sin(1)+Math.cos(1)+Math.tan(1)+Math.sign(1)+Math.trunc(1.5)+Math.cbrt(8)));");
    }

    [Benchmark(Baseline = true)]
    public Engine EngineOnly() => new Engine();

    [Benchmark]
    public Engine EngineTouchMath()
    {
        var engine = new Engine();
        engine.Evaluate(_touchMath);
        return engine;
    }

    // Forces MathInstance.Initialize (building the full property dictionary + all lazy descriptors) but
    // materializes only one dispatcher function — so the delta vs EngineOnly is the *fixed* per-realm
    // storage overhead B1 removes (dict + N lazy descriptors), not the per-function value cost.
    private static readonly Prepared<Script> _lightMath = Engine.PrepareScript("Math.abs(-1);");

    [Benchmark]
    public Engine EngineInitMath()
    {
        var engine = new Engine();
        engine.Evaluate(_lightMath);
        return engine;
    }
}
