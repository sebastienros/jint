#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Caches;

/// <summary>
/// The <c>CacheStorage</c> interface object.
/// <para>
/// https://w3c.github.io/ServiceWorker/#cachestorage-interface
/// </para>
/// </summary>
/// <remarks>
/// Like <c>Cache</c>, the interface declares no constructor operation, so the interface object exists and is a
/// function but refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. The one
/// instance is the <c>caches</c> global, which is what
/// <c>caches instanceof CacheStorage</c> is written against.
/// </remarks>
internal sealed class CacheStorageConstructor : Constructor
{
    private static readonly JsString _functionName = new("CacheStorage");

    internal CacheStorageConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new CacheStoragePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal CacheStoragePrototype PrototypeObject { get; }

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
