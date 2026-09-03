#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.ArrayBuffer;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Encoding;
using Jint.WebApi.Fetch;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.Files;

/// <summary>
/// One <c>FileReader</c> read, from the synchronous state change to the <c>loadend</c> event three tasks
/// later.
/// <para>
/// https://w3c.github.io/FileAPI/#readOperation
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>The "in parallel" half of the algorithm has nothing to wait for, and the task half is the whole
/// observable content.</b> A <c>Blob</c> in this engine is a byte sequence already in memory, so there is no
/// stream to pull and no thread to pull it on; what the specification's loop still decides is that
/// <c>loadstart</c>, the progress events and the completion each arrive in a task of their own, which is why
/// <c>reader.readAsText(blob); reader.onloadstart = f</c> still calls <c>f</c> — the handler is assigned
/// before the first task runs. So the operation is a three-step state machine, and each step queues the next.
/// </para>
/// <para>
/// <b>One chunk, therefore one <c>progress</c> event</b>, and none at all for an empty blob. The
/// specification fires one per chunk read, throttled to roughly 50ms; an in-memory blob arrives whole, so
/// there is exactly one chunk. That is what <c>FileAPI/reading-data-section/filereader_events.any.js</c>
/// asserts in both directions — <c>loadstart, load, loadend</c> for an empty blob and
/// <c>loadstart, progress, load, loadend</c> for a non-empty one — and a second progress event would fail it.
/// </para>
/// <para>
/// <b>Each step is a task, and the engine's one <see cref="FileReadQueue"/> is what runs it</b> — including
/// the microtask checkpoint that makes an <c>await</c> between two events work at all: resuming an async
/// function after <c>await watcher.wait_for('progress')</c> takes several jobs, and without the checkpoint
/// the <c>load</c> event would arrive before the script had asked to wait for it.
/// <c>FileAPI/reading-data-section/filereader_result.any.js</c> and <c>filereader_events.any.js</c> both fail
/// the moment it is removed.
/// </para>
/// <para>
/// <b><c>abort()</c> removes the queued tasks by detaching, not by reaching into the event loop.</b> The queue
/// asks each read whether it is still its reader's current operation; an abort, or a second <c>readAs*</c>
/// started from inside a handler, replaces that reference and the old read is dropped rather than stepped.
/// </para>
/// </remarks>
internal sealed class FileReadOperation
{
    private readonly Engine _engine;
    private readonly Realm _realm;
    private readonly JsFileReader _reader;
    private readonly ReadOnlyMemory<byte> _bytes;
    private readonly FileReadType _type;
    private readonly string _blobType;
    private readonly string? _encodingName;

    /// <summary>
    /// Which of the three tasks runs next: <c>loadstart</c>, then <c>progress</c>, then the completion.
    /// </summary>
    private int _step;

    private FileReadOperation(
        Engine engine,
        Realm realm,
        JsFileReader reader,
        ReadOnlyMemory<byte> bytes,
        FileReadType type,
        string blobType,
        string? encodingName)
    {
        _engine = engine;
        _realm = realm;
        _reader = reader;
        _bytes = bytes;
        _type = type;
        _blobType = blobType;
        _encodingName = encodingName;
    }

    /// <summary>
    /// The synchronous half of https://w3c.github.io/FileAPI/#readOperation: refuse a second read, clear the
    /// state, and put the first task on the loop.
    /// </summary>
    internal static void Start(
        Engine engine,
        Realm realm,
        JsFileReader reader,
        JsBlob blob,
        FileReadType type,
        string? encodingName,
        string context)
    {
        // Step 1: "If reader's state is 'loading', throw an InvalidStateError DOMException."
        if (reader.ReadyState == JsFileReader.Loading)
        {
            var exception = realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.InvalidState,
                context + ": the reader is already loading.");

            var location = engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(engine, exception, in location);
        }

        // Steps 2 to 4.
        reader.ReadyState = JsFileReader.Loading;
        reader.Result = JsValue.Null;
        reader.Error = JsValue.Null;

