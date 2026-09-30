using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;
using Jint.Browser.Dom.Canvas;

namespace Jint.Browser.Dom;

/// <summary>Non-rendering 2D context acquisition and transparent PNG serialization.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#the-canvas-element</remarks>
internal static class BrowserCanvasMembers
{
    internal static JsValue GetContext(DomRealm realm, Element element, JsValue[] arguments)
    {
        Require(realm, element, "HTMLCanvasElement.getContext");
        var name = DomConvert.RequiredText(arguments, 0, "HTMLCanvasElement.getContext");
        realm.Engine.Constraints.Check();
        return name == "2d" ? realm.Canvas.GetContext(element, arguments.At(1)) : JsValue.Null;
    }

    internal static JsValue ProbablySupportsContext(DomRealm realm, Element element, JsValue[] arguments)
    {
        Require(realm, element, "HTMLCanvasElement.probablySupportsContext");
        var name = DomConvert.RequiredText(arguments, 0, "HTMLCanvasElement.probablySupportsContext");
        realm.Engine.Constraints.Check();
        return JsBoolean.Create(name == "2d");
    }

    internal static JsValue SetContext(DomRealm realm, Element element, JsValue[] _)
    {
        Require(realm, element, "HTMLCanvasElement.setContext");
        // This legacy, nonstandard operation cannot retain a foreign rendering context.
        return DomFailures.Refuse(realm, "HTMLCanvasElement.setContext", "NotSupportedError", "No canvas renderer is available.");
    }

    internal static JsValue ToDataUrl(DomRealm realm, Element element, JsValue[] arguments)
    {
        Require(realm, element, "HTMLCanvasElement.toDataURL");
        _ = DomConvert.OptionalText(arguments, 0, "image/png");
        var bytes = Encode(realm, element);
        return JsString.Create(bytes is null ? "data:," : "data:image/png;base64," + Convert.ToBase64String(bytes));
    }

    internal static JsValue ToBlob(DomRealm realm, Element element, JsValue[] arguments)
    {
        Require(realm, element, "HTMLCanvasElement.toBlob");
        if (arguments.Length == 0 || arguments[0] is not ICallable)
            Throw.TypeError(realm.OwningRealm, "The toBlob callback must be callable.");
        var callback = (ICallable) arguments[0];
        _ = DomConvert.OptionalText(arguments, 1, "image/png");
        var bytes = Encode(realm, element);
        realm.Canvas.Post(() => callback.Call(JsValue.Undefined, bytes is null ? JsValue.Null : realm.Canvas.Blob(bytes)));
        return JsValue.Undefined;
    }

    private static byte[]? Encode(DomRealm realm, Element element)
        => CanvasPng.Encode(realm.Canvas,
            DomReflected.HTMLCanvasElementWidth.Get(realm, element).AsNumber(),
            DomReflected.HTMLCanvasElementHeight.Get(realm, element).AsNumber());

    private static void Require(DomRealm realm, Element element, string member)
    {
        if (element.NamespaceUri != Namespaces.Html || element.LocalName != "canvas")
        {
            Throw.TypeError(realm.OwningRealm, "Illegal invocation of " + member);
        }
    }
}
