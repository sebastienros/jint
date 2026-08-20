#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Encoding;

/// <summary>
/// The <c>TextEncoder</c> interface object.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textencoder
/// </para>
/// </summary>
/// <remarks>
/// <c>constructor()</c> — it takes no arguments, because the encoder is UTF-8 and only UTF-8:
/// "the encoding is always UTF-8", https://encoding.spec.whatwg.org/#dom-textencoder. Called without
/// <c>new</c> it raises a <c>TypeError</c>, which <see cref="Constructor"/> already does for every
/// interface object shape.
/// </remarks>
internal sealed class TextEncoderConstructor : Constructor
{
    private static readonly JsString _functionName = new("TextEncoder");

    internal TextEncoderConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new TextEncoderPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal TextEncoderPrototype PrototypeObject { get; }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textencoder
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.TextEncoder.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsTextEncoder(engine));
    }
}
#endif
