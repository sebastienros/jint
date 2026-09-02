#if NET8_0_OR_GREATER
using Jint.Native;

namespace Jint.WebApi.Events;

/// <summary>
/// A <c>ProgressEvent</c> instance — how much of a transfer has happened, and whether the total is known.
/// <para>
/// https://xhr.spec.whatwg.org/#interface-progressevent
/// </para>
/// </summary>
/// <remarks>
/// The interface is declared by the XHR standard but is not about <c>XMLHttpRequest</c>: HTML fires one at a
/// media element, the File API at a <c>FileReader</c>, and workers at a <c>Worker</c>. It lives here beside
/// <see cref="JsEvent"/> rather than under <c>Xhr/</c> so that the next feature to need it finds it, and it is
/// installed by whichever feature flag brings the first interface that fires one.
/// </remarks>
internal sealed class JsProgressEvent : JsEvent
{
    internal JsProgressEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        bool lengthComputable,
        double loaded,
        double total)
        : base(engine, type, init, timeStamp)
    {
        LengthComputable = lengthComputable;
        Loaded = loaded;
        Total = total;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-progressevent-lengthcomputable — whether <see cref="Total"/> is a
    /// number the sender actually declared.
    /// </summary>
    internal bool LengthComputable { get; }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-progressevent-loaded. Declared <c>unsigned long long</c>, so it is
    /// carried as a <see cref="double"/> — every value a transfer can reach is exactly representable.
    /// </summary>
    internal double Loaded { get; }

    /// <summary>https://xhr.spec.whatwg.org/#dom-progressevent-total.</summary>
    internal double Total { get; }
}
#endif
