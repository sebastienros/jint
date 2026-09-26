using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Colors;

namespace Jint.Browser.Styling;

// HTML Rendering §15.3.1: the supported display rules from the HTML user-agent sheet.
// https://html.spec.whatwg.org/multipage/rendering.html#the-css-user-agent-style-sheet-and-presentational-hints
internal static class NativeCssBrowserDefaults
{
    private const string Source = """
        html, body, address, article, aside, blockquote, center, details, dialog, dir, div,
        dl, dt, dd, fieldset, figcaption, figure, footer, form, h1, h2, h3, h4, h5, h6,
        header, hgroup, hr, legend, listing, main, menu, nav, ol, p, plaintext, pre,
        search, section, summary, ul, xmp { display: block; }
        head, base, basefont, link, meta, title, style, script, template, area, datalist,
        param, source, track, noframes { display: none; }
        li { display: list-item; }
        table { display: table; }
        caption { display: table-caption; }
        colgroup { display: table-column-group; }
        col { display: table-column; }
        thead { display: table-header-group; }
        tbody { display: table-row-group; }
        tfoot { display: table-footer-group; }
        tr { display: table-row; }
        td, th { display: table-cell; }
        button, input, select, textarea { display: inline-block; }
        input[type="hidden" i], dialog:not([open]), [hidden]:not([hidden="until-found" i]) { display: none; }
        ruby { display: ruby; }
        rt { display: ruby-text; }
        rp { display: none; }
        [dir="ltr" i] { direction: ltr; }
        [dir="rtl" i] { direction: rtl; }
        """;
    private static readonly ConditionalWeakTable<Document, CssStyleSheet> Sheets = new();

    internal static NativeCssSheet Sheet(Document document, CssValueWork work) =>
        new(Sheets.GetValue(document, _ => CssStyleSheet.Parse(Source, null, work, work.Token)),
            NativeCssOrigin.UserAgent, Namespaces.Html);

    // Explicit headless-device policy: a neutral light/dark canvas, independent of a desktop theme.
    // Other device colors require a supplied palette and retain their named missing-input failure.
    internal static NativeCssSystemColors Palette(bool dark, CssValueWork work)
    {
        var black = CssColorValue.Identity(CssColorKind.Named, "black", default, 0);
        var white = CssColorValue.Identity(CssColorKind.Named, "white", default, 0xffffff);
        return NativeCssSystemColors.Create([("canvas", dark ? black : white), ("canvastext", dark ? white : black)], work);
    }
}
