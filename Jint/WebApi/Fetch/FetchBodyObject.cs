#if NET8_0_OR_GREATER
using SystemEncoding = System.Text.Encoding;
using System.Buffers;
using Jint.Native;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.WebApi.Files;
using Jint.WebApi.Streams;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The state the <c>Body</c> mixin gives both <c>Request</c> and <c>Response</c>:
/// https://fetch.spec.whatwg.org/#body-mixin.
/// </summary>
/// <remarks>
/// <para>
/// The standard's body is a record of a <c>ReadableStream</c> and an optional <i>source</i> — the bytes the
/// stream was built from, kept so that a body can be re-extracted. Both halves are here, and which one is
/// present decides how a consumer is served:
/// </para>
/// <list type="bullet">
/// <item>
/// A body extracted from bytes — a string, a <c>Blob</c>, a <c>BufferSource</c>, <c>URLSearchParams</c> —
/// keeps its <see cref="Source"/> and creates no stream at all until something asks for
/// <see cref="Stream"/>. <c>text()</c> on such a body answers an already-resolved promise off the source, so
/// the common <c>new Response('…').text()</c> costs no stream, no reader and no event-loop turn.
/// </item>
/// <item>
/// A body that arrived as a stream — a network response, or a <c>ReadableStream</c> handed to a constructor
/// — has no source, and every consumer reads it through the standard's fully-read steps.
/// </item>
/// </list>
/// <para>
/// <see cref="HasBody"/> distinguishes both from the standard's <i>null body</i>, which is not the same as an
/// empty one: it can be consumed any number of times and never flips <c>bodyUsed</c>.
/// </para>
/// <para>
/// <c>bodyUsed</c> and "body is unusable" are the stream's <i>disturbed</i> and <i>locked</i> flags, exactly
/// as https://fetch.spec.whatwg.org/#dom-body-bodyused defines them. A body that never materialized a stream
/// has no flags to read, so <see cref="SourceDisturbed"/> stands in for the disturbance the extraction-time
/// stream would have recorded — the two are indistinguishable from script, because the only way to observe
/// the stream is to ask for it, which materializes it.
/// </para>
/// </remarks>
internal abstract class FetchBodyObject : ObjectInstance
{
    protected FetchBodyObject(Engine engine, JsHeaders headers) : base(engine, ObjectClass.Object)
    {
        Headers = headers;
    }

