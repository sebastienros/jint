#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.IndexedDb;

/// <summary>Realm-local interface objects: https://w3c.github.io/IndexedDB/#database-api.</summary>
internal sealed class IndexedDbInterfaces(Engine engine, Realm realm)
{
    internal static readonly string[] Names =
    [
        "IDBFactory", "IDBRequest", "IDBOpenDBRequest", "IDBDatabase", "IDBTransaction",
        "IDBObjectStore", "IDBIndex", "IDBCursor", "IDBCursorWithValue", "IDBKeyRange", "IDBVersionChangeEvent",
    ];

    private readonly Dictionary<string, IndexedDbConstructor> _interfaces = new(StringComparer.Ordinal);
    private JsIdbFactory? _factory;
    internal JsIdbFactory Factory => _factory ??= new JsIdbFactory(engine, realm);

    internal IndexedDbConstructor Get(string name)
    {
        if (_interfaces.TryGetValue(name, out var value)) return value;
        var parent = name switch
        {
            "IDBRequest" or "IDBDatabase" or "IDBTransaction" => realm.Intrinsics.EventTarget,
            "IDBOpenDBRequest" => Get("IDBRequest"),
            "IDBCursorWithValue" => Get("IDBCursor"),
            "IDBVersionChangeEvent" => realm.Intrinsics.Event,
            _ => (ObjectInstance) realm.Intrinsics.Function.PrototypeObject,
        };
        value = name switch
        {
            "IDBKeyRange" => new IdbKeyRangeConstructor(engine, realm),
            "IDBVersionChangeEvent" => new IdbVersionChangeEventConstructor(engine, realm, parent),
            _ => new IndexedDbConstructor(engine, realm, name, parent),
        };
        _interfaces.Add(name, value);
        return value;
    }

    internal ObjectInstance Prototype(string name) => Get(name).PrototypeObject;
}

/// <summary>An interface without a public constructor: https://w3c.github.io/IndexedDB/#database-api.</summary>
internal class IndexedDbConstructor : Constructor
{
    internal IndexedDbConstructor(Engine engine, Realm realm, string name, ObjectInstance parent, bool deferPrototype = false)
        : base(engine, realm, new JsString(name))
    {
        _prototype = parent;
        if (deferPrototype) return;
        var objectPrototype = realm.Intrinsics.Object.PrototypeObject;
        PrototypeObject = name switch
        {
            "IDBFactory" => new IdbFactoryPrototype(engine, realm, this, objectPrototype),
            "IDBRequest" => new IdbRequestPrototype(engine, realm, this, realm.Intrinsics.EventTarget.PrototypeObject),
            "IDBOpenDBRequest" => new IdbOpenDbRequestPrototype(engine, realm, this, realm.Intrinsics.IndexedDb.Prototype("IDBRequest")),
            "IDBDatabase" => new IdbDatabasePrototype(engine, realm, this, realm.Intrinsics.EventTarget.PrototypeObject),
            "IDBTransaction" => new IdbTransactionPrototype(engine, realm, this, realm.Intrinsics.EventTarget.PrototypeObject),
            "IDBObjectStore" => new IdbObjectStorePrototype(engine, realm, this, objectPrototype),
            "IDBIndex" => new IdbIndexPrototype(engine, realm, this, objectPrototype),
            "IDBCursor" => new IdbCursorPrototype(engine, realm, this, objectPrototype),
            "IDBCursorWithValue" => new IdbCursorWithValuePrototype(engine, realm, this, realm.Intrinsics.IndexedDb.Prototype("IDBCursor")),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        InitializeInterface(0);
    }

    internal ObjectInstance PrototypeObject { get; private protected set; } = null!;

    private protected void InitializeInterface(int length)
    {
        _length = new PropertyDescriptor(JsNumber.Create(length), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
