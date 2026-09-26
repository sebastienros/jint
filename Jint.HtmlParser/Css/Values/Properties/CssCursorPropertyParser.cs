namespace Jint.HtmlParser.Css.Values.Properties;

// CSS UI 4 §5.1.1: predefined cursors. No image loading or hotspot computation.
internal static class CssCursorPropertyParser
{
    private const string Keywords = "auto default none context-menu help pointer progress wait cell crosshair text vertical-text "
        + "alias copy move no-drop not-allowed grab grabbing e-resize n-resize ne-resize nw-resize s-resize se-resize "
        + "sw-resize w-resize ew-resize ns-resize nesw-resize nwse-resize col-resize row-resize all-scroll zoom-in zoom-out";

    internal static CssPropertyResult Parse(List<CssComponentValue> parts, CssValueWork work)
    {
        foreach (var part in parts)
        {
            work.Charge(1);
            if ((part.Kind == CssComponentKind.Token && part.Token.Kind == CssTokenKind.Url) ||
                (part.Kind == CssComponentKind.Function && ImageFunction(part.FunctionName, work)))
            {
                work.CheckCancellation();
                return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "V6:cursor-images");
            }
        }
        if (parts.Count != 1) return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
        work.Charge(Keywords.Length);
        var keyword = CssPropertyParser.Keyword(parts[0], Keywords, work);
        work.CheckCancellation();
        return keyword is null ? CssPropertyResult.Rejected(CssPropertyStatus.Invalid)
            : CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, parts[0].Span));
    }

    private static bool ImageFunction(string name, CssValueWork work)
    {
        work.Charge("url image-set -webkit-image-set".Length);
        name = CssPropertyRegistry.NormalizeName(name, work);
        return name is "url" or "image-set" or "-webkit-image-set";
    }
}
