#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Encoding;

/// <summary>
/// The <c>TextDecoderStream</c> interface object.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textdecoderstream
/// </para>
/// </summary>
/// <remarks>
/// The constructor takes exactly what <c>TextDecoder</c>'s does — <c>(label, options)</c>, converted by
/// the same code — and differs only in what it builds: a transform stream whose writable side accepts
/// buffer sources and whose readable side produces the strings they decode to.
/// </remarks>
internal sealed class TextDecoderStreamConstructor : Constructor
{
    private static readonly JsString _functionName = new("TextDecoderStream");

    internal TextDecoderStreamConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new TextDecoderStreamPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal TextDecoderStreamPrototype PrototypeObject { get; }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textdecoderstream, whose steps are "set up a text decoder
    /// stream" (https://encoding.spec.whatwg.org/#set-up-a-text-decoder-stream) once the label and the
    /// options have been converted.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var common = TextDecoderCommon.Create(_realm, arguments.At(0), arguments.At(1), "TextDecoderStream");

        var stream = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.TextDecoderStream.PrototypeObject,
            static (Engine engine, Realm realm, TextDecoderCommon? state) => new JsTextDecoderStream(engine, realm, state!),
            common);

        // The algorithms close over the instance, so the transform can only be set up once it exists —
        // which is why the specification's "set up" operation takes the stream as an argument.
        stream.SetUpTransform();

        return stream;
    }
}
#endif
