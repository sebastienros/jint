#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>TransformStreamDefaultController</c> instance.
/// <para>
/// https://streams.spec.whatwg.org/#ts-default-controller-class
/// </para>
/// </summary>
/// <remarks>
/// <see cref="FinishCapability"/> is the specification's <c>[[finishPromise]]</c>: it is unpopulated until
/// either the flush or the cancel algorithm has been invoked, and its presence is what stops the two from
/// both running — a transformer's <c>cancel()</c> is never called after its <c>flush()</c>, and neither is
/// ever called twice, however the two sides are shut down.
/// </remarks>
internal sealed class JsTransformStreamDefaultController : ObjectInstance
{
    internal JsTransformStreamDefaultController(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the controller was created in.</summary>
    internal Realm Realm { get; }

    /// <summary>https://streams.spec.whatwg.org/#transformstreamdefaultcontroller-stream</summary>
    internal JsTransformStream Stream { get; set; } = null!;

    /// <summary>https://streams.spec.whatwg.org/#transformstreamdefaultcontroller-transformalgorithm</summary>
    internal Func<JsValue, JsPromise>? TransformAlgorithm { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#transformstreamdefaultcontroller-flushalgorithm</summary>
    internal Func<JsPromise>? FlushAlgorithm { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#transformstreamdefaultcontroller-cancelalgorithm</summary>
    internal Func<JsValue, JsPromise>? CancelAlgorithm { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#transformstreamdefaultcontroller-finishpromise</summary>
    internal PromiseCapability? FinishCapability { get; set; }
}
#endif
