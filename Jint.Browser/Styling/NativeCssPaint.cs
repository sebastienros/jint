using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Styling;

internal delegate string? NativeCssUrlResolver(Document document, string url, string? stylesheetBaseUrl, CssValueWork work);

internal sealed partial class NativeCssQuery
{
    // https://svgwg.org/svg2-draft/painting.html#SpecifyingPaint: computed values, not rendered paint.
    private CssPropertyValue ComputePaint(Element element, string name, CssPropertyValue value,
        NativeCssSource? source, ref SelectorMatchWork matching)
    {
        var url = value.PaintUrl;
        // CSS Values 4, 4.5: local fragments and empty URLs keep their spelling; an
        // unresolvable URL keeps its specified value rather than becoming a different resource.
        if (url.Length != 0 && url[0] != '#')
        {
            if (_resolveUrl is null)
                throw new CssIncompleteGrammarException(name, "C6:url-resolver", value.Span);
            var baseUrl = source?.Rule?.ParentStyleSheet?.Attachment.BaseUrl?.AbsoluteUri;
            _work.Charge(url.Length);
            _work.Charge(baseUrl?.Length ?? 0);
            url = _resolveUrl(_document, url, baseUrl, _work) ?? url;
            _work.CheckCancellation();
            Verify();
        }
        var fallback = value.PaintFallback;
        if (fallback is { Kind: CssPropertyValueKind.Color })
            fallback = ComputeColor(element, name, fallback, ref matching);
        return CssPropertyValue.PaintServer(url, fallback, value.Span, _work, value.PaintUsesSrc);
    }
}