        var operation = new FileReadOperation(engine, realm, reader, blob.Data, type, blob.MediaType, encodingName);
        reader.Current = operation;
        FileApi.RequireState(engine).FileReads.Enqueue(operation);
    }

    /// <summary>
    /// <c>abort()</c>'s step 3, "if there are any tasks from this on the file reading task source in an
    /// affiliated task queue, then remove those tasks", plus step 4, "terminate the algorithm for the read
    /// method being processed". Detaching does both: every step already refuses to run for an operation the
    /// reader has moved on from.
    /// </summary>
    internal static void Detach(JsFileReader reader) => reader.Current = null;

    /// <summary>
    /// Whether this read still has tasks to run. False once it has completed, and false from the moment its
    /// reader aborted it or started a second read — which is what makes the queue drop it.
    /// </summary>
    internal bool IsLive => ReferenceEquals(_reader.Current, this);

    /// <summary>
    /// The read's next task: <c>loadstart</c>, then <c>progress</c> for a blob that has bytes, then the
    /// completion. Called by <see cref="FileReadQueue"/>, once per turn.
    /// </summary>
    internal void RunStep()
    {
        switch (_step++)
        {
            case 0:
                // Step 10.2: "queue a task to fire a progress event called loadstart at fr".
                _reader.FireProgressEvent(JsFileReader.LoadStartEventType, loaded: 0, _bytes.Length);
                break;

            case 1:
                // Step 10.4.3: "queue a task to fire a progress event called progress at fr" — once, because
                // an in-memory blob is one chunk, and not at all when there are no bytes to have read.
                if (_bytes.Length > 0)
                {
                    _reader.FireProgressEvent(JsFileReader.ProgressEventType, _bytes.Length, _bytes.Length);
                }

                break;

            default:
                // Steps 10.5 and 10.5.5 in one task, which is what the algorithm says: the microtask
                // checkpoint that separates the two events is the one the dispatch performs when a listener
                // returns to an empty stack, not a task of its own. See Complete's remarks.
                Complete();
                FireLoadEnd();

                // The read is over. Clearing the reference is what lets the queue drop it, and what stops the
                // blob's bytes being held by a reader nothing is reading with any more.
                if (IsLive)
                {
                    _reader.Current = null;
                }

                break;
        }
    }

    /// <summary>
    /// Step 10.5: set the state to <c>done</c>, package the data, and fire <c>load</c> — or <c>error</c> if
    /// the packaging threw.
    /// </summary>
    /// <remarks>
    /// <b><c>load</c> and <c>loadend</c> are fired from one task, which is what the algorithm says, and the
    /// microtask checkpoint between them is the dispatch's.</b> An <c>await</c> resumed by the <c>load</c>
    /// handler has to run before <c>loadend</c> is fired, or
    /// <c>FileAPI/reading-data-section/filereader_events.any.js</c> sees <c>loadend</c> before its
    /// <c>EventWatcher</c> has been told to expect it — and that checkpoint is
    /// <c>Engine.CleanUpAfterRunningScript</c>, performed when a listener returns to an empty JavaScript
    /// execution context stack, which a step running as an event-loop job leaves it at. Until
    /// sebastienros/jint#3668 the engine had no such checkpoint and <c>loadend</c> was given a task of its
    /// own to stand in for one; the <c>abort()</c> path never needed the workaround, because there a browser
    /// has no checkpoint either — the stack is not empty, script called <c>abort()</c>.
    /// <para>
    /// The <c>error</c> arm is unreachable for a blob whose bytes are already in memory — none of the four
    /// packagings can fail, and the failure the algorithm has in mind is a stream that errored mid-read. It
    /// is written all the same, because packaging is where such a failure would arrive and a reader whose
    /// <c>error</c> was only ever <c>null</c> would be a promise this class had not made.
    /// </para>
    /// </remarks>
    private void Complete()
    {
        _reader.ReadyState = JsFileReader.Done;

        JsValue result;
        try
        {
            result = PackageData();
        }
        catch (JavaScriptException exception)
        {
            _reader.Error = exception.Error;
            _reader.FireProgressEvent(JsFileReader.ErrorEventType, _bytes.Length, _bytes.Length);
            return;
        }

        _reader.Result = result;
        _reader.FireProgressEvent(JsFileReader.LoadEventType, _bytes.Length, _bytes.Length);
    }

    /// <summary>
    /// Step 10.5.5, "if fr's state is not 'loading', fire a progress event called loadend at fr" — which is
    /// what stops a second read started from inside the <c>load</c> handler having the first read's
    /// <c>loadend</c> fire on top of it.
    /// </summary>
    private void FireLoadEnd()
    {
        if (_reader.ReadyState == JsFileReader.Loading)
        {
            return;
        }

        _reader.FireProgressEvent(JsFileReader.LoadEndEventType, _bytes.Length, _bytes.Length);
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-package-data
    /// </summary>
    private JsValue PackageData() => _type switch
    {
        FileReadType.ArrayBuffer => NewArrayBuffer(),
        FileReadType.BinaryString => JsString.Create(ToBinaryString(_bytes.Span)),
        FileReadType.Text => DecodeText(),
        _ => JsString.Create(ToDataUrl()),
    };

    /// <summary>
    /// "Return a new ArrayBuffer whose contents are bytes." A fresh buffer every time, because script may
    /// write to what it is handed and a blob is immutable.
    /// </summary>
    private JsArrayBuffer NewArrayBuffer()
        => new(_engine, _bytes.ToArray()) { _prototype = _realm.Intrinsics.ArrayBuffer.PrototypeObject };

    /// <summary>
    /// "Return bytes as a binary string, in which every byte is represented by a code unit of equal value
    /// [0..255]" — which is Latin-1, and is why <c>new Blob(["σ"])</c> reads back as two characters.
    /// </summary>
    private static string ToBinaryString(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        return string.Create(bytes.Length, bytes.ToArray(), static (chars, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                chars[i] = (char) source[i];
            }
        });
    }

    /// <summary>
    /// The <c>Text</c> arm: determine the encoding, then run the Encoding standard's <i>decode</i> over the
    /// bytes with it as the fallback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three sources are consulted in the specification's order and each is allowed to fail into the
    /// next: the <c>encoding</c> argument if it names an encoding, otherwise the <c>charset</c> parameter of
    /// the blob's own type if <i>that</i> names one, otherwise UTF-8. So
    /// <c>readAsText(blob, 'not-an-encoding')</c> falls through to the blob's charset rather than throwing,
    /// which is what "get an encoding" returning failure means.
    /// </para>
    /// <para>
    /// <b>Decoding sniffs a byte order mark, and that is not the same thing as
    /// <c>TextDecoder</c>'s BOM stripping.</b> https://encoding.spec.whatwg.org/#decode runs
    /// <i>BOM sniff</i> first and lets what it finds <i>override</i> the fallback encoding, so a blob
    /// beginning <c>FE FF</c> decodes as UTF-16BE even though the fallback was UTF-8 —
    /// <c>FileAPI/reading-data-section/Determining-Encoding.any.js</c> asserts exactly that, in both
    /// endiannesses. The decoder built for the sniffed encoding then strips the mark itself, which is why
    /// the whole input is handed to it rather than the bytes after the mark.
    /// </para>
    /// </remarks>
    private JsString DecodeText()
    {
        var encoding = ResolveTextEncoding();
        var common = new TextDecoderCommon(in encoding, fatal: false, ignoreBom: false);
        return common.Decode(_realm, _bytes.Span, stream: false);
    }

    private EncodingEntry ResolveTextEncoding()
    {
        // BOM sniff, https://encoding.spec.whatwg.org/#bom-sniff, which wins over both named sources.
        var span = _bytes.Span;
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            return EncodingLabels.Utf8Encoding;
        }

        if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF && EncodingLabels.TryLookup("utf-16be", out var utf16Be))
        {
            return utf16Be;
        }

        if (span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE && EncodingLabels.TryLookup("utf-16le", out var utf16Le))
        {
            return utf16Le;
        }

        if (_encodingName is not null && EncodingLabels.TryLookup(_encodingName, out var named))
        {
            return named;
        }

        if (MimeType.Parse(_blobType)?.GetParameter("charset") is { } charset
            && EncodingLabels.TryLookup(charset, out var fromType))
        {
            return fromType;
        }

        return EncodingLabels.Utf8Encoding;
    }

    /// <summary>
    /// "Return bytes as a DataURL [RFC2397]", using the blob's type as the media type when it has one.
    /// </summary>
    /// <remarks>
    /// RFC 2397 makes the media type optional and the File API says to leave it out when the blob has none.
    /// Every implementation writes <c>application/octet-stream</c> instead — the RFC's own default for a
    /// <c>data:</c> URL with no media type is <c>text/plain;charset=US-ASCII</c>, which would be a lie about
    /// arbitrary bytes — and <c>FileAPI/reading-data-section/filereader_readAsDataURL.any.js</c> asserts that
    /// spelling for both an untyped blob and an empty one.
    /// </remarks>
    private string ToDataUrl()
    {
        var mediaType = MimeType.Parse(_blobType)?.Serialize() ?? "application/octet-stream";
        return "data:" + mediaType + ";base64," + Convert.ToBase64String(_bytes.Span);
    }
}
#endif
