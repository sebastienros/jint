#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://w3c.github.io/IndexedDB/#keyrange-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbKeyRangeConstructor : IndexedDbConstructor
{
    internal IdbKeyRangeConstructor(Engine engine, Realm realm)
        : base(engine, realm, "IDBKeyRange", realm.Intrinsics.Function.PrototypeObject, deferPrototype: true)
    {
        PrototypeObject = new IdbKeyRangePrototype(engine, realm, this);
        InitializeInterface(0);
    }
    protected override void Initialize() { base.Initialize(); CreateProperties_Generated(); }

    [JsFunction(Name = "only", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbKeyRange Only(JsValue thisObject, JsCallArguments args)
    {
        IndexedDbErrors.Arity(_realm, args, 1);
        var key = IndexedDbKeyConverter.Require(_engine, _realm, args[0]);
        return new JsIdbKeyRange(_engine, _realm, key, key, false, false);
    }
    [JsFunction(Name = "lowerBound", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbKeyRange LowerBound(JsValue thisObject, JsCallArguments args)
    {
        IndexedDbErrors.Arity(_realm, args, 1);
        var key = IndexedDbKeyConverter.Require(_engine, _realm, args[0]);
        return new JsIdbKeyRange(_engine, _realm, key, null, TypeConverter.ToBoolean(args.At(1)), true);
    }
    [JsFunction(Name = "upperBound", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbKeyRange UpperBound(JsValue thisObject, JsCallArguments args)
    {
        IndexedDbErrors.Arity(_realm, args, 1);
        var key = IndexedDbKeyConverter.Require(_engine, _realm, args[0]);
        return new JsIdbKeyRange(_engine, _realm, null, key, true, TypeConverter.ToBoolean(args.At(1)));
    }
    [JsFunction(Name = "bound", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbKeyRange Bound(JsValue thisObject, JsCallArguments args)
    {
        IndexedDbErrors.Arity(_realm, args, 2);
        var lower = IndexedDbKeyConverter.Require(_engine, _realm, args[0]);
        var upper = IndexedDbKeyConverter.Require(_engine, _realm, args[1]);
        var lowerOpen = TypeConverter.ToBoolean(args.At(2));
        var upperOpen = TypeConverter.ToBoolean(args.At(3));
        var comparison = lower.CompareTo(upper);
        if (comparison > 0 || (comparison == 0 && (lowerOpen || upperOpen)))
        {
            IndexedDbErrors.Throw(_realm, "DataError", "The range is empty.");
        }
        return new JsIdbKeyRange(_engine, _realm, lower, upper, lowerOpen, upperOpen);
    }
}

internal sealed class JsIdbKeyRange : ObjectInstance
{
    private readonly Realm _realm;
    private JsValue? _lower;
    private JsValue? _upper;
    internal JsIdbKeyRange(Engine engine, Realm realm, IndexedDbKey? lower, IndexedDbKey? upper, bool lowerOpen, bool upperOpen)
        : base(engine)
    {
        _realm = realm;
        Lower = lower;
        Upper = upper;
        LowerOpen = lowerOpen;
        UpperOpen = upperOpen;
        _prototype = realm.Intrinsics.IndexedDb.Prototype("IDBKeyRange");
    }
    internal IndexedDbKey? Lower { get; }
    internal IndexedDbKey? Upper { get; }
    internal bool LowerOpen { get; }
    internal bool UpperOpen { get; }
    internal JsValue LowerValue => _lower ??= Lower is null ? Undefined : IndexedDbKeyConverter.ToValue(_engine, _realm, Lower);
    internal JsValue UpperValue => _upper ??= Upper is null ? Undefined : IndexedDbKeyConverter.ToValue(_engine, _realm, Upper);
    internal bool Contains(IndexedDbKey key)
    {
        if (Lower is not null)
        {
            var order = key.CompareTo(Lower);
            if (order < 0 || (order == 0 && LowerOpen)) return false;
        }
        if (Upper is not null)
        {
            var order = key.CompareTo(Upper);
            if (order > 0 || (order == 0 && UpperOpen)) return false;
        }
        return true;
    }
    internal static JsIdbKeyRange? Convert(Engine engine, Realm realm, JsValue thisObject, bool required)
    {
        if (thisObject is JsIdbKeyRange range) return range;
        if (!required && thisObject.IsNullOrUndefined()) return null;
        var key = IndexedDbKeyConverter.Require(engine, realm, thisObject);
        return new JsIdbKeyRange(engine, realm, key, key, false, false);
    }
}

/// <summary>https://w3c.github.io/IndexedDB/#keyrange-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbKeyRangePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBKeyRange");
    internal IdbKeyRangePrototype(Engine engine, Realm realm, IndexedDbConstructor constructor) : base(engine, realm)
    { _constructor = constructor; _prototype = realm.Intrinsics.Object.PrototypeObject; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    private JsIdbKeyRange Brand(JsValue thisObject) => IndexedDbErrors.Brand<JsIdbKeyRange>(_realm, thisObject);

    [JsAccessor("lower", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue LowerGet(JsValue thisObject) => Brand(thisObject).LowerValue;
    [JsAccessor("upper", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue UpperGet(JsValue thisObject) => Brand(thisObject).UpperValue;
    [JsAccessor("lowerOpen", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean LowerOpenGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).LowerOpen);
    [JsAccessor("upperOpen", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean UpperOpenGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).UpperOpen);
    [JsFunction(Name = "includes", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsBoolean Includes(JsValue thisObject, JsCallArguments args)
    {
        var range = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        return JsBoolean.Create(range.Contains(IndexedDbKeyConverter.Require(_engine, _realm, args[0])));
    }
}
#endif
