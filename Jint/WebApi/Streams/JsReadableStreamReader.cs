#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ReadableStreamGenericReader</c> mixin: the state and behaviour a default reader and a BYOB reader
/// share.
/// <para>
/// https://streams.spec.whatwg.org/#generic-reader-mixin
/// </para>
/// </summary>
/// <remarks>
/// The mixin exists in the standard as a WebIDL <c>includes</c> statement rather than as an interface, so
/// nothing script-visible corresponds to this class: <c>closed</c> and <c>cancel</c> are own members of both
/// reader prototypes, and there is no shared prototype between them. It is a CLR base class purely so that
/// a stream's <c>[[reader]]</c> slot has one type, which is what lets
/// <see cref="ReadableStreamOperations.ReaderGenericRelease"/> and the closed-promise handling be written
/// once.
/// </remarks>
internal abstract class JsReadableStreamReader : ObjectInstance
{
    private protected JsReadableStreamReader(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the reader was created in, which owns its <c>closed</c> and read promises.</summary>
    internal Realm Realm { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablestreamgenericreader-stream — the stream the reader is
    /// active for, or <see langword="null"/> once its lock has been released.
    /// </summary>
    internal JsReadableStream? Stream { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamgenericreader-closedpromise</summary>
    /// <remarks>
    /// Held as a capability rather than as a bare promise because releasing a lock on a non-readable stream
    /// <i>replaces</i> it with a freshly rejected one rather than rejecting the existing one — the
    /// specification distinguishes the two, and so does this.
    /// </remarks>
    internal PromiseCapability ClosedCapability { get; set; } = null!;

    /// <summary>The promise the <c>closed</c> getter answers with.</summary>
    internal JsPromise ClosedPromise => StreamPromises.PromiseOf(ClosedCapability);
}
#endif
