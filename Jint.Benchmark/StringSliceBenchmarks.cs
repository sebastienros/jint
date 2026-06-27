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
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class StringSliceBenchmarks
{
    private Engine _engine = null!;
    private Prepared<Script> _sliceLargeDiscard;
    private Prepared<Script> _substringLargeDiscard;
    private Prepared<Script> _sliceSmall;
    private Prepared<Script> _sliceThenRead;
    private Prepared<Script> _searchOnSlice;
    private Prepared<Script> _searchOnFlat;

    [GlobalSetup]
    public void Setup()
    {
        _sliceLargeDiscard = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    ret = str.slice(0);
                    ret = str.slice(12000, -1);
                }
                return ret.length;
            })();
            """, strict: true);

        _substringLargeDiscard = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    ret = str.substring(0);
                    ret = str.substring(12000, -1);
                }
                return ret.length;
            })();
            """, strict: true);

        _sliceSmall = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    ret = str.slice(15000, 15005);
                    ret = str.slice(-1);
                    ret = str.substr(12000, 5);
                }
                return ret.length;
            })();
            """, strict: true);

        _sliceThenRead = Engine.PrepareScript("""
            (function() {
                var n = 0;
                for (var i = 0; i < 5000; i++) {
                    var t = str.slice(12000, -1);
                    n += t.length;
                    n += t.charCodeAt(100);
                }
                return n;
            })();
            """, strict: true);

        _searchOnSlice = Engine.PrepareScript("""
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
            """, strict: true);

        // Guard: searching a plain (non-view) JsString must not regress from making the base
        // search methods virtual. str.slice(0) returns a flat JsString, not a SlicedString.
        // Deliberately dispatch-bound (many cheap short searches that hit early / compare only the
        // needle) so the measurement isolates per-call dispatch overhead rather than the highly
        // thermal-sensitive throughput of one giant not-found scan.
        _searchOnFlat = Engine.PrepareScript("""
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
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        // Build the shared ~128K char base string once (dromaeo-style doubling).
        _engine.Execute("""
            var str = "aB3$xQ9pLm0_kEwZ";
            while (str.length < 131072) {
                str += str;
            }
            """);
        _engine.Evaluate(_sliceLargeDiscard);
        _engine.Evaluate(_substringLargeDiscard);
        _engine.Evaluate(_sliceSmall);
        _engine.Evaluate(_sliceThenRead);
        _engine.Evaluate(_searchOnSlice);
        _engine.Evaluate(_searchOnFlat);
    }

    [Benchmark]
    public JsValue SliceLargeDiscard() => _engine.Evaluate(_sliceLargeDiscard);

    [Benchmark]
    public JsValue SubstringLargeDiscard() => _engine.Evaluate(_substringLargeDiscard);

    [Benchmark]
    public JsValue SliceSmall() => _engine.Evaluate(_sliceSmall);

    [Benchmark]
    public JsValue SliceThenRead() => _engine.Evaluate(_sliceThenRead);

    [Benchmark]
    public JsValue SearchOnSlice() => _engine.Evaluate(_searchOnSlice);

    [Benchmark]
    public JsValue SearchOnFlat() => _engine.Evaluate(_searchOnFlat);
}
