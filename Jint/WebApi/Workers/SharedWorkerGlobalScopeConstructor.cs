#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Workers;

/// <summary>The non-constructible shared worker global interface.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/workers.html#shared-workers-and-the-sharedworkerglobalscope-interface</remarks>
internal sealed class SharedWorkerGlobalScopeConstructor : Constructor
{
    internal SharedWorkerGlobalScopeConstructor(Engine engine, Realm realm)
        : base(engine, realm, new JsString("SharedWorkerGlobalScope"))
    {
        _prototype = realm.Intrinsics.WorkerGlobalScope;
        PrototypeObject = new SharedWorkerGlobalScopePrototype(engine, realm, this);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ObjectInstance PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}

[JsObject(UseShape = true)]
internal sealed partial class SharedWorkerGlobalScopePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly SharedWorkerGlobalScopeConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString SharedWorkerGlobalScopeToStringTag = new("SharedWorkerGlobalScope");

    internal SharedWorkerGlobalScopePrototype(Engine engine, Realm realm, SharedWorkerGlobalScopeConstructor constructor)
        : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = realm.Intrinsics.WorkerGlobalScope.PrototypeObject;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }
}
#endif
