#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Encoding;

/// <summary>
/// The <c>TextDecoder</c> interface object.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textdecoder
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>constructor(optional DOMString label = "utf-8", optional TextDecoderOptions options = {})</c>. The
/// two arguments are converted by <see cref="TextDecoderCommon.Create"/>, which is also what
/// <c>TextDecoderStream</c>'s identical constructor uses.
/// </para>
/// <para>
/// Called without <c>new</c> it raises a <c>TypeError</c>, which <see cref="Constructor"/> already does
/// for every interface object shape.
/// </para>
/// </remarks>
internal sealed class TextDecoderConstructor : Constructor
{
    private static readonly JsString _functionName = new("TextDecoder");

    internal TextDecoderConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new TextDecoderPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal TextDecoderPrototype PrototypeObject { get; }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textdecoder
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var common = TextDecoderCommon.Create(_realm, arguments.At(0), arguments.At(1), "TextDecoder");

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.TextDecoder.PrototypeObject,
            static (Engine engine, Realm _, TextDecoderCommon? state) => new JsTextDecoder(engine, state!),
            common);
    }
}
#endif
