using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>The canvas capability boundary when no renderer or encoder is installed.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#the-canvas-element</remarks>
internal static class BrowserCanvasMembers
{
    internal static JsValue GetContext(DomRealm realm, Element element, JsValue[] arguments)
    {
        Require(realm, element, "HTMLCanvasElement.getContext");
        _ = DomConvert.RequiredText(arguments, 0, "HTMLCanvasElement.getContext");
        realm.Engine.Constraints.Check();
        return JsValue.Null;
    }

    internal static JsValue ProbablySupportsContext(DomRealm realm, Element element, JsValue[] arguments)
    {
        Require(realm, element, "HTMLCanvasElement.probablySupportsContext");
        _ = DomConvert.RequiredText(arguments, 0, "HTMLCanvasElement.probablySupportsContext");
        realm.Engine.Constraints.Check();
        return JsBoolean.False;
    }

    internal static JsValue SetContext(DomRealm realm, Element element, JsValue[] arguments)
    {
        Require(realm, element, "HTMLCanvasElement.setContext");
        // This legacy, nonstandard operation cannot retain a foreign rendering context.
        return DomFailures.Refuse(realm, "HTMLCanvasElement.setContext", "NotSupportedError", "No canvas renderer is available.");
    }

    internal static JsValue ToDataUrl(DomRealm realm, Element element, JsValue[] arguments)
    {
        Require(realm, element, "HTMLCanvasElement.toDataURL");
        _ = DomConvert.OptionalText(arguments, 0, "image/png");
        realm.Engine.Constraints.Check();
        return DomFailures.Refuse(realm, "HTMLCanvasElement.toDataURL", "NotSupportedError", "No canvas encoder is available.");
    }

    private static void Require(DomRealm realm, Element element, string member)
    {
        if (element.NamespaceUri != Namespaces.Html || element.LocalName != "canvas")
        {
            Throw.TypeError(realm.OwningRealm, "Illegal invocation of " + member);
        }
    }
}
