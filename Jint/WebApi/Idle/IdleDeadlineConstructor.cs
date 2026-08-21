#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Idle;

/// <summary>
/// The <c>IdleDeadline</c> interface object.
/// <para>
/// https://w3c.github.io/requestidlecallback/#idledeadline
/// </para>
/// </summary>
/// <remarks>
/// The interface declares no constructor operation, which in WebIDL means the interface object exists and is a
/// function but refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. A deadline
/// only ever comes from the engine, as the argument to an idle callback. The object is exposed all the same,
/// because that is what makes <c>deadline instanceof IdleDeadline</c> and feature detection work.
/// </remarks>
internal sealed class IdleDeadlineConstructor : Constructor
{
    private static readonly JsString _functionName = new("IdleDeadline");

    internal IdleDeadlineConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new IdleDeadlinePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal IdleDeadlinePrototype PrototypeObject { get; }

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
