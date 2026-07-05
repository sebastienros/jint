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
    // storage overhead the built-in shape removes (dict + N lazy descriptors), not the per-function value cost.
    private static readonly Prepared<Script> _lightMath = Engine.PrepareScript("Math.abs(-1);");

    [Benchmark]
    public Engine EngineInitMath()
    {
        var engine = new Engine();
        engine.Evaluate(_lightMath);
        return engine;
    }

    // Lightly touch each shape-backed built-in (Math, JSON, Reflect, Atomics) so all four initialize.
    // The delta vs EngineOnly is the aggregate per-realm storage overhead removed across them.
    private static readonly Prepared<Script> _initBuiltins = Engine.PrepareScript(
        "Math.abs(-1); JSON.stringify(0); Reflect.has({},'x'); Atomics.isLockFree(4);");

    [Benchmark]
    public Engine EngineInitBuiltins()
    {
        var engine = new Engine();
        engine.Evaluate(_initBuiltins);
        return engine;
    }

    // Forces Temporal.Now (9 functions) to initialize — newly shape-backed here, dictionary on main.
    private static readonly Prepared<Script> _initTemporalNow = Engine.PrepareScript("Temporal.Now.instant();");

    [Benchmark]
    public Engine EngineInitTemporalNow()
    {
        var engine = new Engine();
        engine.Evaluate(_initTemporalNow);
        return engine;
    }

    // Forces the Generator + AsyncGenerator prototypes (3 functions + a constructor instance property each)
    // to initialize — newly shape-backed here (via instance-property support), dictionary on main.
    private static readonly Prepared<Script> _initGenerators = Engine.PrepareScript(
        "(function*(){})().next; (async function*(){})().next;");

    [Benchmark]
    public Engine EngineInitGenerators()
    {
        var engine = new Engine();
        engine.Evaluate(_initGenerators);
        return engine;
    }

    // Forces the function-only Prototype-derived prototypes shaped in this campaign to Initialize —
    // dictionary on main, shape here. The delta vs EngineOnly is the aggregate per-realm storage overhead
    // removed across them.
    private static readonly Prepared<Script> _initPrototypes = Engine.PrepareScript(
        "Promise.prototype.then; WeakMap.prototype.has; WeakSet.prototype.has; WeakRef.prototype.deref; FinalizationRegistry.prototype.register;"
        + "BigInt.prototype.toString; AggregateError.prototype.name; SuppressedError.prototype.constructor;"
        + "Uint8Array.prototype.setFromBase64; ShadowRealm.prototype.evaluate;"
        + "Object.getPrototypeOf(function*(){}).constructor; Object.getPrototypeOf(async function(){}).constructor;"
        + "Object.getPrototypeOf(async function*(){}).constructor;"
        + "Map.prototype.has; Symbol.prototype.toString; ArrayBuffer.prototype.slice;"
        + "DataView.prototype.getInt8; Iterator.prototype.toArray;"
        + "Temporal.Duration.prototype.toString; Temporal.PlainDate.prototype.toString;"
        + "Temporal.PlainDateTime.prototype.toString; Temporal.ZonedDateTime.prototype.toString;"
        + "Temporal.Instant.prototype.toString; Temporal.PlainTime.prototype.toString;"
        + "Temporal.PlainMonthDay.prototype.toString; Temporal.PlainYearMonth.prototype.toString;"
        + "Intl.Locale.prototype.toString; Intl.Collator.prototype.resolvedOptions;"
        + "Intl.DateTimeFormat.prototype.resolvedOptions; Intl.NumberFormat.prototype.resolvedOptions;");

    [Benchmark]
    public Engine EngineInitPrototypes()
    {
        var engine = new Engine();
        engine.Evaluate(_initPrototypes);
        return engine;
    }

    // Forces the shaped constructors to Initialize (touch a static member on each) — dictionary on main,
    // shape here. Constructors are Function-derived; the delta vs EngineOnly is the per-realm constructor
    // storage overhead removed.
    private static readonly Prepared<Script> _initConstructors = Engine.PrepareScript(
        "Object.keys; Array.isArray; ArrayBuffer.isView; Map.groupBy; Set.prototype; Symbol.for; Promise.resolve;");

    [Benchmark]
    public Engine EngineInitConstructors()
    {
        var engine = new Engine();
        engine.Evaluate(_initConstructors);
        return engine;
    }
}
