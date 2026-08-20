#if NET8_0_OR_GREATER
using Jint.Native.Object;

namespace Jint.WebApi.Encoding;

/// <summary>
/// A <c>TextDecoder</c> instance.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textdecoder
/// </para>
/// </summary>
/// <remarks>
/// All of its state is the <c>TextDecoderCommon</c> mixin's, so it lives in
/// <see cref="TextDecoderCommon"/> — the same object a <c>TextDecoderStream</c> carries. What is left here
/// is the platform object itself, which exists so the prototype has a brand to check: a plain object handed
/// to <c>TextDecoder.prototype.decode</c> must raise a <c>TypeError</c>, which it cannot if every object
/// looks like a decoder.
/// </remarks>
internal sealed class JsTextDecoder : ObjectInstance
{
    internal JsTextDecoder(Engine engine, TextDecoderCommon common) : base(engine, ObjectClass.Object)
    {
        Common = common;
    }

    /// <summary>https://encoding.spec.whatwg.org/#textdecodercommon</summary>
    internal TextDecoderCommon Common { get; }
}
#endif
