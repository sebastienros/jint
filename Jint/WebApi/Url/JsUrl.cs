#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url;

/// <summary>
/// A <c>URL</c> instance.
/// <para>
/// https://url.spec.whatwg.org/#url-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The spec gives the object two associated values: a <i>URL</i> record and a <i>query object</i>. Every
/// attribute is an accessor on <see cref="UrlPrototype"/> reading them through a brand check, so an instance
/// has no own property at all — which is what a browser reports for
/// <c>Object.getOwnPropertyNames(new URL("https://example.org/"))</c>.
/// </para>
/// <para>
/// The query object is created on first access rather than in the initialization steps. That is not observable:
/// <c>[SameObject]</c> only requires the getter to keep answering with the same object, which the cached field
/// does, and every route by which the URL's query can change while no query object exists ends in the object
/// being built from the query as it then stands. What it buys is that a <c>URL</c> nobody asks
/// <c>searchParams</c> of — the overwhelmingly common case, and the one <c>fetch</c> takes — allocates neither
/// the object nor its parsed list.
/// </para>
/// </remarks>
internal sealed class JsUrl : ObjectInstance
{
    private readonly Realm _realm;
    private JsUrlSearchParams? _queryObject;

    internal JsUrl(Engine engine, Realm realm, UrlRecord url) : base(engine, ObjectClass.Object)
    {
        _realm = realm;
        Url = url;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#concept-url-url. Replaced wholesale by the <c>href</c> setter, which
    /// parses a new record rather than mutating this one.
    /// </summary>
    internal UrlRecord Url { get; set; }

    /// <summary>
    /// https://url.spec.whatwg.org/#concept-url-query-object — the object the <c>searchParams</c> getter
    /// returns, built on first ask from the query as it then stands.
    /// </summary>
    internal JsUrlSearchParams QueryObject
    {
        get
        {
            if (_queryObject is null)
            {
                _queryObject = _realm.Intrinsics.WebApiUrlSearchParams.CreateInstance(FormUrlEncoded.Parse(Url.Query ?? string.Empty));
                _queryObject.UrlObject = this;
            }

            return _queryObject;
        }
    }

    /// <summary>
    /// "Empty this's query object's list" followed by re-filling it — the tail of the <c>href</c> and
    /// <c>search</c> setters. A query object that does not exist yet needs nothing: it will be built from the
    /// query the setter just wrote.
    /// </summary>
    internal void ReplaceQueryObjectList(List<FormUrlEncodedEntry> list)
    {
        if (_queryObject is not null)
        {
            _queryObject.List = list;
        }
    }
}
#endif
