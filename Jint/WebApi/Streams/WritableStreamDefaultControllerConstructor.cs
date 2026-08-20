#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>WritableStreamDefaultController</c> interface object, which declares no constructor operation and
/// is therefore not constructible.
/// <para>
/// https://streams.spec.whatwg.org/#ws-default-controller-class
/// </para>
/// </summary>
internal sealed class WritableStreamDefaultControllerConstructor : Constructor
{
    private static readonly JsString _functionName = new("WritableStreamDefaultController");

    internal WritableStreamDefaultControllerConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new WritableStreamDefaultControllerPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal WritableStreamDefaultControllerPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
