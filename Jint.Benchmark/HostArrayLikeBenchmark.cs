#nullable enable

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Benchmark;

/// <summary>
/// What a host pays to expose a <b>live indexed collection</b> — a DOM <c>NodeList</c>, a result window, any
/// lazily projected list — to a script that reads it the two ways such scripts read: an indexed <c>for</c> loop
/// and an <c>Array.prototype</c> generic.
///
/// <para><b>What each parameter proves</b></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="HostArrayLikeKind.PlainHost"/> — the shape an embedder writes today without
/// <see cref="ArrayLikeObject"/>: an <see cref="ObjectInstance"/> subclass overriding
/// <see cref="ObjectInstance.GetOwnProperty"/> to answer index keys and <c>length</c>, with
/// <c>Array.prototype</c> attached. Correct, and entirely off both lanes: <c>list[i]</c> rents a
/// <see cref="Jint.Runtime.Reference"/> and runs the full <c>GetValue</c> pipeline, and every generic goes
/// through the dispatcher's generic per-key path — a <see cref="JsString"/> key, a full <c>HasProperty</c>
/// prototype walk and a <c>Get</c> per element, plus a fresh <see cref="PropertyDescriptor"/> for each.
/// </description></item>
/// <item><description>
/// <see cref="HostArrayLikeKind.ArrayLike"/> — the same projection as an <see cref="ArrayLikeObject"/>. Both
/// lanes engage: the interpreter's computed-read branch resolves <c>list[i]</c> from one
/// <see cref="ArrayLikeObject.TryGetIndex"/> with no reference and no descriptor, and the
/// <c>ArrayOperations</c> dispatcher drives every generic and the array iterator from the same single call per
/// element. Read <c>Allocated</c> next to <c>Mean</c>: the descriptor and key churn is most of the delta.
/// </description></item>
/// <item><description>
/// <see cref="HostArrayLikeKind.JsArrayCopy"/> — the control, and the right answer whenever the collection is a
/// static snapshot: copy it into a real <see cref="Jint.Native.Array.JsArray"/> once and let the dense lanes do
/// the work. It is the floor the two host rows are measured against, and the gap that remains is the price of
/// the collection being live rather than copied.
/// </description></item>
/// </list>
///
/// <para>
/// <b>Restricted to the public surface deliberately.</b> <c>Jint.Benchmark</c> has <c>InternalsVisibleTo</c>, so
/// a host type written here could reach members no real embedder has. Both host types below are limited to what
/// a third-party assembly can see — the two <see cref="ArrayLikeObject"/> abstract members,
/// <see cref="ObjectInstance.GetOwnProperty"/>, <see cref="ObjectInstance.Prototype"/> and
/// <c>Engine.Intrinsics</c> — so the numbers are the ones an embedder can actually reproduce. The restriction
/// bites in one place worth naming: <see cref="PlainHostList"/> cannot claim the internal <c>IsArrayLike</c>
/// flag, so it advertises a <c>length</c> property and lets the engine's dynamic probe find it, which is exactly
/// what a real host has to do.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class HostArrayLikeBenchmark
{
    public enum HostArrayLikeKind
    {
        PlainHost,
        ArrayLike,
        JsArrayCopy,
    }

    [Params(HostArrayLikeKind.PlainHost, HostArrayLikeKind.ArrayLike, HostArrayLikeKind.JsArrayCopy)]
    public HostArrayLikeKind Kind { get; set; }

    [Params(10_000)]
    public int Count { get; set; }

    private Engine _engine = null!;
    private Prepared<Script> _indexedLoop;
    private Prepared<Script> _join;
    private Prepared<Script> _forOf;

    private static List<string> BuildItems(int count)
    {
        var items = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add("item-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return items;
    }

    // Deliberately a real JsArray rather than JsValue.FromObject over a CLR array, which would produce an
    // interop wrapper and measure a third representation instead of the dense-array floor.
    private static JsArray CopyToJsArray(Engine engine, List<string> items)
    {
        var values = new JsValue[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = items[i];
        }

        return new JsArray(engine, values);
    }

    /// <summary>
    /// The pre-<see cref="ArrayLikeObject"/> shape: index and <c>length</c> answered by materializing a
    /// descriptor per read.
    /// </summary>
    private sealed class PlainHostList : ObjectInstance
    {
        private readonly List<string> _items;

        public PlainHostList(Engine engine, List<string> items) : base(engine)
        {
            _items = items;
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            var name = property.ToString();
            if (string.Equals(name, "length", System.StringComparison.Ordinal))
            {
                return new PropertyDescriptor(JsNumber.Create(_items.Count), writable: false, enumerable: false, configurable: true);
            }

            if (uint.TryParse(name, out var index) && index < (uint) _items.Count)
            {
                return new PropertyDescriptor(_items[(int) index], writable: false, enumerable: true, configurable: true);
            }

            return base.GetOwnProperty(property);
        }
    }

    private sealed class ArrayLikeHostList : ArrayLikeObject
    {
        private readonly List<string> _items;

        public ArrayLikeHostList(Engine engine, List<string> items) : base(engine)
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

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        var items = BuildItems(Count);

        JsValue list = Kind switch
        {
            HostArrayLikeKind.PlainHost => new PlainHostList(_engine, items),
            HostArrayLikeKind.ArrayLike => new ArrayLikeHostList(_engine, items),
            _ => CopyToJsArray(_engine, items),
        };

        _engine.SetValue("list", list);

        _indexedLoop = Engine.PrepareScript("var n = 0; for (var i = 0; i < list.length; i++) { if (list[i].length > 4) { n++; } } n;");
        _join = Engine.PrepareScript("list.join(',').length;");
        _forOf = Engine.PrepareScript("var n = 0; for (var x of list) { n += x.length; } n;");

        // Warm the per-node caches — a host that runs the same script repeatedly is the case this measures.
        _engine.Evaluate(_indexedLoop);
        _engine.Evaluate(_join);
        _engine.Evaluate(_forOf);
    }

    [Benchmark]
    public JsValue IndexedLoop() => _engine.Evaluate(_indexedLoop);

    [Benchmark]
    public JsValue Join() => _engine.Evaluate(_join);

    [Benchmark]
    public JsValue ForOf() => _engine.Evaluate(_forOf);
}
