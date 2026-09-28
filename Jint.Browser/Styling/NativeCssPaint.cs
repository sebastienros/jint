using Jint.HtmlParser;
using Jint.HtmlParser.Css;
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
        var url = ResolveCssUrl(name, value.PaintUrl, value.Span, source);
        var fallback = value.PaintFallback;
        if (fallback is { Kind: CssPropertyValueKind.Color })
            fallback = ComputeColor(element, name, fallback, ref matching);
        return CssPropertyValue.PaintServer(url, fallback, value.Span, _work, value.PaintUsesSrc);
    }

    private CssPropertyValue ComputeUrl(string name, CssPropertyValue value, NativeCssSource? source) =>
        CssPropertyValue.UrlValue(value.Url with { Url = ResolveCssUrl(name, value.Url.Url, value.Span, source) }, value.Span, _work);

    private CssPropertyValue ComputeImages(string name, CssPropertyValue value, NativeCssSource? source)
    {
        var layers = new CssPropertyValue[value.Components.Count];
        for (var i = 0; i < layers.Length; i++)
        {
            _work.Charge(1);
            var layer = value.Components[i];
            layers[i] = layer.Kind == CssPropertyValueKind.Url ? ComputeUrl(name, layer, source) : layer;
        }
        return CssPropertyValue.ImageList(layers, value.Span, _work);
    }

    private string ResolveCssUrl(string name, string url, CssSourceSpan span, NativeCssSource? source)
    {
        // CSS Values 4, 4.5: local fragments and empty URLs keep their spelling; an
        // unresolvable URL keeps its specified value rather than becoming a different resource.
        if (url.Length != 0 && url[0] != '#')
        {
            if (_resolveUrl is null)
                throw new CssIncompleteGrammarException(name, "C6:url-resolver", span);
            var baseUrl = source?.Rule?.ParentStyleSheet?.Attachment.BaseUrl?.AbsoluteUri;
            _work.Charge(url.Length);
            _work.Charge(baseUrl?.Length ?? 0);
            url = _resolveUrl(_document, url, baseUrl, _work) ?? url;
            _work.CheckCancellation();
            Verify();
        }
        return url;
    }
}
