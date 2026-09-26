using System.Globalization;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>HTML document resource metadata, frozen independently of later URL changes.</summary>
internal static class DomDocumentMetadata
{
    internal static DomDocumentOrigin CreatorOrigin(DomRealm realm)
        => realm.Document is { } document ? DomDocumentState.Of(document).Origin
            : PageRuntime.Find(realm.Engine)?.DocumentCreationOrigin ?? DomDocumentOrigin.Opaque();

    internal static string Origin(Document document) => DomDocumentState.Of(document).Origin.Serialized;
    // https://html.spec.whatwg.org/multipage/browsers.html#dom-document-domain
    internal static string Domain(Document document) => DomDocumentState.Of(document).Origin.Domain;
    internal static JsValue SetDomain(DomRealm realm, Document document, string value)
    {
        // Domain relaxation is an existing unsupported Browser capability; changing the URL
        // would neither relax the actual origin nor implement the cross-origin WindowProxy.
        Throw.JavaScriptException(realm.Engine,
            realm.OwningRealm.Intrinsics.DomException.CreateException(DomExceptionNames.NotSupported,
                "Changing document.domain is not supported."), realm.Engine.GetLastSyntaxElement()?.Location ?? default);
        return JsValue.Undefined;
    }

    // https://html.spec.whatwg.org/multipage/dom.html#dom-document-lastmodified
    internal static string LastModified(Document document)
        => (DomDocumentState.Of(document).SourceLastModified ?? DateTimeOffset.Now).ToLocalTime()
            .ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    internal static DateTimeOffset? ParseLastModified(string? value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var result) ? result : null;

    internal static void Initialize(Document document, DomDocumentOrigin origin, DateTimeOffset? lastModified = null)
    {
        var state = DomDocumentState.Of(document);
        state.Origin = origin;
        state.SourceLastModified = lastModified;
    }
}
