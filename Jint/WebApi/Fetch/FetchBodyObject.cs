#if NET8_0_OR_GREATER
using SystemEncoding = System.Text.Encoding;
using Jint.Native;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.WebApi.Files;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The state the <c>Body</c> mixin gives both <c>Request</c> and <c>Response</c>:
/// https://fetch.spec.whatwg.org/#body-mixin.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bodies are buffered, not streamed.</b> The standard's body is a <c>ReadableStream</c>, which Jint does
/// not have; a body here is a byte array that already exists in full, which is why every consumer answers an
/// already-resolved promise and why <c>body</c> is always <see langword="null"/> (see
/// <c>RequestPrototype.BodyGet</c>). What the standard's <i>disturbed</i> flag buys is kept exactly:
/// <see cref="BodyUsed"/> flips on the first consume and every later one rejects, so a script that reads a
/// response twice fails the way it would in a browser rather than silently succeeding here and failing there.
/// </para>
/// <para>
/// A <see langword="null"/> <see cref="Body"/> is the standard's null body, which is not the same as an empty
/// one: it can be consumed any number of times and never flips <see cref="BodyUsed"/>.
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
    /// https://fetch.spec.whatwg.org/#concept-body — the bytes, or <see langword="null"/> for a null body.
    /// A clone shares the array with its original, which a buffered body's immutability makes safe.
    /// </summary>
    internal ReadOnlyMemory<byte>? Body { get; set; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-bodyused — whether the body has been consumed. Per object, so
    /// a clone starts unused however often the original was read.
    /// </summary>
    internal bool BodyUsed { get; set; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#body-unusable — a non-null body that has already been consumed. What
    /// makes a second <c>text()</c> reject and a <c>clone()</c> after one throw.
    /// </summary>
    internal bool IsUnusable => Body is not null && BodyUsed;
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
    private const string JsonType = "application/json";

    /// <summary>
    /// The extract-a-body algorithm, https://fetch.spec.whatwg.org/#concept-bodyinit-extract — the bytes plus
    /// the <c>Content-Type</c> the source implies, which is <see langword="null"/> when it implies none.
    /// </summary>
    /// <remarks>
    /// The union arms are tried in the order the IDL declares them, which is what makes a <c>Blob</c> a blob
    /// rather than a stringified object. <c>ReadableStream</c> is absent, and a <c>FormData</c> is refused
    /// rather than silently stringified — see <see cref="ThrowFormDataUnsupported"/>.
    /// </remarks>
    internal static (ReadOnlyMemory<byte> Bytes, string? Type) Extract(Realm realm, JsValue body)
    {
        switch (body)
        {
            case JsBlob blob:
                return (blob.Data.ToArray(), blob.MediaType.Length == 0 ? null : blob.MediaType);

            case JsFormData:
                ThrowFormDataUnsupported(realm);
                return default;

            case JsUrlSearchParams parameters:
                return (SystemEncoding.UTF8.GetBytes(FormUrlEncoded.Serialize(parameters.List)), FormUrlEncodedType);
        }

        if (FileApi.TryGetBufferSourceBytes(body, out var buffered))
        {
            // "Set source to a copy of the bytes held by object" — the buffer stays script-writable, and a
            // request body that changed under the engine after it was set would be a request-smuggling
            // primitive rather than a curiosity.
            return (buffered.ToArray(), null);
        }

        // The USVString arm: every unpaired surrogate becomes U+FFFD, which is what UTF8.GetBytes does.
        return (SystemEncoding.UTF8.GetBytes(UrlValues.ToUsvString(body)), TextPlainType);
    }

    /// <summary>
    /// The <c>Content-Type</c> a JSON body carries, for <c>Response.json(data, init)</c>.
    /// </summary>
    internal const string JsonContentType = JsonType;

    /// <summary>
    /// The tail of the two constructors' body handling: store the extracted bytes, and append the implied
    /// <c>Content-Type</c> unless the header list already carries one — steps 39 and 40 of
    /// https://fetch.spec.whatwg.org/#dom-request and step 6 of
    /// https://fetch.spec.whatwg.org/#initialize-a-response.
    /// </summary>
    internal static void SetBody(FetchBodyObject target, ReadOnlyMemory<byte> bytes, string? type)
    {
        target.Body = bytes;

        if (type is not null && !target.Headers.List.Contains("content-type"))
        {
            target.Headers.List.Append("content-type", type);
        }
    }

    /// <summary>
    /// The <c>FormData</c> arm of <c>BodyInit</c>, which needs a <c>multipart/form-data</c> serializer Jint
    /// does not have yet. A <c>TypeError</c> naming the gap beats writing a body that is not what the script
    /// asked for.
    /// </summary>
    internal static void ThrowFormDataUnsupported(Realm realm)
    {
        Throw.TypeError(realm, "A FormData body is not supported yet: Jint has no multipart/form-data serializer. Send a string, a Blob, a BufferSource or a URLSearchParams instead.");
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
        var bytes = target.Body ?? default;
        if (target.Body is not null)
        {
            target.BodyUsed = true;
        }

        try
        {
            capability.Resolve(Package(engine, realm, target, bytes.Span, kind));
        }
        catch (JavaScriptException ex)
        {
            // json() on a malformed body: the parser's own SyntaxError becomes the rejection, which is what
            // the standard's "if parsing fails, reject with that exception" asks for.
            capability.Reject(ex.Error);
        }

        return capability.PromiseInstance;
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
                var uint8Array = realm.Intrinsics.Uint8Array;
                var array = uint8Array.AllocateTypedArray(uint8Array, (uint) bytes.Length);
                TypedArrayConstructor.FillTypedArrayInstance(array, bytes);
                return array;

            case BodyConsumeKind.Blob:
                return new JsBlob(engine, bytes.ToArray(), MimeType(target))
                {
                    _prototype = realm.Intrinsics.Blob.PrototypeObject,
                };

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
    private static string MimeType(FetchBodyObject target)
    {
        var contentType = target.Headers.List.Get("content-type");
        return contentType is null ? string.Empty : FileApi.NormalizeMediaType(contentType);
    }
}
#endif
