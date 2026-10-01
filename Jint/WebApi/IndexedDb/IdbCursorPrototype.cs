#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://w3c.github.io/IndexedDB/#cursor-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbCursorPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBCursor");
    internal IdbCursorPrototype(Engine engine, Realm realm, IndexedDbConstructor constructor, ObjectInstance parent) : base(engine, realm)
    { _constructor = constructor; _prototype = parent; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    private JsIdbCursor Brand(JsValue thisObject) => IndexedDbErrors.Brand<JsIdbCursor>(_realm, thisObject);

    [JsAccessor("source", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue SourceGet(JsValue thisObject) => Brand(thisObject).Source;
    [JsAccessor("direction", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString DirectionGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Direction);
    [JsAccessor("key", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue KeyGet(JsValue thisObject) => Brand(thisObject).Key;
    [JsAccessor("primaryKey", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue PrimaryKeyGet(JsValue thisObject) => Brand(thisObject).PrimaryKey;
    [JsAccessor("request", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsIdbRequest RequestGet(JsValue thisObject) => Brand(thisObject).Request;

    [JsFunction(Name = "advance", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Advance(JsValue thisObject, JsCallArguments args)
    {
        var cursor = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        var count = (uint) IndexedDbErrors.EnforceRange(_realm, args[0], uint.MaxValue);
        if (count == 0) Throw.TypeError(_realm, "The count must be greater than zero.");
        cursor.Continue(count, null, null);
        return Undefined;
    }
    [JsFunction(Name = "continue", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Continue(JsValue thisObject, JsValue key)
    {
        var cursor = Brand(thisObject);
        cursor.RequireValue();
        cursor.Continue(1, key.IsUndefined() ? null : IndexedDbKeyConverter.Require(_engine, _realm, key), null);
        return Undefined;
    }
    [JsFunction(Name = "continuePrimaryKey", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ContinuePrimaryKey(JsValue thisObject, JsCallArguments args)
    {
        var cursor = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 2);
        cursor.RequireValue();
        var key = IndexedDbKeyConverter.Require(_engine, _realm, args[0]);
        var primary = IndexedDbKeyConverter.Require(_engine, _realm, args[1]);
        cursor.Continue(1, key, primary, primaryOperation: true);
        return Undefined;
    }
    [JsFunction(Name = "update", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest Update(JsValue thisObject, JsCallArguments args)
    {
        var cursor = Brand(thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        return cursor.Update(args[0]);
    }
    [JsFunction(Name = "delete", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbRequest DeleteRecord(JsValue thisObject) => Brand(thisObject).Delete();
}

/// <summary>https://w3c.github.io/IndexedDB/#idbcursorwithvalue.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbCursorWithValuePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBCursorWithValue");
    internal IdbCursorWithValuePrototype(Engine engine, Realm realm, IndexedDbConstructor constructor, ObjectInstance parent) : base(engine, realm)
    { _constructor = constructor; _prototype = parent; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    [JsAccessor("value", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ValueGet(JsValue thisObject)
    {
        var cursor = IndexedDbErrors.Brand<JsIdbCursor>(_realm, thisObject);
        if (cursor.KeysOnly) Throw.TypeError(_realm, "Illegal invocation");
        return cursor.Value;
    }
}
#endif
