#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#the-domstringlist-interface.</summary>
internal sealed class DomStringListConstructor : IndexedDbConstructor
{
    internal DomStringListConstructor(Engine engine, Realm realm)
        : base(engine, realm, "DOMStringList", realm.Intrinsics.Function.PrototypeObject, deferPrototype: true)
    {
        PrototypeObject = new DomStringListPrototype(engine, realm, this);
        InitializeInterface(0);
    }
}

internal sealed class JsDomStringList : ArrayLikeObject
{
    private readonly Func<IReadOnlyList<string>> _values;
    internal JsDomStringList(Engine engine, Realm realm, IReadOnlyList<string> values) : this(engine, realm, () => values) { }
    internal JsDomStringList(Engine engine, Realm realm, Func<IReadOnlyList<string>> values) : base(engine)
    {
        _values = values;
        _prototype = realm.Intrinsics.DomStringList.PrototypeObject;
    }
    public override uint Length => (uint) _values().Count;
    protected override bool OwnsLength => false;
    public override bool TryGetIndex(uint index, out JsValue value)
    {
        var values = _values();
        if (index < (uint) values.Count) { value = JsString.Create(values[(int) index]); return true; }
        value = Undefined;
        return false;
    }
    internal JsValue Item(uint index) => TryGetIndex(index, out var value) ? value : Null;
    internal bool Contains(string value) => _values().Contains(value, StringComparer.Ordinal);
}

/// <summary>Shared by IndexedDB and browser CSS style-sheet sets.</summary>
[JsObject(UseShape = true)]
internal sealed partial class DomStringListPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly DomStringListConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("DOMStringList");
    [JsSymbol("Iterator", Flags = PropertyFlag.NonEnumerable)]
    private JsValue Iterator => _realm.Intrinsics.Array.PrototypeObject.Get(GlobalSymbolRegistry.Iterator);
    internal DomStringListPrototype(Engine engine, Realm realm, DomStringListConstructor constructor) : base(engine, realm)
    { _constructor = constructor; _prototype = realm.Intrinsics.Object.PrototypeObject; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    private JsDomStringList Brand(JsValue thisObject) => IndexedDbErrors.Brand<JsDomStringList>(_realm, thisObject);
    [JsAccessor("length", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber LengthGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).Length);
    [JsFunction(Name = "item", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Item(JsValue thisObject, JsCallArguments args)
    {
        var list = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        return list.Item(TypeConverter.ToUint32(args[0]));
    }
    [JsFunction(Name = "contains", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsBoolean Contains(JsValue thisObject, JsCallArguments args)
    {
        var list = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        return JsBoolean.Create(list.Contains(TypeConverter.ToString(args[0])));
    }
}
#endif
