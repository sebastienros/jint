#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Storage;

/// <summary>
/// The <c>Storage</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/webstorage.html#the-storage-interface
/// </para>
/// </summary>
/// <remarks>
/// The interface declares no constructor operation, which in WebIDL means the interface object exists and is
/// a function but refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. A
/// storage can only come from the <c>localStorage</c> and <c>sessionStorage</c> globals, which is what makes
/// the host's provider the only door to one. The object is still worth having: it carries
/// <c>Storage.prototype</c>, so <c>localStorage instanceof Storage</c> is true and a script may patch the
/// prototype the way it patches any other.
/// </remarks>
internal sealed class StorageConstructor : Constructor
{
    private static readonly JsString _functionName = new("Storage");

    internal StorageConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new StoragePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal StoragePrototype PrototypeObject { get; }

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }

    /// <summary>
    /// Builds the one storage object a global names, over the map the host supplied.
    /// </summary>
    internal JsStorage CreateStorage(StorageProvider provider) => new(_engine, _realm, provider, PrototypeObject);
}
#endif
