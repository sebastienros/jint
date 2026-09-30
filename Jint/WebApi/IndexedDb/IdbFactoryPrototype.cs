#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.IndexedDb;

/// <summary>https://w3c.github.io/IndexedDB/#factory-interface.</summary>
[JsObject(UseShape = true)]
internal sealed partial class IdbFactoryPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IndexedDbConstructor _constructor;
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString Tag = new("IDBFactory");

    internal IdbFactoryPrototype(Engine engine, Realm realm, IndexedDbConstructor constructor, ObjectInstance parent) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = parent;
    }

    protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }

    [JsFunction(Name = "open", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbOpenDbRequest Open(JsValue thisObject, JsCallArguments args)
    {
        var factory = IndexedDbErrors.Brand<JsIdbFactory>(_realm, thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        var name = TypeConverter.ToString(args[0]);
        double? version = args.At(1).IsUndefined() ? null : IndexedDbErrors.EnforceRange(_realm, args[1], 18446744073709549568d);
        if (version == 0) Throw.TypeError(_realm, "The database version must be greater than zero.");
        return factory.Open(name, version, delete: false);
    }

    [JsFunction(Name = "deleteDatabase", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsIdbOpenDbRequest DeleteDatabase(JsValue thisObject, JsCallArguments args)
    {
        var factory = IndexedDbErrors.Brand<JsIdbFactory>(_realm, thisObject);
        IndexedDbErrors.Arity(_realm, args, 1);
        return factory.Open(TypeConverter.ToString(args[0]), null, delete: true);
    }

    [JsFunction(Name = "cmp", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsNumber Compare(JsValue thisObject, JsCallArguments args)
    {
        IndexedDbErrors.Brand<JsIdbFactory>(_realm, thisObject);
        IndexedDbErrors.Arity(_realm, args, 2);
        var first = IndexedDbKeyConverter.Require(_engine, _realm, args[0]);
        var second = IndexedDbKeyConverter.Require(_engine, _realm, args[1]);
        return JsNumber.Create(System.Math.Sign(first.CompareTo(second)));
    }

    [JsFunction(Name = "databases", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Databases(JsValue thisObject)
    {
        try { return IndexedDbErrors.Brand<JsIdbFactory>(_realm, thisObject).Databases(); }
        catch (JavaScriptException exception)
        {
            var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
            capability.Reject(exception.Error);
            return capability.PromiseInstance;
        }
    }
}
#endif
