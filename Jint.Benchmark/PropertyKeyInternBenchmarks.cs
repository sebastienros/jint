using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Property-name interning bench. Gates the perfect-hash KnownKeys table (Phase 4 of the source-gen
/// plan) — a generator stage that collects every property name registered by every [JsObject] /
/// [JsAccessible] type at compile time, emits a perfect-hash function over them, and exposes
/// `KnownKeys.TryIntern(ReadOnlySpan&lt;char&gt;, out JsString)`. Hits skip both the JsString
/// allocation and the dictionary's string-comparison probe in favour of identity comparison.
///
/// Scenarios cover three regimes the perfect-hash should improve:
///   - All-common keys ("length", "value", "next", "constructor"): every read should be a hit.
///   - Mixed common/rare: half hit, half miss → measures the miss path's cost (which still hashes
///     and probes via the existing dictionary).
///   - All-rare keys (user-domain names): all-miss baseline. Phase 4 should leave this unchanged.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class PropertyKeyInternBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    private Engine _warm = null!;
    private Prepared<Script> _commonKeys;
    private Prepared<Script> _rareKeys;
    private Prepared<Script> _mixedKeys;
    private Prepared<Script> _arrayLengthTightLoop;
    private Prepared<Script> _objectKeysTightLoop;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Common keys: every name appears in built-in surfaces, so they're prime candidates for
        // the perfect-hash hit path.
        _commonKeys = Engine.PrepareScript(@"
            var o = {length:1, value:2, next:3, constructor:4, name:5, prototype:6};
            var s = 0;
            for (var i = 0; i < 1000; i++) {
                s += o.length + o.value + o.next + o.constructor + o.name + o.prototype;
            }
            s
        ");

        // Rare keys: user-domain names that won't be in the perfect-hash table.
        _rareKeys = Engine.PrepareScript(@"
            var o = {alpha:1, bravo:2, charlie:3, delta:4, echo:5, foxtrot:6};
            var s = 0;
            for (var i = 0; i < 1000; i++) {
                s += o.alpha + o.bravo + o.charlie + o.delta + o.echo + o.foxtrot;
            }
            s
        ");

        // Mixed: 3 common, 3 rare.
        _mixedKeys = Engine.PrepareScript(@"
            var o = {length:1, value:2, next:3, alpha:4, bravo:5, charlie:6};
            var s = 0;
            for (var i = 0; i < 1000; i++) {
                s += o.length + o.value + o.next + o.alpha + o.bravo + o.charlie;
            }
            s
        ");

        // .length is the single most common property access — measure it in isolation.
        _arrayLengthTightLoop = Engine.PrepareScript("var a = [1,2,3,4,5]; var s = 0; for (var i = 0; i < 1000; i++) s += a.length; s");

        // Object.keys returns a list of property names — exercises the name-allocation path.
        _objectKeysTightLoop = Engine.PrepareScript(@"
            var o = {a:1, b:2, c:3, d:4, e:5};
            var n = 0;
            for (var i = 0; i < 1000; i++) n += Object.keys(o).length;
            n
        ");

        _warm = new Engine();
        _warm.Evaluate(_commonKeys);
        _warm.Evaluate(_rareKeys);
        _warm.Evaluate(_mixedKeys);
        _warm.Evaluate(_arrayLengthTightLoop);
        _warm.Evaluate(_objectKeysTightLoop);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_CommonKeys() => _warm.Evaluate(_commonKeys);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_RareKeys() => _warm.Evaluate(_rareKeys);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_MixedKeys() => _warm.Evaluate(_mixedKeys);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ArrayLength() => _warm.Evaluate(_arrayLengthTightLoop);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ObjectKeys() => _warm.Evaluate(_objectKeysTightLoop);
}
