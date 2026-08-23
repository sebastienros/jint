using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates String.prototype.slice/substring/substr cost over a large (128K char) string —
/// the dromaeo-object-string shape where large slice results are assigned and discarded.
/// Both SliceLargeDiscard and SubstringLargeDiscard use the actual dromaeo arguments
/// (start, -1): substring clamps the -1 to 0 and swaps, producing a 12000-char result that
/// previously fell below the zero-copy retention guard and copied on every call.
/// SliceSmall guards the small-result path; SliceThenRead guards lazy-materialization cost
/// when the result is actually consumed.
/// SearchOnSlice exercises indexOf/startsWith/endsWith/includes on a fresh large slice each
/// iteration — the case where the inherited base search methods materialize the whole substring
/// on every call. With zero-copy span search overrides this drops to ~0 allocation.
/// SliceOfSlice slices a large view of a view: without receiver unwrapping the second slice
/// materializes the intermediate view; with it, it rebases straight onto the backing string.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs the fixture so each engine owns its own <c>str</c> — and warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all seven row
/// scripts, so each row was measured on an engine carrying the other six rows' handler-tree entries,
/// per-call-site caches and — because these rows differ precisely in whether a result is a flat string
/// or a zero-copy view — their string-representation history. That makes a row's number depend on which
/// siblings exist and on what a change did to <em>them</em>. The rows still measure warm slicing, and
/// engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers
/// from this class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class StringSliceBenchmarks
{
    private IsolatedScript _sliceLargeDiscard;
    private IsolatedScript _substringLargeDiscard;
    private IsolatedScript _sliceSmall;
    private IsolatedScript _sliceThenRead;
    private IsolatedScript _searchOnSlice;
    private IsolatedScript _searchOnFlat;
    private IsolatedScript _sliceOfSlice;

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine(static options => options.Strict = true);
        // Build the shared ~128K char base string once (dromaeo-style doubling) — once per row's
        // own engine, so no row ever observes another row's string representations.
        engine.Execute("""
            var str = "aB3$xQ9pLm0_kEwZ";
            while (str.length < 131072) {
                str += str;
            }
            """);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _sliceLargeDiscard = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    ret = str.slice(0);
                    ret = str.slice(12000, -1);
                }
                return ret.length;
            })();
            """, strict: true), CreateEngine);

        _substringLargeDiscard = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    ret = str.substring(0);
                    ret = str.substring(12000, -1);
                }
                return ret.length;
            })();
            """, strict: true), CreateEngine);

        _sliceSmall = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    ret = str.slice(15000, 15005);
                    ret = str.slice(-1);
                    ret = str.substr(12000, 5);
                }
                return ret.length;
            })();
            """, strict: true), CreateEngine);

        _sliceThenRead = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var n = 0;
                for (var i = 0; i < 5000; i++) {
                    var t = str.slice(12000, -1);
                    n += t.length;
                    n += t.charCodeAt(100);
                }
                return n;
            })();
            """, strict: true), CreateEngine);

        _searchOnSlice = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var hits = 0;
                for (var i = 0; i < 5000; i++) {
                    var sub = str.slice(12000, -1);
                    if (sub.indexOf("~absent~") !== -1) hits++;
                    if (sub.startsWith("aB3$x")) hits++;
                    if (sub.endsWith("_kEw")) hits++;
                    if (sub.includes("Q9pLm")) hits++;
                }
                return hits;
            })();
            """, strict: true), CreateEngine);

        // Guard: searching a plain (non-view) JsString must not regress from making the base
        // search methods virtual. str.slice(0) returns a flat JsString, not a SlicedString.
        // Deliberately dispatch-bound (many cheap short searches that hit early / compare only the
        // needle) so the measurement isolates per-call dispatch overhead rather than the highly
        // thermal-sensitive throughput of one giant not-found scan.
        _searchOnFlat = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var hits = 0;
                var flat = str.slice(0);
                for (var i = 0; i < 100000; i++) {
                    if (flat.indexOf("aB3$x") === 0) hits++;
                    if (flat.startsWith("aB3$xQ9")) hits++;
                    if (flat.endsWith("kEwZ")) hits++;
                    if (flat.includes("aB3$x")) hits++;
                }
                return hits;
            })();
            """, strict: true), CreateEngine);

        // Slice-of-slice: the first slice returns a large zero-copy view; the second slices that view.
        // Without receiver unwrapping the second slice materializes the intermediate view first; with
        // it, the second slice rebases straight onto the original backing string.
        _sliceOfSlice = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    var outer = str.slice(0, 100000);
                    ret = outer.slice(1000, 60000);
                }
                return ret.length;
            })();
            """, strict: true), CreateEngine);
    }

    [Benchmark]
    public JsValue SliceLargeDiscard() => _sliceLargeDiscard.Run();

    [Benchmark]
    public JsValue SubstringLargeDiscard() => _substringLargeDiscard.Run();

    [Benchmark]
    public JsValue SliceSmall() => _sliceSmall.Run();

    [Benchmark]
    public JsValue SliceThenRead() => _sliceThenRead.Run();

    [Benchmark]
    public JsValue SearchOnSlice() => _searchOnSlice.Run();

    [Benchmark]
    public JsValue SearchOnFlat() => _searchOnFlat.Run();

    [Benchmark]
    public JsValue SliceOfSlice() => _sliceOfSlice.Run();
}
