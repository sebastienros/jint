#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://w3c.github.io/IndexedDB/#events.</summary>
internal sealed class IdbVersionChangeEventConstructor : IndexedDbConstructor
{
    internal IdbVersionChangeEventConstructor(Engine engine, Realm realm, ObjectInstance parent)
        : base(engine, realm, "IDBVersionChangeEvent", parent, deferPrototype: true)
    {
        PrototypeObject = new IdbVersionChangeEventPrototype(engine, realm, this);
        InitializeInterface(1);
    }
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = EventConstructor.RequireType(_realm, arguments, "IDBVersionChangeEvent");
        var init = EventConstructor.ReadEventInit(_realm, arguments.At(1), "IDBVersionChangeEvent");
        var options = IndexedDbErrors.Dictionary(_realm, arguments.At(1));
        var next = options?.Get("newVersion") ?? Null;
        var newVersion = next.IsNullOrUndefined() ? (double?) null : ProgressEventConstructor.ToUnsignedLongLong(next);
        var old = options?.Get("oldVersion") ?? Undefined;
        var oldVersion = old.IsUndefined() ? 0 : ProgressEventConstructor.ToUnsignedLongLong(old);
        return OrdinaryCreateFromConstructor(newTarget, static intrinsics => intrinsics.IndexedDb.Prototype("IDBVersionChangeEvent"),
            static (Engine engine, Realm _, (JsString Type, EventInit Init, double Time, double Old, double? New) state)
                => new JsIdbVersionChangeEvent(engine, state.Type, state.Init, state.Time, state.Old, state.New),
            (type, init, EventConstructor.TimeStampNow(_engine), oldVersion, newVersion));
    }
}

internal sealed class JsIdbVersionChangeEvent(Engine engine, JsString type, EventInit init, double timestamp, double oldVersion, double? newVersion)
    : JsEvent(engine, type, init, timestamp)
{
    internal double OldVersion { get; } = oldVersion;
    internal double? NewVersion { get; } = newVersion;
    internal static JsIdbVersionChangeEvent Trusted(Engine engine, Realm realm, string type, double oldVersion, double? newVersion)
        => new(engine, JsString.Create(type), default, EventConstructor.TimeStampNow(engine), oldVersion, newVersion)
        {
            IsTrusted = true,
            _prototype = realm.Intrinsics.IndexedDb.Prototype("IDBVersionChangeEvent"),
        };
}

/// <summary>https://w3c.github.io/IndexedDB/#events.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbVersionChangeEventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBVersionChangeEvent");
    internal IdbVersionChangeEventPrototype(Engine engine, Realm realm, IndexedDbConstructor constructor) : base(engine, realm)
    { _constructor = constructor; _prototype = realm.Intrinsics.Event.PrototypeObject; }
    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
    [JsAccessor("oldVersion", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber OldVersionGet(JsValue thisObject) => JsNumber.Create(IndexedDbErrors.Brand<JsIdbVersionChangeEvent>(_realm, thisObject).OldVersion);
    [JsAccessor("newVersion", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue NewVersionGet(JsValue thisObject) => IndexedDbErrors.Brand<JsIdbVersionChangeEvent>(_realm, thisObject).NewVersion is { } version
        ? JsNumber.Create(version) : Null;
}
#endif
