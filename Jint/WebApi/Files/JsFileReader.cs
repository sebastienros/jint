#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.WebApi.Files;

/// <summary>
/// Which of the four <c>readAs*</c> methods a read operation was started by, which is the only thing that
/// differs between them once the bytes are in hand.
/// <para>
/// https://w3c.github.io/FileAPI/#dfn-package-data
/// </para>
/// </summary>
internal enum FileReadType
{
    /// <summary>https://w3c.github.io/FileAPI/#dfn-readAsArrayBuffer.</summary>
    ArrayBuffer,

    /// <summary>https://w3c.github.io/FileAPI/#dfn-readAsBinaryString.</summary>
    BinaryString,

    /// <summary>https://w3c.github.io/FileAPI/#dfn-readAsText.</summary>
    Text,

    /// <summary>https://w3c.github.io/FileAPI/#dfn-readAsDataURL.</summary>
    DataUrl,
}

/// <summary>
/// A <c>FileReader</c> instance: a state, a result, an error, and whichever read is in flight.
/// <para>
/// https://w3c.github.io/FileAPI/#dfn-filereader
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every attribute of the interface is read-only, so the whole state lives in CLR fields here and
/// <see cref="FileReaderPrototype"/> reads it through a brand check — an instance therefore has no own
/// property at all, which is what a browser reports for
/// <c>Object.getOwnPropertyNames(new FileReader())</c>.
/// </para>
/// <para>
/// <b>There is no thread.</b> A <c>Blob</c> here is a byte sequence already in memory, so the specification's
/// "in parallel" loop over the blob's stream has nothing to wait for; what survives of it is the *task*
/// structure, which is observable and is the whole reason the events arrive in the order they do. See
/// <see cref="FileReadOperation"/>.
/// </para>
/// </remarks>
internal sealed class JsFileReader : JsEventTarget
{
    /// <summary>https://w3c.github.io/FileAPI/#dom-filereader-empty.</summary>
    internal const int Empty = 0;

    /// <summary>https://w3c.github.io/FileAPI/#dom-filereader-loading.</summary>
    internal const int Loading = 1;

    /// <summary>https://w3c.github.io/FileAPI/#dom-filereader-done.</summary>
    internal const int Done = 2;

    /// <summary>https://w3c.github.io/FileAPI/#event-loadstart.</summary>
    internal static readonly JsString LoadStartEventType = new("loadstart");

    /// <summary>https://w3c.github.io/FileAPI/#event-progress.</summary>
    internal static readonly JsString ProgressEventType = new("progress");

    /// <summary>https://w3c.github.io/FileAPI/#event-load.</summary>
    internal static readonly JsString LoadEventType = new("load");

    /// <summary>https://w3c.github.io/FileAPI/#event-abort.</summary>
    internal static readonly JsString AbortEventType = new("abort");

    /// <summary>https://w3c.github.io/FileAPI/#event-error.</summary>
    internal static readonly JsString ErrorEventType = new("error");

    /// <summary>https://w3c.github.io/FileAPI/#event-loadend.</summary>
    internal static readonly JsString LoadEndEventType = new("loadend");

    internal JsFileReader(Engine engine, Realm realm) : base(engine, realm)
    {
    }

    /// <summary>https://w3c.github.io/FileAPI/#dom-filereader-readystate.</summary>
    internal int ReadyState { get; set; } = Empty;

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dom-filereader-result — <c>null</c> until a read completes, and
    /// <c>null</c> again from the moment the next one starts.
    /// </summary>
    internal JsValue Result { get; set; } = JsValue.Null;

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dom-filereader-error — the <c>DOMException</c> a failed read left
    /// behind, or <c>null</c>.
    /// </summary>
    internal JsValue Error { get; set; } = JsValue.Null;

    /// <summary>
    /// The read in flight, or <see langword="null"/>. It is what makes <c>abort()</c>'s "remove those tasks
    /// from that task queue" step implementable without reaching into the event loop: a job checks whether it
    /// is still this reader's current operation and returns if it is not, so aborting or starting a second
    /// read leaves the queued jobs inert.
    /// </summary>
    internal FileReadOperation? Current { get; set; }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#concept-event-fire-progress — create a <c>ProgressEvent</c>,
    /// initialize its three members, and dispatch it. The event is trusted because the engine created it.
    /// </summary>
    /// <remarks>
    /// The File API fires every one of its six events "with total set to <i>total</i> and loaded set to
    /// <i>loaded</i>" for a blob whose size is known, so <c>lengthComputable</c> is true whenever the blob is
    /// non-empty — the same condition <c>XMLHttpRequest</c> uses, and for the same reason.
    /// </remarks>
    internal void FireProgressEvent(JsString type, double loaded, double total)
    {
        var ev = _realm.Intrinsics.ProgressEvent.CreateTrustedProgressEvent(
            type,
            lengthComputable: total != 0,
            loaded,
            total);

        DispatchEvent(ev);
    }
}
#endif
