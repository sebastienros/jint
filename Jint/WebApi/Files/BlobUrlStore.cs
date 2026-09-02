#if NET8_0_OR_GREATER
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Files;

/// <summary>
/// The engine's <i>blob URL store</i>: the <c>blob:</c> URLs <c>URL.createObjectURL</c> has minted and not
/// yet had revoked, and the <c>Blob</c> each of them names.
/// <para>
/// https://w3c.github.io/FileAPI/#BlobURLStore
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>An entry holds its blob strongly, and that is the point of the API.</b> A blob URL is a string a script
/// can put anywhere — into a variable, into a message, into a fetch it starts later — and the entry is what
/// keeps the bytes alive until <c>revokeObjectURL</c> says otherwise. It is therefore a leak by design if a
/// script forgets to revoke, which is exactly the trade a browser makes and why the standard's own note tells
/// authors to revoke.
/// </para>
/// <para>
/// <b>The origin is <c>null</c>, so a minted URL reads <c>blob:null/&lt;uuid&gt;</c>.</b> "Generate a new blob
/// URL" appends the ASCII serialization of the current settings object's origin, and an engine with no
/// document has an opaque origin, which serializes to <c>"null"</c>. Step 6 lets a user agent substitute
/// something implementation-defined for that, and a browser puts a UUID there; using the serialization itself
/// is the honest reading for a runtime that genuinely has no origin, and it is what makes
/// <c>new URL(blobUrl).origin</c> answer <c>"null"</c> — the same answer as the environment it was created
/// in. Jint's browser package, which has a document and therefore an origin, is what will put a real one
/// here.
/// </para>
/// <para>
/// <b>The store is per engine, and a restore empties it.</b> A blob URL belongs to the evaluation cycle that
/// minted it, the way a timer and a registered observer do — the strings are unreachable once the cycle's
/// globals are gone, and keeping the entries would make a pooled engine's memory grow one blob at a time with
/// nothing able to revoke them.
/// </para>
/// </remarks>
internal sealed class BlobUrlStore
{
    /// <summary>
    /// The entries, keyed by the URL's serialization <i>including</i> its fragment — which is what
    /// <c>revokeObjectURL</c> removes by. A lookup excludes the fragment instead, so
    /// <c>fetch(url + '#x')</c> finds the blob and <c>revokeObjectURL(url + '#x')</c> revokes nothing.
    /// </summary>
    private readonly Dictionary<string, JsBlob> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-add-an-entry — mint a URL for the blob and record it.
    /// </summary>
    internal string Add(JsBlob blob)
    {
        // "Generate a new blob URL": "blob:" + the origin + "/" + a UUID. Guid.NewGuid is a version 4 UUID,
        // which is what FileAPI/url/url-format.any.js's own pattern accepts.
        var url = "blob:null/" + Guid.NewGuid().ToString("D");
        _entries[url] = blob;
        return url;
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-obtain-a-blob-url-entry — "serializing url with the exclude
    /// fragment flag set", so a fragment appended to a blob URL still names the same blob.
    /// </summary>
    internal JsBlob? Resolve(UrlRecord url)
    {
        if (!string.Equals(url.Scheme, "blob", StringComparison.Ordinal))
        {
            return null;
        }

        return _entries.TryGetValue(url.Serialize(excludeFragment: true), out var blob) ? blob : null;
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#removeTheEntry — by the serialization <i>with</i> the fragment, which
    /// is why only an exact match revokes.
    /// </summary>
    internal void Remove(UrlRecord url)
    {
        if (string.Equals(url.Scheme, "blob", StringComparison.Ordinal))
        {
            _entries.Remove(url.Serialize());
        }
    }

    /// <summary>Drops every entry, because the evaluation cycle that minted them has ended.</summary>
    internal void Clear() => _entries.Clear();

    /// <summary>How many URLs are live, for a host that wants to see whether a script is revoking.</summary>
    internal int Count => _entries.Count;
}
#endif
