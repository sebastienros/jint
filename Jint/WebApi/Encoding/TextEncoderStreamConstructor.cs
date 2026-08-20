#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Encoding;

/// <summary>
/// The <c>TextEncoderStream</c> interface object.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textencoderstream
/// </para>
/// </summary>
/// <remarks>
/// <c>constructor()</c> takes no arguments at all: like <c>TextEncoder</c>, the interface encodes UTF-8 and
/// nothing else, so there is no label to give it.
/// </remarks>
internal sealed class TextEncoderStreamConstructor : Constructor
{
    private static readonly JsString _functionName = new("TextEncoderStream");

    internal TextEncoderStreamConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new TextEncoderStreamPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal TextEncoderStreamPrototype PrototypeObject { get; }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textencoderstream
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var stream = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.TextEncoderStream.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsTextEncoderStream(engine, realm));

        // The algorithms close over the instance, so the transform can only be set up once it exists.
        stream.Transform = TransformStreamOperations.SetUp(
            _engine,
            _realm,
            stream.EncodeAndEnqueue,
            stream.EncodeAndFlush);

        return stream;
    }
}
#endif
