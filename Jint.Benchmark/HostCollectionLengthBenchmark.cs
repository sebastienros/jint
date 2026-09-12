#nullable enable

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Benchmark;

/// <summary>
/// The loop a page writes over a host collection — <c>for (var j = 0; j &lt; list.length; j++) list[j]</c> —
/// where the collection is a WebIDL one: <c>length</c> is an accessor on the interface prototype, not an own
/// property. It is the shape <c>dom/nodes/NodeList-static-length-getter-tampered-*.html</c> runs about
/// 76 million times, and the one sebastienros/jint#3947 measured at roughly 200 ns an iteration.
///
/// <para><b>What each row is</b></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="InheritedLength"/> — the WebIDL arrangement, with the host declaring the accessor its prototype
/// was created with. This is the row the change is about.
/// </description></item>
/// <item><description>
/// <see cref="InheritedLengthTampered"/> — the same collection after a page has redefined that accessor, which
/// is what the three web-platform-tests documents do half way through. The lane must decline here, so this row
/// measures the *cost of declining* — it is the row that must not regress, and the reason a negative verdict
/// is cached rather than re-derived per read.
/// </description></item>
/// <item><description>
/// <see cref="InheritedLengthUndeclared"/> — the same collection with no accessor declared, which is every
/// <c>ArrayLikeObject</c> written before the hook existed. It is the *before* picture, measurable in the same
/// process as the after.
/// </description></item>
/// <item><description>
/// <see cref="OwnedLength"/> — the <c>ArrayLikeObject</c> default, where <c>length</c> is an own property.
/// </description></item>
/// <item><description>
/// <see cref="PlainArray"/> — the control and the floor: the identical loop over a real
/// <see cref="JsArray"/> of the same size. The gap to it is the price of the collection
/// being live rather than copied.
/// </description></item>
/// </list>
///
/// <para>
/// <b>Restricted to the public surface deliberately.</b> <c>Jint.Benchmark</c> has <c>InternalsVisibleTo</c>,
/// so a host type written here could reach members no real embedder has. <see cref="CollectionList"/> uses
/// only what a third-party assembly sees: the two <see cref="ArrayLikeObject"/> abstract members,
/// <c>OwnsLength</c>, <c>PristineLengthGetter</c> and <see cref="ObjectInstance.Prototype"/>. The prototype
/// and its accessor are built in script, exactly as a binding's shaped prototype would supply them.
/// </para>
///
/// <para>
/// <b>Engine isolation.</b> One engine per row (<see cref="IsolatedScript"/>), built by
/// <c>CreateEngine</c> and warmed with that row's own script and nothing else, so a row's number never
/// depends on which siblings warmed which lane first. The backing <see cref="List{T}"/> is shared because it
/// is plain CLR state with no engine affinity; the receiver in front of it is per engine, as an
/// <see cref="ObjectInstance"/> must be. Engine construction and warm-up stay in <c>[GlobalSetup]</c>,
/// outside the measurement.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class HostCollectionLengthBenchmark
{
    /// <summary>
    /// 400 elements — the size sebastienros/jint#3947 measured, and about four times the 100-element list the
    /// web-platform-tests documents build. Small enough that the per-iteration cost, not cache behaviour, is
    /// what the row reports.
    /// </summary>
    [Params(400)]
    public int Count { get; set; }

    private IsolatedScript _inheritedLength;
    private IsolatedScript _inheritedLengthTampered;
    private IsolatedScript _inheritedLengthUndeclared;
    private IsolatedScript _ownedLength;
    private IsolatedScript _plainArray;

    private const string Loop = "var n = 0; for (var j = 0; j < list.length; j++) { if (list[j] !== undefined) { n++; } } n;";

    /// <summary>
    /// A WebIDL-shaped collection: <c>length</c> lives on the prototype. <paramref name="declareAccessor"/>
    /// decides whether the host tells the engine which accessor that is, which is the whole opt-in.
    /// </summary>
    private sealed class CollectionList : ArrayLikeObject
    {
        private readonly List<string> _items;
        private readonly ObjectInstance? _lengthGetter;

        public CollectionList(Engine engine, List<string> items, ObjectInstance prototype, ObjectInstance? lengthGetter)
            : base(engine)
        {
            _items = items;
            _lengthGetter = lengthGetter;
            Prototype = prototype;
        }

        public override uint Length => (uint) _items.Count;

        protected override bool OwnsLength => false;

        protected override ObjectInstance? PristineLengthGetter => _lengthGetter;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < (uint) _items.Count)
            {
                value = _items[(int) index];
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }
    }

    /// <summary>The <c>ArrayLikeObject</c> default: <c>length</c> is an own property backed by <c>Length</c>.</summary>
    private sealed class OwnedLengthList : ArrayLikeObject
    {
        private readonly List<string> _items;

        public OwnedLengthList(Engine engine, List<string> items) : base(engine)
        {
            _items = items;
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public override uint Length => (uint) _items.Count;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < (uint) _items.Count)
            {
                value = _items[(int) index];
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }
    }

    private static List<string> BuildItems(int count)
    {
        var items = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add("item-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return items;
    }

    private static Engine CreateCollectionEngine(List<string> items, bool declareAccessor, bool tamper)
    {
        var engine = new Engine();
        CollectionList? collection = null;
        engine.SetValue("__count", new Func<double>(() => collection!.Length));

        var prototype = engine.Evaluate("""
            (function () {
                var proto = Object.create(Array.prototype);
                Object.defineProperty(proto, 'length', {
                    configurable: true,
                    enumerable: true,
                    get: function () { return __count(); }
                });
                return proto;
            })()
            """).AsObject();

        var declared = declareAccessor ? prototype.GetOwnProperty("length").Get!.AsObject() : null;
        collection = new CollectionList(engine, items, prototype, declared);
        engine.SetValue("list", collection);

        if (tamper)
        {
            // Exactly what NodeList-static-length-getter-tampered-3.html does: a fresh accessor under the
            // same name, which the lane must never mistake for the declared one however it answers.
            engine.SetValue("proto", prototype);
            engine.Execute("Object.defineProperty(proto, 'length', { configurable: true, get: function () { return __count(); } })");
        }

        return engine;
    }

    private static Engine CreateOwnedLengthEngine(List<string> items)
    {
        var engine = new Engine();
        engine.SetValue("list", new OwnedLengthList(engine, items));
        return engine;
    }

    // Deliberately a real JsArray rather than JsValue.FromObject over a CLR array, which would produce an
    // interop wrapper and measure a third representation instead of the dense-array floor.
    private static Engine CreatePlainArrayEngine(List<string> items)
    {
        var engine = new Engine();
        var values = new JsValue[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = items[i];
        }

        engine.SetValue("list", new JsArray(engine, values));
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        var items = BuildItems(Count);
        var loop = Engine.PrepareScript(Loop);

        _inheritedLength = IsolatedScript.Warm(loop, () => CreateCollectionEngine(items, declareAccessor: true, tamper: false));
        _inheritedLengthTampered = IsolatedScript.Warm(loop, () => CreateCollectionEngine(items, declareAccessor: true, tamper: true));
        _inheritedLengthUndeclared = IsolatedScript.Warm(loop, () => CreateCollectionEngine(items, declareAccessor: false, tamper: false));
        _ownedLength = IsolatedScript.Warm(loop, () => CreateOwnedLengthEngine(items));
        _plainArray = IsolatedScript.Warm(loop, () => CreatePlainArrayEngine(items));
    }

    [Benchmark]
    public JsValue InheritedLength() => _inheritedLength.Run();

    [Benchmark]
    public JsValue InheritedLengthTampered() => _inheritedLengthTampered.Run();

    [Benchmark]
    public JsValue InheritedLengthUndeclared() => _inheritedLengthUndeclared.Run();

    [Benchmark]
    public JsValue OwnedLength() => _ownedLength.Run();

    [Benchmark(Baseline = true)]
    public JsValue PlainArray() => _plainArray.Run();
}
