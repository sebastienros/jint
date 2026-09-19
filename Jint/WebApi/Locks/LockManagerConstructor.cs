#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Locks;

/// <summary>
/// The <c>LockManager</c> interface object.
/// <para>
/// https://w3c.github.io/web-locks/#lockmanager-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The IDL declares no constructor operation, so the interface object exists and is a function but refuses
/// to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. It is where <c>request</c> and
/// <c>query</c> actually live, which is what makes <c>Reflect.ownKeys(navigator.locks)</c> the empty array
/// and <c>navigator.locks instanceof LockManager</c> answerable.
/// </para>
/// <para>
/// <b>The interface is <c>[SecureContext]</c> and this engine has no contexts to be secure</b>, so the
/// declaration decides nothing here: <c>navigator.locks</c>, <c>LockManager</c> and <c>Lock</c> exist
/// exactly when <c>WebApiFeatures.WebLocks</c> is on. An embedded engine has no origin, no transport and no
/// mixed content to protect a lock space from; what it has instead is a host that decides, in one line,
/// which engines share a <see cref="Jint.WebApi.LockManager"/> at all.
/// </para>
/// </remarks>
internal sealed class LockManagerConstructor : Constructor
{
    private static readonly JsString _functionName = new("LockManager");

    internal LockManagerConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new LockManagerPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal LockManagerPrototype PrototypeObject { get; }

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
