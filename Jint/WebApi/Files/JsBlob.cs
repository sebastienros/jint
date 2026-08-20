#if NET8_0_OR_GREATER
using Jint.Native.Object;

namespace Jint.WebApi.Files;

/// <summary>
/// A <c>Blob</c> instance: an immutable byte sequence plus the media type it claims to be.
/// <para>
/// https://w3c.github.io/FileAPI/#dfn-Blob
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>size</c> and <c>type</c> are WebIDL attributes, so they live in CLR state here and
/// <see cref="BlobPrototype"/> reads them through a brand check rather than being own properties of the
/// instance — <c>Object.getOwnPropertyNames(new Blob())</c> is empty, as in a browser.
/// </para>
/// <para>
/// The bytes are held as a <see cref="ReadOnlyMemory{T}"/> and never handed out by reference: a blob is
/// immutable by specification, so <c>slice</c> takes a window over the very same array instead of copying,
/// while <c>arrayBuffer</c> and <c>bytes</c> — which hand the bytes to script, where they are mutable —
/// always copy.
/// </para>
/// </remarks>
internal class JsBlob : ObjectInstance
{
    internal JsBlob(Engine engine, ReadOnlyMemory<byte> data, string type) : base(engine, ObjectClass.Object)
    {
        Data = data;
        MediaType = type;
    }

    /// <summary>The blob's associated byte sequence, https://w3c.github.io/FileAPI/#byte-sequence.</summary>
    internal ReadOnlyMemory<byte> Data { get; }

    /// <summary>The normalized media type, or the empty string.</summary>
    internal string MediaType { get; }
}
#endif
