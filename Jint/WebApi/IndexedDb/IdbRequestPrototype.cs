#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://w3c.github.io/IndexedDB/#request-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbRequestPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBRequest");

    internal IdbRequestPrototype(Engine engine, Realm realm, IndexedDbConstructor constructor, ObjectInstance parent) : base(engine, realm)
    { _constructor = constructor; _prototype = parent; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    private JsIdbRequest Brand(JsValue thisObject) => IndexedDbErrors.Brand<JsIdbRequest>(_realm, thisObject);

    [JsAccessor("result", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ResultGet(JsValue thisObject) => Brand(thisObject).ReadResult();
    [JsAccessor("error", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ErrorGet(JsValue thisObject) => Brand(thisObject).ReadError();
    [JsAccessor("source", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue SourceGet(JsValue thisObject) => Brand(thisObject).Source;
    [JsAccessor("transaction", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue TransactionGet(JsValue thisObject) => Brand(thisObject).Transaction ?? (JsValue) Null;
    [JsAccessor("readyState", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ReadyStateGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Done ? "done" : "pending");
    [JsAccessor("onsuccess", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnSuccessGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "success");
    [JsAccessor("onsuccess", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnSuccessSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "success", handler); return Undefined; }
    [JsAccessor("onerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "error");
    [JsAccessor("onerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "error", handler); return Undefined; }
}

/// <summary>https://w3c.github.io/IndexedDB/#openrequest-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbOpenDbRequestPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBOpenDBRequest");

    internal IdbOpenDbRequestPrototype(Engine engine, Realm realm, IndexedDbConstructor constructor, ObjectInstance parent) : base(engine, realm)
    { _constructor = constructor; _prototype = parent; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    private JsIdbOpenDbRequest Brand(JsValue thisObject) => IndexedDbErrors.Brand<JsIdbOpenDbRequest>(_realm, thisObject);

    [JsAccessor("onblocked", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnBlockedGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "blocked");
    [JsAccessor("onblocked", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnBlockedSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "blocked", handler); return Undefined; }
    [JsAccessor("onupgradeneeded", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnUpgradeNeededGet(JsValue thisObject) => EventHandlerAttributes.Get(Brand(thisObject), "upgradeneeded");
    [JsAccessor("onupgradeneeded", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnUpgradeNeededSet(JsValue thisObject, JsValue handler) { EventHandlerAttributes.Set(Brand(thisObject), "upgradeneeded", handler); return Undefined; }
}
#endif
