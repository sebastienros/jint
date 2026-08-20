#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ReadableByteStreamController</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#rbs-controller-class
/// </para>
/// </summary>
/// <remarks>
/// Like <c>ReadableStreamDefaultController</c> it declares no constructor operation, so the interface object
/// exists and is a function but refuses to construct anything —
/// https://webidl.spec.whatwg.org/#es-interface-call. A byte controller can only come from a stream handing
/// one to its underlying byte source. It is not installed as a global either, and is reached through
/// <c>Object.getPrototypeOf(controller).constructor</c>.
/// </remarks>
internal sealed class ReadableByteStreamControllerConstructor : Constructor
{
    private static readonly JsString _functionName = new("ReadableByteStreamController");

    internal ReadableByteStreamControllerConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ReadableByteStreamControllerPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ReadableByteStreamControllerPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
