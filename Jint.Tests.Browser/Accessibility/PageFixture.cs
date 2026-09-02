using AngleSharp;
using AngleSharp.Dom;

namespace Jint.Tests.Browser.Accessibility;

/// <summary>
/// A parsed document with the CSS cascade available, which is what every test in this area needs and all
/// that any of them needs.
/// </summary>
/// <remarks>
/// There is no engine here on purpose: the accessibility tree and the extraction payloads are computed over
/// AngleSharp's DOM alone, so a test that reached for one would be testing something the production path
/// never does.
/// </remarks>
internal static class PageFixture
{
    /// <summary>Parses <paramref name="html"/> with <c>AngleSharp.Css</c> registered.</summary>
    internal static IDocument Parse(string html, string? url = null)
    {
        var context = BrowsingContext.New(Configuration.Default.WithCss());
        return context.OpenAsync(response =>
        {
            response.Content(html);
            if (url is not null)
            {
                response.Address(url);
            }
        }).GetAwaiter().GetResult();
    }

    /// <summary>Parses <paramref name="html"/> without the CSS services, so the cascade cannot answer.</summary>
    internal static IDocument ParseWithoutCss(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        return context.OpenAsync(response => response.Content(html)).GetAwaiter().GetResult();
    }

    /// <summary>Parses <paramref name="html"/> and returns the element with the identifier <c>t</c>.</summary>
    internal static IElement Target(string html)
    {
        var document = Parse(html);
        return document.GetElementById("t")
            ?? throw new InvalidOperationException("The fixture has no element with the identifier 't'.");
    }
}
