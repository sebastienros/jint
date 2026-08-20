#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url;

/// <summary>
/// A <c>URLSearchParams</c> instance.
/// <para>
/// https://url.spec.whatwg.org/#urlsearchparams
/// </para>
/// </summary>
/// <remarks>
/// The spec gives the object two associated values: a <i>list</i> of name/value tuples and a nullable
/// <i>URL object</i>. When the second is present the two stay in sync in both directions — every mutating
/// method runs <see cref="Update"/>, which rewrites the URL's query, and the <c>URL</c> attribute setters that
/// change the query rewrite this list.
/// </remarks>
internal sealed class JsUrlSearchParams : ObjectInstance
{
    internal JsUrlSearchParams(Engine engine) : base(engine, ObjectClass.Object)
    {
    }

    /// <summary>https://url.spec.whatwg.org/#concept-urlsearchparams-list</summary>
    internal List<FormUrlEncodedEntry> List { get; set; } = [];

    /// <summary>https://url.spec.whatwg.org/#concept-urlsearchparams-url-object</summary>
    internal JsUrl? UrlObject { get; set; }

    /// <summary>
    /// The update steps, https://url.spec.whatwg.org/#concept-urlsearchparams-update — run after every
    /// mutation, and a no-op for a standalone object.
    /// </summary>
    internal void Update()
    {
        if (UrlObject is null)
        {
            return;
        }

        var serialized = FormUrlEncoded.Serialize(List);
        UrlObject.Url.Query = serialized.Length == 0 ? null : serialized;
    }
}
#endif
