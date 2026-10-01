#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://w3c.github.io/IndexedDB/#transaction-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbTransactionPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBTransaction");
    internal IdbTransactionPrototype(Engine engine, Realm realm, IndexedDbConstructor constructor, ObjectInstance parent) : base(engine, realm)
    { _constructor = constructor; _prototype = parent; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    private JsIdbTransaction Brand(JsValue thisObject) => IndexedDbErrors.Brand<JsIdbTransaction>(_realm, thisObject);

    [JsAccessor("objectStoreNames", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsDomStringList NamesGet(JsValue thisObject) => new JsDomStringList(_engine, _realm, Brand(thisObject).Names());
    [JsAccessor("mode", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ModeGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Mode);
    [JsAccessor("durability", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString DurabilityGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Durability);
    [JsAccessor("db", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsIdbDatabase DatabaseGet(JsValue thisObject) => Brand(thisObject).Database;
    [JsAccessor("error", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ErrorGet(JsValue thisObject) => Brand(thisObject).Error;
    [JsFunction(Name = "objectStore", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbObjectStore ObjectStore(JsValue thisObject, JsCallArguments args)
    {
        var transaction = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        return transaction.ObjectStore(TypeConverter.ToString(args[0]));
    }
    [JsFunction(Name = "commit", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Commit(JsValue thisObject) { Brand(thisObject).Commit(); return Undefined; }
    [JsFunction(Name = "abort", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Abort(JsValue thisObject) { Brand(thisObject).Abort(); return Undefined; }
    [JsAccessor("oncomplete", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnCompleteGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "complete");
    [JsAccessor("oncomplete", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnCompleteSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "complete", handler); return Undefined; }
    [JsAccessor("onabort", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnAbortGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "abort");
    [JsAccessor("onabort", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnAbortSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "abort", handler); return Undefined; }
    [JsAccessor("onerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "error");
    [JsAccessor("onerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "error", handler); return Undefined; }
}
#endif
