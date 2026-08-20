using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The global <c>structuredClone</c> function — <see cref="WebApiFeatures.StructuredClone"/>,
/// https://html.spec.whatwg.org/multipage/structured-data.html#dom-structuredclone.
///
/// <para>Three rows along the axis that decides the cost. <see cref="CloneFlat"/> is a twelve-property
/// record of primitives, so it is per-property ceremony and nothing else — the shape a worker-style
/// message or a cached configuration object actually has. <see cref="CloneNested"/> is a 32-level graph
/// carrying the platform objects the algorithm has separate serialization steps for (<c>Map</c>,
/// <c>Set</c>, <c>Date</c>, <c>RegExp</c>, a typed array) plus a cycle, so it also measures the memo that
/// makes the algorithm terminate. <see cref="CloneTransfer"/> is the transfer path
/// (https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializewithtransfer): a 4 KiB
/// <c>ArrayBuffer</c> moved into the clone rather than copied, with the source detached, so a regression
/// that turned a transfer back into a copy would show up here as time <i>and</i> as allocated bytes.</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine carrying
/// <see cref="WebApiFeatures.StructuredClone"/> alone, warmed with its own fixture and its own script and
/// nothing else — see <see cref="WebApiBenchmarkSupport"/>. Engine construction and building the source
/// graph stay in <c>[GlobalSetup]</c> and never enter the measurement.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory(WebApiBenchmarkSupport.Category)]
public class WebApiStructuredCloneBenchmark
{
    private IsolatedScript _cloneFlat;
    private IsolatedScript _cloneNested;
    private IsolatedScript _cloneTransfer;

    [GlobalSetup]
    public void Setup()
    {
        _cloneFlat = Row(
            """
            var FLAT = {
                id: 1234, name: 'widget', sku: 'WGT-0042', price: 19.99, active: true,
                tags: 'a,b,c', created: '2026-01-01T00:00:00.000Z', qty: 7, weight: 0.5,
                note: null, ratio: 0.125, code: 'X'
            };
            function cloneFlat() {
                var n = 0;
                for (var i = 0; i < 500; i++) { n += structuredClone(FLAT).id; }
                return n;
            }
            """,
            "cloneFlat()");

        // The cycle is deliberate: without the memo the algorithm would not terminate at all, so a row
        // that did not carry one would never measure it.
        _cloneNested = Row(
            """
            var NESTED = (function () {
                var root = {
                    level: 0, children: [], meta: new Map([['k', 'v'], ['n', 42]]),
                    seen: new Set([1, 2, 3]), when: new Date(0), pattern: /ab+c/gi,
                    bytes: new Uint8Array([1, 2, 3, 4, 5, 6, 7, 8])
                };
                var node = root;
                for (var i = 1; i < 32; i++) {
                    var child = { level: i, children: [], values: [i, i * 2, 'n' + i] };
                    node.children.push(child);
                    node = child;
                }
                root.self = root;
                return root;
            })();
            function cloneNested() {
                var n = 0;
                for (var i = 0; i < 50; i++) {
                    var clone = structuredClone(NESTED);
                    n += clone.children[0].level + clone.meta.size + clone.seen.size + clone.bytes.length;
                }
                return n;
            }
            """,
            "cloneNested()");

        _cloneTransfer = Row(
            """
            function cloneTransfer() {
                var n = 0;
                for (var i = 0; i < 200; i++) {
                    var buffer = new ArrayBuffer(4096);
                    new Uint8Array(buffer)[0] = 7;
                    var clone = structuredClone({ payload: buffer }, { transfer: [buffer] });
                    n += new Uint8Array(clone.payload)[0] + buffer.byteLength;
                }
                return n;
            }
            """,
            "cloneTransfer()");
    }

    private static IsolatedScript Row(string fixture, string call)
        => WebApiBenchmarkSupport.DeterministicRow(WebApiFeatures.StructuredClone, fixture, call);

    /// <summary>500 clones of a twelve-property record of primitives.</summary>
    [Benchmark]
    public JsValue CloneFlat() => _cloneFlat.Run();

    /// <summary>50 clones of a 32-level graph carrying a Map, a Set, a Date, a RegExp, a typed array and a cycle.</summary>
    [Benchmark]
    public JsValue CloneNested() => _cloneNested.Run();

    /// <summary>200 transfers of a 4 KiB <c>ArrayBuffer</c>, each leaving the source detached.</summary>
    [Benchmark]
    public JsValue CloneTransfer() => _cloneTransfer.Run();
}
