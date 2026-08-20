#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ReadableStreamDefaultController</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#rs-default-controller-class
/// </para>
/// </summary>
/// <remarks>
/// The interface declares no constructor operation, which in WebIDL means the interface object exists and is
/// a function but refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. A
/// controller can only come from a stream handing one to its underlying source. Like the other
/// non-constructed stream interfaces it is not installed as a global, and is reached through
/// <c>Object.getPrototypeOf(controller).constructor</c>.
/// </remarks>
internal sealed class ReadableStreamDefaultControllerConstructor : Constructor
{
    private static readonly JsString _functionName = new("ReadableStreamDefaultController");

    internal ReadableStreamDefaultControllerConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ReadableStreamDefaultControllerPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ReadableStreamDefaultControllerPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
