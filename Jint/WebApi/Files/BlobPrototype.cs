#if NET8_0_OR_GREATER
using SystemEncoding = System.Text.Encoding;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Files;

/// <summary>
/// <c>Blob.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/FileAPI/#dfn-Blob
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>size</c> and <c>type</c> are accessors here rather than own properties of the instance, as WebIDL
/// specifies attributes; each brand-checks its receiver and raises a <c>TypeError</c> for anything that is
/// not a <c>Blob</c> — including <c>Blob.prototype</c> itself, which is not one.
/// </para>
/// <para>
/// <c>text()</c>, <c>arrayBuffer()</c> and <c>bytes()</c> each answer an already-resolved promise — the
/// bytes are in memory, so there is nothing for an event-loop turn to wait for. They are still real
/// promises: <c>await blob.text()</c> works exactly as it does in a browser, and the value arrives on the
/// microtask turn a <c>then</c> would give it. The other two read methods answer a <c>ReadableStream</c>
/// over those same bytes instead of a promise: <c>stream()</c> a byte stream, and <c>textStream()</c> that
/// byte stream piped through a UTF-8 <c>TextDecoderStream</c>, so its chunks are strings.
/// </para>
/// <para>
/// One documented simplification against WebIDL: the operations are non-enumerable, where a WebIDL
/// interface prototype object's operations are enumerable. That is how every built-in Jint has ever shipped
/// declares its prototype methods, and it is observable only to code inspecting property attributes.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class BlobPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly BlobConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString BlobToStringTag = new("Blob");

    internal BlobPrototype(
        Engine engine,
        Realm realm,
        BlobConstructor constructor,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-size
    /// </summary>
    [JsAccessor("size", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber SizeGet(JsValue thisObject)
    {
        return JsNumber.Create(Brand(thisObject).Data.Length);
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-type
    /// </summary>
    [JsAccessor("type", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString TypeGet(JsValue thisObject)
    {
        return JsString.Create(Brand(thisObject).MediaType);
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#slice-method-algo, which delegates to the slice blob algorithm,
    /// https://w3c.github.io/FileAPI/#slice-blob.
    /// </summary>
    /// <remarks>
    /// The result is always a <c>Blob</c>, never a <c>File</c>: the algorithm says "a new Blob object",
    /// and a slice of a file has no name to carry. The bytes are shared with the receiver rather than
    /// copied, which a blob's immutability makes safe.
    /// </remarks>
    [JsFunction(Name = "slice", Length = 0)]
    private JsBlob Slice(JsValue thisObject, JsValue start, JsValue end, JsValue contentType)
    {
        var blob = Brand(thisObject);
        var originalSize = blob.Data.Length;

        // Every optional argument here has no default value, so an explicitly passed `undefined` means the
        // argument is missing — `blob.slice(0, undefined)` slices to the end, it does not slice to zero.
        var relativeStart = start.IsUndefined()
            ? 0
            : Relative(FileApi.ToClampedLongLong(start), originalSize);

        var relativeEnd = end.IsUndefined()
            ? originalSize
            : Relative(FileApi.ToClampedLongLong(end), originalSize);

        var relativeContentType = contentType.IsUndefined()
            ? string.Empty
            : FileApi.NormalizeMediaType(TypeConverter.ToString(contentType));

        var span = System.Math.Max(relativeEnd - relativeStart, 0);

        return new JsBlob(_engine, blob.Data.Slice(relativeStart, span), relativeContentType)
        {
            _prototype = _realm.Intrinsics.Blob.PrototypeObject,
        };
    }

    /// <summary>
    /// The normalization both the start and the end parameter get: a negative value counts back from the
    /// end, and either direction clamps into <c>[0, originalSize]</c>.
    /// </summary>
    private static int Relative(long value, int originalSize)
    {
        if (value < 0)
        {
            return (int) System.Math.Max(originalSize + value, 0);
        }

        return (int) System.Math.Min(value, originalSize);
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#text-method-algo
    /// </summary>
    /// <remarks>
    /// The fulfillment value is the UTF-8 decode of the bytes, which is BOM-stripping and lenient: an
    /// ill-formed sequence becomes U+FFFD rather than rejecting — https://encoding.spec.whatwg.org/#utf-8-decode.
    /// </remarks>
    [JsFunction(Name = "text", Length = 0)]
    private JsValue Text(JsValue thisObject)
    {
        var data = Brand(thisObject).Data.Span;

        // UTF-8 decode: strip one leading BOM, then decode without it.
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            data = data.Slice(3);
        }

        return Resolved(JsString.Create(SystemEncoding.UTF8.GetString(data)));
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#arraybuffer-method-algo
    /// </summary>
    [JsFunction(Name = "arrayBuffer", Length = 0)]
    private JsValue ArrayBuffer(JsValue thisObject)
    {
        return Resolved(NewArrayBuffer(Brand(thisObject)));
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dom-blob-stream, which is the "get stream" algorithm,
    /// https://w3c.github.io/FileAPI/#blob-get-stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stream is "set up with byte reading support" as the algorithm says — a readable <i>byte</i>
    /// stream — so <c>getReader({ mode: "byob" })</c> works on it and a consumer can read a blob into a
    /// buffer it recycles.
    /// </para>
    /// <para>
    /// One reduction against the algorithm as written: the blob's bytes are one chunk rather than the
    /// implementation-defined slices the algorithm's loop reads. The bytes are already in memory, so
    /// slicing them would manufacture event-loop turns rather than model any I/O — and it is only a
    /// <i>default</i> reader that can tell, since a BYOB read is bounded by the caller's own buffer either
    /// way. An empty blob enqueues nothing and closes, so its first <c>read()</c> is done.
    /// </para>
    /// <para>
    /// The bytes are copied on the way out, like every other way a blob's bytes cross into script: a blob is
    /// immutable and what script is handed is writable.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "stream", Length = 0)]
    private Streams.JsReadableStream Stream(JsValue thisObject)
    {
        return Streams.ByteStreams.CreateFromBytes(_engine, _realm, Brand(thisObject).Data);
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dom-blob-textstream — four steps over pieces that already exist: get
    /// the blob's stream, make a <c>TextDecoderStream</c> in this realm, set it up with UTF-8, and return
    /// the source piped through it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always UTF-8.</b> The interface takes no encoding argument and the blob's own <c>type</c> is never
    /// consulted, so a <c>charset</c> parameter on it — valid, invalid, or contradicting the bytes — changes
    /// nothing. That is the specification's own note, and the difference from <c>FileReader.readAsText()</c>,
    /// which does read the charset. Being an ordinary UTF-8 <c>TextDecoderStream</c> also fixes the rest of
    /// the behaviour: one leading BOM is dropped, an ill-formed sequence becomes U+FFFD rather than failing,
    /// and a sequence split across two chunks decodes to what the whole byte sequence would have.
    /// </para>
    /// <para>
    /// <b>What comes back is an ordinary <c>ReadableStream</c> of strings.</b> The byte-stream machinery
    /// behind <see cref="Stream"/> is only the <i>source</i> here — the transform's readable side is a
    /// default-controller stream whose chunks are strings, so <c>getReader({ mode: "byob" })</c> on it is
    /// the <c>TypeError</c> it is on any non-byte stream. And <c>[NewObject]</c>: every call builds a fresh
    /// source, a fresh decoder and a fresh result, so reading one cannot disturb another.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "textStream", Length = 0)]
    private Streams.JsReadableStream TextStream(JsValue thisObject)
    {
        // Step 1, which is "get stream" on this — the same algorithm stream() is, so the two cannot drift.
        var stream = Stream(thisObject);

        // Steps 2 and 3: a new TextDecoderStream in this's relevant realm, set up with UTF-8. The decoder
        // itself never reaches script; only the readable side of its transform does.
        var decoder = Encoding.JsTextDecoderStream.SetUpUtf8(_engine, _realm);

        // Step 4.
        return Streams.ReadableStreamPipe.PipeThrough(stream, decoder.Transform);
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#bytes-method-algo
    /// </summary>
    [JsFunction(Name = "bytes", Length = 0)]
    private JsValue Bytes(JsValue thisObject)
    {
        var blob = Brand(thisObject);
        var uint8Array = _realm.Intrinsics.Uint8Array;
        var array = uint8Array.AllocateTypedArray(uint8Array, (uint) blob.Data.Length);
        TypedArrayConstructor.FillTypedArrayInstance(array, blob.Data.Span);
        return Resolved(array);
    }

    /// <summary>
    /// A blob's bytes always cross into script through a fresh <c>ArrayBuffer</c>: script may write to what
    /// it is handed, and a blob is immutable.
    /// </summary>
    private JsArrayBuffer NewArrayBuffer(JsBlob blob)
    {
        return new JsArrayBuffer(_engine, blob.Data.ToArray())
        {
            _prototype = _realm.Intrinsics.ArrayBuffer.PrototypeObject,
        };
    }

    /// <summary>
    /// A promise that is already fulfilled with <paramref name="value"/>. Every read method returns one:
    /// the data is in memory, so nothing is waiting on the event loop, but the method still has to answer
    /// a real promise a script can <c>await</c> or chain.
    /// </summary>
    private JsValue Resolved(JsValue value)
    {
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        capability.Resolve(value);
        return capability.PromiseInstance;
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsBlob Brand(JsValue thisObject)
    {
        if (thisObject is JsBlob blob)
        {
            return blob;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a Blob");
        return null!;
    }
}
#endif
