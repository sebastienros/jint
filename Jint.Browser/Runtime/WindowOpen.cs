using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Runtime;

/// <summary>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#window-open-steps
/// No activation-based popup blocking; separate pages always use the restricted cross-origin surface.
/// </summary>
internal static class WindowOpen
{
    internal static JsValue Open(PageRuntime runtime, JsValue[] arguments)
    {
        var url = arguments.At(0).IsUndefined() ? "" : TypeConverter.ToString(arguments[0]);
        var name = arguments.At(1).IsUndefined() ? "_blank" : TypeConverter.ToString(arguments[1]);
        var features = arguments.At(2).IsUndefined() ? "" : TypeConverter.ToString(arguments[2]);
        var (noopener, noreferrer) = Features(features);
        if (name.Length == 0) name = "_blank";
        var navigation = Navigation(runtime, url, noreferrer, preserveEmpty: true);
        if (IsCurrent(runtime, name, noopener))
        {
            if (url.Length != 0) runtime.Page.RequestNavigation(navigation.Url, replace: false, engine: runtime.Engine,
                referrer: navigation.Referrer);
            // Window open steps: noopener returns null even when the current navigable was chosen.
            return noopener ? JsValue.Null : runtime.Engine._mainRealm.GlobalObject;
        }
        var handle = runtime.Page.Context.ChoosePopup(runtime.Page, name, noopener, navigation);
        return noopener || handle is null ? JsValue.Null : runtime.WindowProxyFor(handle);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/links.html#get-an-element's-noopener
    /// Applies to anchors, areas and forms, including the implicit noopener on _blank.
    /// </summary>
    internal static (bool Noopener, bool Noreferrer) Relationship(Element element, string target)
    {
        var tokens = (element.GetAttribute("rel") ?? "").Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries);
        bool Has(string value) => tokens.Contains(value, StringComparer.OrdinalIgnoreCase);
        var noreferrer = Has("noreferrer");
        return (noreferrer || Has("noopener") || target.Equals("_blank", StringComparison.OrdinalIgnoreCase) && !Has("opener"), noreferrer);
    }

    internal static string Target(PageRuntime runtime, Element element, string? target)
        => target ?? (element.OwnerDocument is { } document
            ? DomSelectors.QuerySelector(runtime.Dom, document, "base[target]")?.GetAttribute("target") : null) ?? "";

    internal static bool NavigateTarget(PageRuntime runtime, string name, bool noopener, CrossPageNavigation navigation)
    {
        if (IsCurrent(runtime, name, noopener)) return false;
        runtime.Page.Context.ChoosePopup(runtime.Page, name, noopener, navigation);
        return true;
    }

    internal static CrossPageNavigation Navigation(PageRuntime runtime, string url, bool noreferrer = false,
        bool replace = false, byte[]? body = null, string? contentType = null, bool preserveEmpty = false)
    {
        var baseUrl = runtime.Document is { } document
            ? DomDocumentState.BaseUri(document, runtime.Engine.Constraints.Check, runtime.Dom.CancellationToken)
            : runtime.DocumentUrl;
        var resolved = url.Length == 0 && preserveEmpty ? "" : PageUrl.Resolve(url, baseUrl);
        if (resolved is null)
            ThrowDom(runtime, DomExceptionNames.Syntax, "The URL '" + url + "' cannot be parsed.");
        return new CrossPageNavigation(resolved!, runtime.DocumentUrl,
            noreferrer || !PageUrl.HasOrigin(runtime.DocumentUrl) ? "" : runtime.DocumentUrl, DomDocumentMetadata.CreatorOrigin(runtime.Dom),
            baseUrl, replace, body, contentType);
    }

    private static bool IsCurrent(PageRuntime runtime, string name, bool noopener)
    {
        if (name.Length == 0 || name.Equals("_self", StringComparison.OrdinalIgnoreCase)
            || name.Equals("_parent", StringComparison.OrdinalIgnoreCase)
            || name.Equals("_top", StringComparison.OrdinalIgnoreCase)
            || !noopener && !name.Equals("_blank", StringComparison.OrdinalIgnoreCase) && name == runtime.WindowName)
            return true;
        if (!noopener && !name.Equals("_blank", StringComparison.OrdinalIgnoreCase) && runtime.Document is { } document)
        {
            var work = new DomReadWork(runtime.Dom.NativeReadCheckpoint, runtime.Dom.CancellationToken);
            foreach (var element in NodeTraversal.DescendantElements(document, runtime.Dom.CancellationToken))
            {
                work.Step();
                if (element is { NamespaceUri: Namespaces.Html, LocalName: "iframe" or "frame" }
                    && work.Attribute(element, "name") == name)
                {
                    runtime.Recorder.Add(PageErrorKind.ReportedError,
                        "The target '" + name + "' names a child frame; independent child navigation is not supported, so it loads in the same page.",
                        "target");
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#concept-window-open-features-tokenize</summary>
    private static (bool Noopener, bool Noreferrer) Features(string features)
    {
        var noopener = false;
        var noreferrer = false;
        var position = 0;
        while (position < features.Length)
        {
            while (position < features.Length && Separator(features[position])) position++;
            var start = position;
            while (position < features.Length && !Separator(features[position])) position++;
            var name = features[start..position];
            while (position < features.Length && features[position] != '='
                && features[position] != ',' && Separator(features[position])) position++;
            var value = "";
            if (position < features.Length && Separator(features[position]))
            {
                while (position < features.Length && features[position] != ',' && Separator(features[position])) position++;
                start = position;
                while (position < features.Length && !Separator(features[position])) position++;
                value = features[start..position];
            }
            if (name.Equals("noopener", StringComparison.OrdinalIgnoreCase)) noopener = BooleanFeature(value);
            if (name.Equals("noreferrer", StringComparison.OrdinalIgnoreCase)) noreferrer = BooleanFeature(value);
        }
        return (noopener || noreferrer, noreferrer);
    }

    private static bool Separator(char value) => value is ' ' or '\t' or '\r' or '\n' or '\f' or '=' or ',';

    private static bool BooleanFeature(string value)
    {
        if (value.Length == 0 || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        var position = value[0] is '+' or '-' ? 1 : 0;
        var nonzero = false;
        while (position < value.Length && value[position] is >= '0' and <= '9')
            nonzero |= value[position++] != '0';
        return nonzero;
    }

    internal static void ThrowDom(PageRuntime runtime, string name, string message)
        => Throw.JavaScriptException(runtime.Engine,
            runtime.Engine._mainRealm.Intrinsics.DomException.CreateException(name, message),
            runtime.Engine.GetLastSyntaxElement()?.Location ?? default);
}
