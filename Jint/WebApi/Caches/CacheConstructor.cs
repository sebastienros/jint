#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Caches;

/// <summary>
/// The <c>Cache</c> interface object.
/// <para>
/// https://w3c.github.io/ServiceWorker/#cache-interface
/// </para>
/// </summary>
/// <remarks>
/// The interface declares no constructor operation, which in WebIDL means the interface object exists and is
/// a function but refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. A
/// <c>Cache</c> comes from <c>caches.open(name)</c> and from nowhere else, which is what keeps every cache a
/// script can reach one the host's provider knows about.
/// </remarks>
internal sealed class CacheConstructor : Constructor
{
    private static readonly JsString _functionName = new("Cache");

    internal CacheConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new CachePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal CachePrototype PrototypeObject { get; }

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }

    /// <summary>
    /// Builds the <c>Cache</c> object <c>caches.open</c> hands out — a new one per call, as the standard
    /// says, over whichever store the provider answered with.
    /// </summary>
    internal JsCache CreateInstance(string name, CacheStore store)
        => new(_engine, name, store) { _prototype = PrototypeObject };
}
#endif
