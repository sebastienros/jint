using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Dom;

/// <summary>HTML serialization using the native tree traversal and attribute-name rules.</summary>
internal sealed class DomHtmlMarkupFormatter : HtmlMarkupFormatter
{
    internal static readonly IMarkupFormatter BrowserInstance = new DomHtmlMarkupFormatter();

    internal static string InnerHtml(INode node)
        => (node is IHtmlTemplateElement template ? template.Content.ChildNodes : node.ChildNodes)
            .ToHtml(BrowserInstance);

    protected override string Attribute(IAttr attribute)
    {
        var serialized = base.Attribute(attribute);
        if (attribute.Value is not { } value || value.AsSpan().IndexOfAny('<', '>') < 0)
        {
            return serialized;
        }

        // https://html.spec.whatwg.org/multipage/parsing.html#escapingString
        // The native formatter already escapes &, nonbreaking spaces and double quotes. Its last two
        // literal quotes delimit the value, even when a modern attribute name itself contains quotes.
        // Keep that name untouched and add only the two escapes missing from attribute mode.
        var start = serialized.LastIndexOf('"', serialized.Length - 2) + 1;
        return serialized[..start] + serialized[start..]
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
