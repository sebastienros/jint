#if NET8_0_OR_GREATER
using Jint.Native.Object;

namespace Jint.WebApi.Abort;

/// <summary>
/// An <c>AbortController</c> instance: a handle that owns one <c>AbortSignal</c> and can abort it.
/// <para>
/// https://dom.spec.whatwg.org/#interface-abortcontroller
/// </para>
/// </summary>
/// <remarks>
/// The signal is <c>[SameObject]</c> in the IDL, so the same instance is handed back on every read — it is a
/// CLR field here and an accessor on the prototype, which is what makes that true without an own property.
/// </remarks>
internal sealed class JsAbortController : ObjectInstance
{
    internal JsAbortController(Engine engine, JsAbortSignal signal) : base(engine, ObjectClass.Object)
    {
        Signal = signal;
    }

    /// <summary>https://dom.spec.whatwg.org/#abortcontroller-signal.</summary>
    internal JsAbortSignal Signal { get; }
}
#endif
