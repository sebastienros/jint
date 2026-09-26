using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

internal static partial class NativeCssStyleSheets
{
    // The host supplies snapshots and bounded work; query construction retains no engine or realm.
    internal static (NativeCssQuery Query, SelectorMatchWork Matching) CreateQuery(Document document,
        CssMediaEnvironment media, in SelectorEnvironment selectors, CssValueWork work,
        Action? selectorCheckpoint = null, NativeCssQueryDiagnostics? diagnostics = null)
    {
        var author = Get(document, work, includeShadow: true);
        var sheets = new List<NativeCssSheet>(author.Count + 1) { NativeCssBrowserDefaults.Sheet(document, work) };
        foreach (var sheet in author) { work.Charge(1); sheets.Add(sheet); }
        var query = new NativeCssQuery(document, sheets, [], media, selectors,
            CssEnvironmentSnapshot.Create([], work), work,
            new NativeCssMetrics { FontSize = media.InitialFontSize, RootFontSize = media.InitialFontSize },
            readInlineAttributes: true, systemColors: NativeCssBrowserDefaults.Palette(media.ColorScheme == "dark", work),
            diagnostics: diagnostics);
        var matching = new SelectorMatchWork(document, work.Token, selectorCheckpoint ?? work.CheckCancellation);
        work.CheckCancellation();
        return (query, matching);
    }
}
