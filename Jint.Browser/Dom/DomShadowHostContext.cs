using Jint.Browser.CustomElements;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Browser.Dom;

/// <summary>Cached host facts for native shadow attachment; this reads no JavaScript.</summary>
internal static class DomShadowHostContext
{
    internal static ShadowAttachmentContext Of(Element host, CustomElementRegistry? existingRegistry)
    {
        var record = existingRegistry?.TryGetRecord(host);
        var definition = record?.Definition
            ?? existingRegistry?.Lookup(host.OwnerDocument, host.NamespaceUri, host.LocalName, host.IsValue);
        return new ShadowAttachmentContext(host.OwnerDocument!.CustomElementRegistry,
            definition?.DisableShadow == true,
            record?.State is CustomElementState.Custom or CustomElementState.Precustomized);
    }
}

// The parser calls this only at the eligible declarative attachment point. The
// current cached registry can change after an earlier inline script defines it.
internal sealed class BrowserShadowHostContextProvider(PageRuntime runtime, Action<Element>? candidate = null) : IHtmlShadowHostContextProvider
{
    public ShadowAttachmentContext GetShadowAttachmentContext(Element host)
    {
        candidate?.Invoke(host);
        return DomShadowHostContext.Of(host, runtime.CustomElementsIfCreated);
    }
}
