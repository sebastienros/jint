#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Encoding;

/// <summary>
/// A <c>TextEncoder</c> instance.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textencoder
/// </para>
/// </summary>
/// <remarks>
/// It carries no state at all: <c>TextEncoder</c> encodes UTF-8 and nothing else, and neither
/// <c>encode</c> nor <c>encodeInto</c> streams, so there is no pending code unit to remember between
/// calls. The type exists so the prototype has a brand to check — a plain object handed to
/// <c>TextEncoder.prototype.encode</c> must raise a <c>TypeError</c>, which it cannot if every object
/// looks like an encoder.
/// </remarks>
internal sealed class JsTextEncoder : ObjectInstance
{
    internal JsTextEncoder(Engine engine) : base(engine, ObjectClass.Object)
    {
    }
}
#endif
