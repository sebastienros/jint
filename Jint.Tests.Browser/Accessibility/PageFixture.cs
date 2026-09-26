using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Accessibility;

/// <summary>
/// A parsed native document, which is what every test in this area needs and all
/// that any of them needs.
/// </summary>
/// <remarks>
/// There is no engine here on purpose: the accessibility tree and the extraction payloads are computed over
/// the native DOM alone, so a test that reached for one would be testing something the production path
/// never does.
/// </remarks>
internal static class PageFixture
{
    /// <summary>Parses inert HTML into the actual native tree, without creating an engine.</summary>
    internal static Document Parse(string html, string? url = null)
    {
        var document = ContentDom.Parse(html);
        if (url is not null) DomDocumentState.Of(document).Url = url;
        return document;
    }

    /// <summary>Returns the element with the identifier <c>t</c> from a native parse.</summary>
    internal static Element Target(string html)
    {
        var document = Parse(html);
        return ContentDom.ElementById(document, "t")
            ?? throw new InvalidOperationException("The fixture has no element with the identifier 't'.");
    }
}
