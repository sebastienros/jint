using Jint.Browser.Dom.Collections;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>https://html.spec.whatwg.org/multipage/interaction.html#dom-document-designmode</summary>
internal static class DomDocumentEditing
{
    internal static string Get(Document document) => DomDocumentState.IsDesignModeEnabled(document) ? "on" : "off";

    internal static JsValue Set(DomRealm realm, Document document, string value)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        if (work.EqualAsciiIgnoreCase(value, "on"))
        {
            if (DomDocumentState.IsDesignModeEnabled(document))
            {
                work.Check();
                return JsValue.Undefined;
            }
            work.Check();
            DomDocumentState.Of(document).DesignModeEnabled = true;
            BrowserSelectorSemanticRevision.Advance(document);
            // The existing Selection implementation belongs to the displayed document.
            // A child or inert document must never reset or notify its principal's range.
            if (PageRuntime.Find(realm.Engine, document)?.ViewsIfCreated?.ExistingSelection?.Range is { } range)
            {
                range.SetStart(new DomNodeIdentity(document), 0);
                range.SetEnd(new DomNodeIdentity(document), 0);
            }
            if (DomChildHtmlCollection.First(document, realm.NativeReadCheckpoint, realm.CancellationToken) is { } element)
                FocusController.Focus(realm, element);
        }
        else if (work.EqualAsciiIgnoreCase(value, "off"))
        {
            work.Check();
            if (DomDocumentState.IsDesignModeEnabled(document))
            {
                DomDocumentState.Of(document).DesignModeEnabled = false;
                BrowserSelectorSemanticRevision.Advance(document);
            }
        }
        work.Check();
        return JsValue.Undefined;
    }
}
