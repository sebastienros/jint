using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values.Descriptors;

// CSS Fonts 4 §4, Fonts 5 §3. These are descriptors, never ordinary properties.
internal static class CssFontFaceDescriptorCatalog
{
    internal static string NormalizeName(string name, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(name);
        work.CheckCancellation();
        var result = string.Create(name.Length, (Name: name, Work: work), static (target, state) =>
        {
            var source = state.Name;
            for (var i = 0; i < source.Length; i++)
            {
                state.Work.Charge(1);
                target[i] = source[i] is >= 'A' and <= 'Z' ? (char) (source[i] + 32) : source[i];
            }
        });
        work.CheckCancellation();
        return result;
    }

    internal static CssPropertyResult? NameFailure(string name) => name switch
    {
        "font-family" or "src" or "font-display" or "font-weight" or "font-style" => null,
        "font-width" or "font-stretch" or "unicode-range" or "font-feature-settings" or
        "font-variation-settings" or "font-named-instance" or "font-language-override" or
        "ascent-override" or "descent-override" or "line-gap-override" or "size-adjust" or "font-variant" =>
            CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "R4:font-face:" + name),
        _ => CssPropertyResult.Rejected(CssPropertyStatus.UnsupportedProperty)
    };
}
