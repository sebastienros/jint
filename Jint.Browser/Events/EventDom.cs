using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.Browser.Events;

/// <summary>HTML event classifications over the native node identity and attributes.</summary>
internal static class EventDom
{
    internal static bool HasContentAttribute(this Element element, string name) => element.GetAttributeNodeNS(null, name) is not null;
    internal static bool IsHtml(Element element, string name) => element.NamespaceUri == Namespaces.Html && element.LocalName == name;
    internal static string InputType(Element element) => HtmlInputTypes.Info(HtmlInputTypes.Get(element)).Keyword;
    internal static string ButtonType(Element element)
        => element.GetAttributeNS(null, "type") switch
        {
            { } type when CssAscii.EqualsIgnoreCase(type, "reset") => "reset",
            { } type when CssAscii.EqualsIgnoreCase(type, "button") => "button",
            _ => "submit",
        };

    internal static bool Disabled(DomRealm dom, Element element)
        => HtmlDisabledness.GetState(element, dom.NativeReadCheckpoint, dom.CancellationToken) == HtmlDisabledState.Disabled;

    internal static bool Disabled(Element element, CancellationToken cancellationToken = default)
        => HtmlDisabledness.GetState(element, cancellationToken) == HtmlDisabledState.Disabled;
}
