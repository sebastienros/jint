#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Locks;

/// <summary>
/// The <c>Lock</c> interface object.
/// <para>
/// https://w3c.github.io/web-locks/#lock-class
/// </para>
/// </summary>
/// <remarks>
/// The IDL declares no constructor operation, so the interface object exists and is a function but refuses
/// to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. It is exposed for the reason
/// every other such object is: a script holding what a callback was handed asks
/// <c>lock instanceof Lock</c>, and <c>'Lock' in self</c> is how the API is feature-detected.
/// </remarks>
internal sealed class LockConstructor : Constructor
{
    private static readonly JsString _functionName = new("Lock");

    internal LockConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new LockPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal LockPrototype PrototypeObject { get; }

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
