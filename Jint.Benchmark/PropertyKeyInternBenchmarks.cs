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
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one shared <c>_warm</c> engine warmed with all
/// five scripts, so each row was measured on an engine carrying its siblings' globals (four of the five
/// scripts declare <c>o</c>, <c>s</c> and <c>i</c>) and their handler-tree and call-site state — which on
/// a property-name bench also meant the other rows' key set was already interned. The rows still measure
/// warm dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the
/// measurement. <b>Numbers from this class are not comparable to any published before the harness
/// changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class PropertyKeyInternBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    private IsolatedScript _commonKeys;
    private IsolatedScript _rareKeys;
    private IsolatedScript _mixedKeys;
    private IsolatedScript _arrayLengthTightLoop;
    private IsolatedScript _objectKeysTightLoop;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Common keys: every name appears in built-in surfaces, so they're prime candidates for
        // the perfect-hash hit path.
        _commonKeys = IsolatedScript.Warm(@"
            var o = {length:1, value:2, next:3, constructor:4, name:5, prototype:6};
            var s = 0;
            for (var i = 0; i < 1000; i++) {
                s += o.length + o.value + o.next + o.constructor + o.name + o.prototype;
            }
            s
        ");

        // Rare keys: user-domain names that won't be in the perfect-hash table.
        _rareKeys = IsolatedScript.Warm(@"
            var o = {alpha:1, bravo:2, charlie:3, delta:4, echo:5, foxtrot:6};
            var s = 0;
            for (var i = 0; i < 1000; i++) {
                s += o.alpha + o.bravo + o.charlie + o.delta + o.echo + o.foxtrot;
            }
            s
        ");

        // Mixed: 3 common, 3 rare.
        _mixedKeys = IsolatedScript.Warm(@"
            var o = {length:1, value:2, next:3, alpha:4, bravo:5, charlie:6};
            var s = 0;
            for (var i = 0; i < 1000; i++) {
                s += o.length + o.value + o.next + o.alpha + o.bravo + o.charlie;
            }
            s
        ");

        // .length is the single most common property access — measure it in isolation.
        _arrayLengthTightLoop = IsolatedScript.Warm("var a = [1,2,3,4,5]; var s = 0; for (var i = 0; i < 1000; i++) s += a.length; s");

        // Object.keys returns a list of property names — exercises the name-allocation path.
        _objectKeysTightLoop = IsolatedScript.Warm(@"
            var o = {a:1, b:2, c:3, d:4, e:5};
            var n = 0;
            for (var i = 0; i < 1000; i++) n += Object.keys(o).length;
            n
        ");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_CommonKeys() => _commonKeys.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_RareKeys() => _rareKeys.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_MixedKeys() => _mixedKeys.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ArrayLength() => _arrayLengthTightLoop.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_ObjectKeys() => _objectKeysTightLoop.Run();
}