    /// <summary>
    /// The <c>headers</c> attribute, which is <c>[SameObject]</c> — one <c>Headers</c> object per request or
    /// response, for its whole life.
    /// </summary>
    internal JsHeaders Headers { get; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-body-source — the bytes the body was extracted from, or
    /// <see langword="null"/> when there is no body or the body only ever existed as a stream. A clone shares
    /// the array with its original, which a buffered body's immutability makes safe.
    /// </summary>
    internal ReadOnlyMemory<byte>? Source { get; private set; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-body-stream — the body's stream, or <see langword="null"/>
    /// while a buffered body has not been asked for one.
    /// </summary>
    internal JsReadableStream? Stream { get; private set; }

    /// <summary>
    /// Whether the body concept is non-null at all, which is what
    /// https://fetch.spec.whatwg.org/#body-unusable and <c>bodyUsed</c> both key on.
    /// </summary>
    internal bool HasBody { get; private set; }

    /// <summary>
    /// Stands in for the disturbance of the stream a buffered body never had to create. Only ever read while
    /// <see cref="Stream"/> is <see langword="null"/>.
    /// </summary>
    private bool SourceDisturbed { get; set; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-bodyused — "this's body is non-null and this's body's stream
    /// is disturbed".
    /// </summary>
    internal bool BodyUsed => HasBody && (Stream is not null ? Stream.Disturbed : SourceDisturbed);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#body-unusable — a non-null body whose stream is disturbed or locked.
    /// What makes a second <c>text()</c> reject and a <c>clone()</c> after one throw.
    /// </summary>
    internal bool IsUnusable => HasBody && (BodyUsed || (Stream is not null && ReadableStreamOperations.IsLocked(Stream)));

    /// <summary>Sets the body to one extracted from a byte sequence.</summary>
    internal void SetBufferedBody(ReadOnlyMemory<byte> bytes)
    {
        Source = bytes;
        Stream = null;
        SourceDisturbed = false;
        HasBody = true;
    }

    /// <summary>
    /// Sets the body to one that only exists as a stream — a network response, or a <c>ReadableStream</c>
    /// passed as a <c>BodyInit</c>.
    /// </summary>
    internal void SetStreamBody(JsReadableStream stream)
    {
        Source = null;
        Stream = stream;
        SourceDisturbed = false;
        HasBody = true;
    }

    /// <summary>
    /// Replaces the stream in place, which is what <c>clone</c> does with the first branch of its tee —
    /// https://fetch.spec.whatwg.org/#concept-body-clone step 2.
    /// </summary>
    internal void ReplaceStream(JsReadableStream stream) => Stream = stream;

    /// <summary>
    /// Records that a buffered body's source has been consumed. Never called once a stream exists, because
    /// the stream's own <c>disturbed</c> flag is then the authority.
    /// </summary>
    internal void MarkSourceDisturbed() => SourceDisturbed = true;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-body — the stream, materializing it from the buffered source
    /// on first ask. <see langword="null"/> for a null body.
    /// </summary>
    /// <remarks>
    /// A body extracted from bytes has a stream from the moment it is extracted, per the standard; deferring
    /// its creation to the first read of <c>body</c> is invisible, because asking is the only way to observe
    /// it. What the deferral buys is the buffered fast path in <see cref="FetchBody.Consume"/>: a script that
    /// only ever calls <c>text()</c> never builds a stream, a controller or a reader.
    /// </remarks>
    internal JsReadableStream? GetOrCreateStream(Realm realm)
    {
        if (!HasBody)
        {
            return null;
        }

        if (Stream is not null)
        {
            return Stream;
        }

        var bytes = Source!.Value;
        var stream = ByteStreams.CreateFromBytes(Engine, realm, bytes);

        // A source that has already been consumed hands back a stream that is disturbed to match, so
        // `r.text(); r.body.locked` and `r.bodyUsed` keep telling the same story.
        if (SourceDisturbed)
        {
            ReadableStreamOperations.Cancel(stream, Undefined);
        }

        Stream = stream;
        return stream;
    }
}

/// <summary>
/// Which representation a <c>Body</c> consumer answers with.
/// </summary>
internal enum BodyConsumeKind
{
    /// <summary><c>arrayBuffer()</c></summary>
    ArrayBuffer,

    /// <summary><c>bytes()</c></summary>
    Bytes,

    /// <summary><c>blob()</c></summary>
    Blob,

    /// <summary><c>json()</c></summary>
    Json,

    /// <summary><c>text()</c></summary>
    Text,

    /// <summary><c>formData()</c></summary>
    FormData,
}

/// <summary>
/// The <c>Body</c> mixin's algorithms: extracting a body from a <c>BodyInit</c> and consuming one.
/// <para>
/// https://fetch.spec.whatwg.org/#body-mixin
/// </para>
/// </summary>
internal static class FetchBody
{
    private const string TextPlainType = "text/plain;charset=UTF-8";
    private const string FormUrlEncodedType = "application/x-www-form-urlencoded;charset=UTF-8";
    private const string FormUrlEncodedEssence = "application/x-www-form-urlencoded";
    private const string JsonType = "application/json";

    /// <summary>
    /// One arm of the extract-a-body algorithm's result: either the bytes a source was reduced to, or the
    /// stream the source <i>is</i>.
    /// </summary>
    /// <remarks>
    /// A <c>readonly record struct</c> rather than a tuple because both constructors pass it around and one
    /// of them stores it, and because <c>Bytes</c>/<c>Stream</c>/<c>Type</c> read better than three
    /// positional items at those call sites.
    /// </remarks>
    internal readonly record struct ExtractedBody(ReadOnlyMemory<byte> Bytes, JsReadableStream? Stream, string? Type);

    /// <summary>
    /// The extract-a-body algorithm, https://fetch.spec.whatwg.org/#concept-bodyinit-extract — the bytes (or
    /// the stream) plus the <c>Content-Type</c> the source implies, which is <see langword="null"/> when it
    /// implies none.
    /// </summary>
    /// <remarks>
    /// The union arms are tried in the order the IDL declares them, which is what makes a <c>Blob</c> a blob
    /// rather than a stringified object. Every arm is here, <c>FormData</c> included.
    /// </remarks>
    internal static ExtractedBody Extract(Realm realm, JsValue body)
    {
        switch (body)
        {
            case JsBlob blob:
                return new ExtractedBody(blob.Data.ToArray(), null, blob.MediaType.Length == 0 ? null : blob.MediaType);

            case JsFormData formData:
                // "Set type to `multipart/form-data; boundary=`, followed by the multipart/form-data boundary
                // string generated by the multipart/form-data encoding algorithm" — so the boundary is
                // generated once and both the body and the header are built from that one value.
                var boundary = MultipartFormData.GenerateBoundary();
                return new ExtractedBody(MultipartFormData.Serialize(formData.Entries, boundary), null, MultipartFormData.ContentTypePrefix + boundary);

            case JsUrlSearchParams parameters:
                return new ExtractedBody(SystemEncoding.UTF8.GetBytes(FormUrlEncoded.Serialize(parameters.List)), null, FormUrlEncodedType);

            case JsReadableStream stream:
                // "If object is disturbed or locked, then throw a TypeError" — the stream becomes this
                // body's stream, so a stream something else is already reading cannot also be one.
                if (stream.Disturbed || ReadableStreamOperations.IsLocked(stream))
                {
                    Throw.TypeError(realm, "The provided ReadableStream is disturbed or locked");
                }

                // A stream implies no Content-Type: the standard's ReadableStream arm sets no type.
                return new ExtractedBody(default, stream, null);
        }

        if (FileApi.TryGetBufferSourceBytes(body, out var buffered))
        {
            // "Set source to a copy of the bytes held by object" — the buffer stays script-writable, and a
            // request body that changed under the engine after it was set would be a request-smuggling
            // primitive rather than a curiosity.
            return new ExtractedBody(buffered.ToArray(), null, null);
        }

        // The USVString arm: every unpaired surrogate becomes U+FFFD, which is what UTF8.GetBytes does.
        return new ExtractedBody(SystemEncoding.UTF8.GetBytes(UrlValues.ToUsvString(body)), null, TextPlainType);
    }

    /// <summary>
    /// The <c>Content-Type</c> a JSON body carries, for <c>Response.json(data, init)</c>.
    /// </summary>
    internal const string JsonContentType = JsonType;

    /// <summary>
    /// The tail of the two constructors' body handling: store the extracted body, and append the implied
    /// <c>Content-Type</c> unless the header list already carries one — steps 39 and 40 of
    /// https://fetch.spec.whatwg.org/#dom-request and step 6 of
    /// https://fetch.spec.whatwg.org/#initialize-a-response.
    /// </summary>
    internal static void SetBody(FetchBodyObject target, in ExtractedBody body)
    {
        if (body.Stream is { } stream)
        {
            target.SetStreamBody(stream);
        }
        else
        {
            target.SetBufferedBody(body.Bytes);
        }

        if (body.Type is { } type && !target.Headers.List.Contains("content-type"))
        {
            target.Headers.List.Append("content-type", type);
        }
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-body-clone — "tee body's stream, keep the first branch and give
    /// the second to the clone".
    /// </summary>
    /// <remarks>
    /// A body that has not materialized a stream is cloned by <i>sharing its source</i> instead, which is
    /// indistinguishable: neither object has a stream to tee, and each carries its own disturbance. Tee is
    /// used the moment a stream exists — for a network response, or for a body a script has already asked
    /// <c>body</c> for — because from then on the two objects' streams have to be different objects with
    /// independent queues.
    /// <para>
    /// The tee goes through <see cref="ReadableStreamOperations.Tee"/> rather than straight to the default
    /// algorithm, because https://streams.spec.whatwg.org/#readable-stream-tee dispatches on the kind of
    /// controller the stream has: a body's own stream is a byte stream, and its two branches have to be byte
    /// streams too or a clone would quietly lose BYOB reading.
    /// </para>
    /// </remarks>
    internal static void CloneBody(FetchBodyObject source, FetchBodyObject target)
    {
        if (!source.HasBody)
        {
            return;
        }

        if (source.Stream is not { } stream)
        {
            target.SetBufferedBody(source.Source!.Value);
            return;
        }

        var (branch1, branch2) = ReadableStreamOperations.Tee(stream);
        source.ReplaceStream(branch1);
        target.SetStreamBody(branch2);
    }

    /// <summary>
    /// The consume-body algorithm, https://fetch.spec.whatwg.org/#concept-body-consume-body — always a
    /// promise, and never a synchronous throw: an unusable body is a <i>rejection</i> with a <c>TypeError</c>,
    /// which is the whole reason a <c>fetch</c> chain can be written without a <c>try</c>.
    /// </summary>
    internal static JsValue Consume(Engine engine, Realm realm, FetchBodyObject target, BodyConsumeKind kind)
    {
        var capability = PromiseConstructor.NewPromiseCapability(engine, realm.Intrinsics.Promise);

        if (target.IsUnusable)
        {
            capability.Reject(realm.Intrinsics.TypeError.Construct("Body has already been consumed"));
            return capability.PromiseInstance;
        }

        // A null body is not disturbed by being read, so bodyUsed stays false and a second read is fine.
        if (!target.HasBody)
        {
            Settle(engine, realm, target, kind, default, capability);
            return capability.PromiseInstance;
        }

        if (target.Stream is null)
        {
            // The buffered fast path: the bytes are here, so nothing has to wait for an event-loop turn.
            target.MarkSourceDisturbed();
            Settle(engine, realm, target, kind, target.Source!.Value.Span, capability);
            return capability.PromiseInstance;
        }

        // https://fetch.spec.whatwg.org/#concept-body-fully-read — get a reader and read all bytes.
        FullyRead(
            engine,
            realm,
            target.Stream!,
            bytes => Settle(engine, realm, target, kind, bytes.WrittenSpan, capability),
            capability.Reject);

        return capability.PromiseInstance;
    }

    /// <summary>
    /// Hands over a body's bytes for a consumer outside the <c>Body</c> mixin — the Cache API's "read all
    /// bytes from reader". A null body answers <see langword="null"/> synchronously; a buffered body is
    /// disturbed and answered synchronously; a stream body is fully read, disturbing it, and answers on the
    /// engine's thread when the read settles.
    /// </summary>
    internal static void ReadBodyBytes(
        Engine engine,
        Realm realm,
        FetchBodyObject body,
        Action<ReadOnlyMemory<byte>?> onBytes,
        Action<JsValue> onError)
    {
        if (!body.HasBody)
        {
            onBytes(null);
            return;
        }

        if (body.Stream is null)
        {
            body.MarkSourceDisturbed();
            onBytes(body.Source!.Value);
            return;
        }

        FullyRead(engine, realm, body.Stream!, bytes => onBytes(bytes.WrittenSpan.ToArray()), onError);
    }

    private static void Settle(
        Engine engine,
        Realm realm,
        FetchBodyObject target,
        BodyConsumeKind kind,
        ReadOnlySpan<byte> bytes,
        PromiseCapability capability)
    {
        try
        {
            capability.Resolve(Package(engine, realm, target, bytes, kind));
        }
        catch (JavaScriptException ex)
        {
            // json() on a malformed body: the parser's own SyntaxError becomes the rejection, which is what
            // the standard's "if parsing fails, reject with that exception" asks for.
            capability.Reject(ex.Error);
        }
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-body-fully-read, whose read-all-bytes half is
    /// https://streams.spec.whatwg.org/#readablestream-read-all-bytes.
    /// </summary>
    private static void FullyRead(
        Engine engine,
        Realm realm,
        JsReadableStream stream,
        Action<ArrayBufferWriter<byte>> onBytes,
        Action<JsValue> onError)
    {
        JsReadableStreamDefaultReader reader;
        try
        {
            reader = ReadableStreamOperations.AcquireDefaultReader(stream);
        }
        catch (JavaScriptException ex)
        {
            // "If acquiring a reader throws, reject promise with that exception" — the locked case, which
            // IsUnusable above has already ruled out for every caller here.
            onError(ex.Error);
            return;
        }

        new FullyReadRequest(realm, reader, onBytes, onError).Run();
    }

    /// <summary>
    /// The read request behind <see cref="FullyRead"/>: accumulate every chunk, then package the bytes.
    /// </summary>
    /// <remarks>
    /// The standard's read-loop is written as recursion — each chunk's steps read again — which for a stream
    /// whose queue is already full would recurse once per queued chunk. <see cref="Run"/> turns that into a
    /// loop with the same reentrancy pair a tee uses: a chunk delivered <i>inside</i> the read sets
    /// <c>readAgain</c> and the loop goes round, while one delivered later starts a fresh loop of its own.
    /// </remarks>
    private sealed class FullyReadRequest : ReadRequest
    {
        private readonly Realm _realm;
        private readonly JsReadableStreamDefaultReader _reader;
        private readonly Action<ArrayBufferWriter<byte>> _onBytes;
        private readonly Action<JsValue> _onError;
        private readonly ArrayBufferWriter<byte> _bytes = new();

        private bool _reading;
        private bool _readAgain;
        private bool _done;

        internal FullyReadRequest(
            Realm realm,
            JsReadableStreamDefaultReader reader,
            Action<ArrayBufferWriter<byte>> onBytes,
            Action<JsValue> onError)
        {
            _realm = realm;
            _reader = reader;
            _onBytes = onBytes;
            _onError = onError;
        }

        internal void Run()
        {
            do
            {
                _readAgain = false;
                _reading = true;
                try
                {
                    ReadableStreamOperations.DefaultReaderRead(_reader, this);
                }
                finally
                {
                    _reading = false;
                }
            }
            while (_readAgain && !_done);
        }

        internal override void ChunkSteps(JsValue chunk)
        {
            // "If chunk is not a Uint8Array object, call failureSteps with a TypeError" — a stream carrying
            // strings or plain objects is not a body, and saying so beats coercing it into one.
            if (chunk is not JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8 })
            {
                Fail(_realm.Intrinsics.TypeError.Construct("The body stream produced a chunk that is not a Uint8Array"));
                return;
            }

            if (FileApi.TryGetBufferSourceBytes(chunk, out var bytes) && !bytes.IsEmpty)
            {
                _bytes.Write(bytes);
            }

            if (_reading)
            {
                _readAgain = true;
                return;
            }

            Run();
        }

        internal override void CloseSteps()
        {
            _done = true;
            _onBytes(_bytes);
        }

        internal override void ErrorSteps(JsValue error)
        {
            _done = true;
            _onError(error);
        }

        private void Fail(JsValue error)
        {
            _done = true;

            // The standard's failure steps for a non-Uint8Array chunk release the reader's lock before
            // reporting, so the stream is left usable rather than pinned by a reader nobody holds.
            ReadableStreamOperations.DefaultReaderRelease(_reader);
            _onError(error);
        }
    }

    private static JsValue Package(Engine engine, Realm realm, FetchBodyObject target, ReadOnlySpan<byte> bytes, BodyConsumeKind kind)
    {
        switch (kind)
        {
            case BodyConsumeKind.ArrayBuffer:
                return new JsArrayBuffer(engine, bytes.ToArray())
                {
                    _prototype = realm.Intrinsics.ArrayBuffer.PrototypeObject,
                };

            case BodyConsumeKind.Bytes:
                return ByteStreams.NewUint8Array(engine, realm, bytes);

            case BodyConsumeKind.Blob:
                return new JsBlob(engine, bytes.ToArray(), BlobMimeType(target))
                {
                    _prototype = realm.Intrinsics.Blob.PrototypeObject,
                };

            case BodyConsumeKind.FormData:
                return ParseFormData(engine, realm, target, bytes);

            case BodyConsumeKind.Json:
                // The byte overload transcodes and strips one leading BOM, which is the UTF-8 decode the
                // standard runs before parsing. It decodes strictly, where the standard's decode would put
                // U+FFFD in place of an ill-formed sequence — so a body that is not well-formed UTF-8 is a
                // SyntaxError here rather than a parse of the replaced text. Both answers are a rejection
                // with a SyntaxError unless the corruption sits inside a JSON string literal.
                return new JsonParser(engine).Parse(bytes);

            default:
                return JsString.Create(Utf8Decode(bytes));
        }
    }

    /// <summary>
    /// The <c>formData()</c> steps of https://fetch.spec.whatwg.org/#dom-body-formdata: the body's own MIME
    /// type chooses the parser, and anything else — including a body with no <c>Content-Type</c> at all — is a
    /// <c>TypeError</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MIME type is the parse of the <c>Content-Type</c> header <see cref="HeaderList.Get"/> answers with,
    /// rather than the full https://fetch.spec.whatwg.org/#concept-header-extract-mime-type, which reconciles
    /// several <c>Content-Type</c> headers and carries a <c>charset</c> forward between them. The two agree
    /// whenever the list holds one such header, which is the only shape a conforming response has; where they
    /// differ this one is the more forgiving, because a boundary containing a comma survives it.
    /// </para>
    /// <para>
    /// Every failure is one <c>TypeError</c> — the standard's "if that fails for some reason, then throw a
    /// TypeError" — which <see cref="Consume"/> turns into a rejection.
    /// </para>
    /// </remarks>
    private static JsFormData ParseFormData(Engine engine, Realm realm, FetchBodyObject target, ReadOnlySpan<byte> bytes)
    {
        var contentType = target.Headers.List.Get("content-type");
        var mimeType = contentType is null ? null : MimeType.Parse(contentType);

        if (mimeType is not null && string.Equals(mimeType.Essence, MultipartFormData.Essence, StringComparison.Ordinal))
        {
            var boundary = mimeType.GetParameter("boundary");
            var parts = boundary is null ? null : MultipartFormData.Parse(bytes, boundary);
            if (parts is null)
            {
                Throw.TypeError(realm, "Failed to parse body as multipart/form-data");
            }

            return BuildFormData(engine, realm, parts);
        }

        if (mimeType is not null && string.Equals(mimeType.Essence, FormUrlEncodedEssence, StringComparison.Ordinal))
        {
            return BuildFormData(engine, realm, FormUrlEncoded.Parse(bytes.ToArray()));
        }

        Throw.TypeError(realm, "Failed to parse body as FormData: the Content-Type is neither multipart/form-data nor application/x-www-form-urlencoded");
        return null!;
    }

    /// <summary>
    /// "Return a new FormData object, appending each entry, resulting from the parsing operation, to its entry
    /// list."
    /// </summary>
    /// <remarks>
    /// A part carrying a <c>filename</c> becomes a <c>File</c> whatever its <c>Content-Type</c> says, and one
    /// carrying none becomes a string "regardless of the presence or the value of a <c>Content-Type</c>
    /// header" — the filename parameter alone decides. A file's <c>lastModified</c> is the moment it was
    /// parsed, read from the engine's own clock, because a <c>multipart/form-data</c> part carries no
    /// modification time to recover.
    /// </remarks>
    private static JsFormData BuildFormData(Engine engine, Realm realm, List<MultipartPart> parts)
    {
        var formData = NewFormData(engine, realm);

        foreach (var part in parts)
        {
            JsValue value;
            if (part.FileName is null)
            {
                value = JsString.Create(MultipartFormData.Utf8DecodeWithoutBom(part.Bytes));
            }
            else
            {
                var type = FileApi.NormalizeMediaType(part.ContentType ?? MultipartFormData.DefaultPartContentType);
                value = new JsFile(engine, part.Bytes, type, part.FileName, FileConstructor.Now(engine))
                {
                    _prototype = realm.Intrinsics.File.PrototypeObject,
                };
            }

            formData.Entries.Add(new FormDataEntry(part.Name, value));
        }

        return formData;
    }

    /// <summary>
    /// The <c>application/x-www-form-urlencoded</c> arm: "Return a new FormData object whose entry list is
    /// entries" — every value a string, since the format cannot carry a file.
    /// </summary>
    private static JsFormData BuildFormData(Engine engine, Realm realm, List<FormUrlEncodedEntry> entries)
    {
        var formData = NewFormData(engine, realm);

        foreach (var entry in entries)
        {
            formData.Entries.Add(new FormDataEntry(entry.Name, JsString.Create(entry.Value)));
        }

        return formData;
    }

    private static JsFormData NewFormData(Engine engine, Realm realm) => new(engine)
    {
        _prototype = realm.Intrinsics.FormData.PrototypeObject,
    };

    /// <summary>
    /// UTF-8 decode, https://encoding.spec.whatwg.org/#utf-8-decode: strip one leading BOM, then decode
    /// leniently — an ill-formed sequence becomes U+FFFD rather than raising.
    /// </summary>
    private static string Utf8Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            bytes = bytes.Slice(3);
        }

        return SystemEncoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// The MIME type a <c>blob()</c> gives its result, https://fetch.spec.whatwg.org/#concept-body-mime-type.
    /// </summary>
    /// <remarks>
    /// The standard parses the <c>Content-Type</c> as a MIME type and serializes it back, which lowercases the
    /// essence and normalizes the parameters; this runs the File API's own media-type normalization instead —
    /// ASCII-lowercase, and the empty string for anything carrying a non-printable character. The two agree on
    /// every well-formed value; a malformed one differs in that the standard would answer the empty string
    /// where this keeps the lowercased text.
    /// </remarks>
    private static string BlobMimeType(FetchBodyObject target)
    {
        var contentType = target.Headers.List.Get("content-type");
        return contentType is null ? string.Empty : FileApi.NormalizeMediaType(contentType);
    }
}
#endif
