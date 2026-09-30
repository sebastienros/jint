#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://w3c.github.io/IndexedDB/#object-store-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbObjectStorePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBObjectStore");
    internal IdbObjectStorePrototype(Engine engine, Realm realm, IndexedDbConstructor constructor, ObjectInstance parent) : base(engine, realm)
    { _constructor = constructor; _prototype = parent; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    private JsIdbObjectStore Brand(JsValue thisObject) => IndexedDbErrors.Brand<JsIdbObjectStore>(_realm, thisObject);

    [JsAccessor("name", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString NameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Data.Name);
    [JsAccessor("name", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue NameSet(JsValue thisObject, JsValue name) { Brand(thisObject).Rename(TypeConverter.ToString(name)); return Undefined; }
    [JsAccessor("keyPath", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue KeyPathGet(JsValue thisObject) => Brand(thisObject).KeyPath;
    [JsAccessor("indexNames", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsDomStringList IndexNamesGet(JsValue thisObject) => new JsDomStringList(_engine, _realm, Brand(thisObject).Data.Indexes.Keys.Order(StringComparer.Ordinal).ToArray());
    [JsAccessor("transaction", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsIdbTransaction TransactionGet(JsValue thisObject) => Brand(thisObject).Transaction;
    [JsAccessor("autoIncrement", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean AutoIncrementGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Data.AutoIncrement);

    [JsFunction(Name = "put", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest Put(JsValue thisObject, JsCallArguments args) => Write(thisObject, args, overwrite: true);
    [JsFunction(Name = "add", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest Add(JsValue thisObject, JsCallArguments args) => Write(thisObject, args, overwrite: false);
    private JsIdbRequest Write(JsValue thisObject, JsCallArguments args, bool overwrite)
    {
        var store = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        return store.Put(args[0], args.At(1), overwrite);
    }

    [JsFunction(Name = "delete", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest Delete(JsValue thisObject, JsCallArguments args)
    {
        var store = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        store.RequireData(write: true);
        return store.Delete(JsIdbKeyRange.Convert(_engine, _realm, args[0], required: true));
    }
    [JsFunction(Name = "clear", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest Clear(JsValue thisObject) => Brand(thisObject).Clear();

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
        var store = Brand(thisObject);
        if (required) IndexedDbErrors.Arity(_realm, args, 1);
        var count = operation is "getAll" or "getAllKeys" && !args.At(1).IsUndefined()
            ? (uint) IndexedDbErrors.EnforceRange(_realm, args[1], uint.MaxValue) : 0;
        store.RequireData();
        var range = JsIdbKeyRange.Convert(_engine, _realm, args.At(0), required);
        return store.Query(null, range, operation, count);
    }

    [JsFunction(Name = "openCursor", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest OpenCursor(JsValue thisObject, JsValue query, JsValue direction) => Cursor(thisObject, query, direction, keys: false);
    [JsFunction(Name = "openKeyCursor", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest OpenKeyCursor(JsValue thisObject, JsValue query, JsValue direction) => Cursor(thisObject, query, direction, keys: true);
    private JsIdbRequest Cursor(JsValue thisObject, JsValue query, JsValue direction, bool keys)
    {
        var store = Brand(thisObject);
        var convertedDirection = IndexedDbErrors.Direction(_realm, direction);
        store.RequireData();
        return store.OpenCursor(null, JsIdbKeyRange.Convert(_engine, _realm, query, required: false), convertedDirection, keys);
    }

    [JsFunction(Name = "index", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbIndex Index(JsValue thisObject, JsCallArguments args)
    {
        var store = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        return store.Index(TypeConverter.ToString(args[0]));
    }
    [JsFunction(Name = "createIndex", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbIndex CreateIndex(JsValue thisObject, JsCallArguments args)
    {
        var store = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 2);
        var name = TypeConverter.ToString(args[0]);
        var path = IndexedDbKeyPath.Read(_engine, _realm, args[1]);
        var options = IndexedDbErrors.Dictionary(_realm, args.At(2));
        var multiEntry = TypeConverter.ToBoolean(options?.Get("multiEntry") ?? Undefined);
        var unique = TypeConverter.ToBoolean(options?.Get("unique") ?? Undefined);
        return store.CreateIndex(name, path, unique, multiEntry);
    }
    [JsFunction(Name = "deleteIndex", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue DeleteIndex(JsValue thisObject, JsCallArguments args)
    {
        var store = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        store.DeleteIndex(TypeConverter.ToString(args[0]));
        return Undefined;
    }
}
#endif
