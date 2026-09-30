#if NET8_0_OR_GREATER
using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://w3c.github.io/IndexedDB/#database-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbDatabasePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBDatabase");
    internal IdbDatabasePrototype(Engine engine, Realm realm, IndexedDbConstructor constructor, ObjectInstance parent) : base(engine, realm)
    { _constructor = constructor; _prototype = parent; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    private JsIdbDatabase Brand(JsValue thisObject) => IndexedDbErrors.Brand<JsIdbDatabase>(_realm, thisObject);

    [JsAccessor("name", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString NameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Name);
    [JsAccessor("version", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber VersionGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).Version);
    [JsAccessor("objectStoreNames", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsDomStringList ObjectStoreNamesGet(JsValue thisObject) => new JsDomStringList(_engine, _realm, Brand(thisObject).Names());

    [JsFunction(Name = "createObjectStore", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbObjectStore CreateObjectStore(JsValue thisObject, JsCallArguments args)
    {
        var database = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        var name = TypeConverter.ToString(args[0]);
        var options = IndexedDbErrors.Dictionary(_realm, args.At(1));
        var auto = TypeConverter.ToBoolean(options?.Get("autoIncrement") ?? Undefined);
        var pathValue = options?.Get("keyPath") ?? Null;
        var path = pathValue.IsNullOrUndefined() ? null : IndexedDbKeyPath.Read(_engine, _realm, pathValue);
        return database.CreateStore(name, path, auto);
    }

    [JsFunction(Name = "deleteObjectStore", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue DeleteObjectStore(JsValue thisObject, JsCallArguments args)
    {
        var database = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        database.DeleteStore(TypeConverter.ToString(args[0]));
        return Undefined;
    }

    [JsFunction(Name = "transaction", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbTransaction Transaction(JsValue thisObject, JsCallArguments args)
    {
        var database = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        var names = ReadNames(args[0]);
        var mode = args.At(1).IsUndefined() ? "readonly" : TypeConverter.ToString(args[1]);
        if (mode is not ("readonly" or "readwrite")) Throw.TypeError(_realm, "Invalid transaction mode.");
        var options = IndexedDbErrors.Dictionary(_realm, args.At(2));
        var durabilityValue = options?.Get("durability") ?? Undefined;
        var durability = durabilityValue.IsUndefined() ? "default" : TypeConverter.ToString(durabilityValue);
        if (durability is not ("default" or "strict" or "relaxed")) Throw.TypeError(_realm, "Invalid durability.");
        return database.Transaction(names, mode, durability);
    }

    private string[] ReadNames(JsValue thisObject)
    {
        if (thisObject is not ObjectInstance obj || obj.GetMethod(GlobalSymbolRegistry.Iterator) is null)
        {
            return [TypeConverter.ToString(thisObject)];
        }
        var names = new List<string>();
        var iterator = thisObject.GetIterator(_realm);
        try
        {
            while (iterator.TryIteratorStepValue(out var item))
            {
                _engine.Constraints.Check();
                names.Add(TypeConverter.ToString(item));
            }
        }
        catch { iterator.Close(CompletionType.Throw); throw; }
        return names.ToArray();
    }

    [JsFunction(Name = "close", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Close(JsValue thisObject) { Brand(thisObject).Close(); return Undefined; }

    [JsAccessor("onabort", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnAbortGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "abort");
    [JsAccessor("onabort", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnAbortSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "abort", handler); return Undefined; }
    [JsAccessor("onclose", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnCloseGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "close");
    [JsAccessor("onclose", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnCloseSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "close", handler); return Undefined; }
    [JsAccessor("onerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "error");
    [JsAccessor("onerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "error", handler); return Undefined; }
    [JsAccessor("onversionchange", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnVersionChangeGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "versionchange");
    [JsAccessor("onversionchange", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnVersionChangeSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "versionchange", handler); return Undefined; }
}
#endif
