using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

internal static class DomDocumentMembers
{
    // HTML §7.3: manufactured documents have no browsing context or window.
    internal static JsValue DefaultView(DomRealm realm, Document document)
        => DomBrowsingContext.Of(document) is not null && PageRuntime.FindBrowsingContext(realm.Engine, document) is { } runtime
            ? FrameWindows.ForDocument(runtime, document) : JsValue.Null;
}
