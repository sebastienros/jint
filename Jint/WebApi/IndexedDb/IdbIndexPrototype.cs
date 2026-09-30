#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://w3c.github.io/IndexedDB/#index-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbIndexPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBIndex");
    internal IdbIndexPrototype(Engine engine, Realm realm, IndexedDbConstructor constructor, ObjectInstance parent) : base(engine, realm)
    { _constructor = constructor; _prototype = parent; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    private JsIdbIndex Brand(JsValue thisObject) => IndexedDbErrors.Brand<JsIdbIndex>(_realm, thisObject);

    [JsAccessor("name", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString NameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Data.Name);
    [JsAccessor("name", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue NameSet(JsValue thisObject, JsValue name) { Brand(thisObject).Rename(TypeConverter.ToString(name)); return Undefined; }
    [JsAccessor("objectStore", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsIdbObjectStore ObjectStoreGet(JsValue thisObject) => Brand(thisObject).Store;
    [JsAccessor("keyPath", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue KeyPathGet(JsValue thisObject) => Brand(thisObject).KeyPath;
    [JsAccessor("multiEntry", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean MultiEntryGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Data.MultiEntry);
    [JsAccessor("unique", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean UniqueGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Data.Unique);

    [JsFunction(Name = "get", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest Get(JsValue thisObject, JsCallArguments args) => Query(thisObject, args, "get", required: true);
    [JsFunction(Name = "getKey", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest GetKey(JsValue thisObject, JsCallArguments args) => Query(thisObject, args, "getKey", required: true);
    [JsFunction(Name = "getAll", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest GetAll(JsValue thisObject, JsCallArguments args) => Query(thisObject, args, "getAll", required: false);
    [JsFunction(Name = "getAllKeys", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest GetAllKeys(JsValue thisObject, JsCallArguments args) => Query(thisObject, args, "getAllKeys", required: false);
    [JsFunction(Name = "count", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest Count(JsValue thisObject, JsCallArguments args) => Query(thisObject, args, "count", required: false);
    private JsIdbRequest Query(JsValue thisObject, JsCallArguments args, string operation, bool required)
    {
        var index = Brand(thisObject);
        if (required) IndexedDbErrors.Arity(_realm, args, 1);
        var count = operation is "getAll" or "getAllKeys" && !args.At(1).IsUndefined()
            ? (uint) IndexedDbErrors.EnforceRange(_realm, args[1], uint.MaxValue) : 0;
        index.RequireData();
        return index.Store.Query(index, JsIdbKeyRange.Convert(_engine, _realm, args.At(0), required), operation, count);
    }
    [JsFunction(Name = "openCursor", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest OpenCursor(JsValue thisObject, JsValue query, JsValue direction) => Cursor(thisObject, query, direction, keys: false);
    [JsFunction(Name = "openKeyCursor", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest OpenKeyCursor(JsValue thisObject, JsValue query, JsValue direction) => Cursor(thisObject, query, direction, keys: true);
    private JsIdbRequest Cursor(JsValue thisObject, JsValue query, JsValue direction, bool keys)
    {
        var index = Brand(thisObject);
        var converted = IndexedDbErrors.Direction(_realm, direction);
        index.RequireData();
        return index.Store.OpenCursor(index, JsIdbKeyRange.Convert(_engine, _realm, query, required: false), converted, keys);
    }
}
#endif
