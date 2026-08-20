#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>ReadableStreamDefaultController</c> instance.
/// <para>
/// https://streams.spec.whatwg.org/#rs-default-controller-class
/// </para>
/// </summary>
/// <remarks>
/// The three algorithm slots are nullable because <c>ReadableStreamDefaultControllerClearAlgorithms</c>
/// sets them to undefined — which the specification does the moment they can no longer be needed,
/// specifically so that a stream stops retaining the underlying source's closures once it is closed or
/// errored.
/// </remarks>
internal sealed class JsReadableStreamDefaultController : ObjectInstance
{
    internal JsReadableStreamDefaultController(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the controller was created in.</summary>
    internal Realm Realm { get; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-stream</summary>
    internal JsReadableStream Stream { get; set; } = null!;

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-queue and
    /// <c>[[queueTotalSize]]</c>, which the standard keeps synchronized as one structure.
    /// </summary>
    internal StreamQueue Queue { get; } = new();

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-started</summary>
    internal bool Started { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-closerequested</summary>
    internal bool CloseRequested { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-pullagain</summary>
    internal bool PullAgain { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-pulling</summary>
    internal bool Pulling { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-strategyhwm</summary>
    internal double StrategyHighWaterMark { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-strategysizealgorithm</summary>
    internal Func<JsValue, double>? StrategySizeAlgorithm { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-pullalgorithm</summary>
    internal Func<JsPromise>? PullAlgorithm { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultcontroller-cancelalgorithm</summary>
    internal Func<JsValue, JsPromise>? CancelAlgorithm { get; set; }
}
#endif
