#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ReadableStreamBYOBRequest</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#rs-byob-request-class
/// </para>
/// </summary>
/// <remarks>
/// It declares no constructor operation: a BYOB request only ever comes from
/// <c>controller.byobRequest</c>. Like the controllers it is not installed as a global, and is reached
/// through <c>Object.getPrototypeOf(controller.byobRequest).constructor</c>.
/// </remarks>
internal sealed class ReadableStreamBYOBRequestConstructor : Constructor
{
    private static readonly JsString _functionName = new("ReadableStreamBYOBRequest");

    internal ReadableStreamBYOBRequestConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ReadableStreamBYOBRequestPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ReadableStreamBYOBRequestPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
