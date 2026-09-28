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

    internal static CssPropertyResult? NameFailure(string name) => CssFontFaceDescriptorLookup.Match(name) switch
    {
        CssFontFaceDescriptorKind.FontFamily or CssFontFaceDescriptorKind.Src or CssFontFaceDescriptorKind.FontDisplay or CssFontFaceDescriptorKind.FontWeight or CssFontFaceDescriptorKind.FontStyle => null,
        CssFontFaceDescriptorKind.FontWidth or CssFontFaceDescriptorKind.FontStretch or CssFontFaceDescriptorKind.UnicodeRange or CssFontFaceDescriptorKind.FontFeatureSettings or
        CssFontFaceDescriptorKind.FontVariationSettings or CssFontFaceDescriptorKind.FontNamedInstance or CssFontFaceDescriptorKind.FontLanguageOverride or
        CssFontFaceDescriptorKind.AscentOverride or CssFontFaceDescriptorKind.DescentOverride or CssFontFaceDescriptorKind.LineGapOverride or CssFontFaceDescriptorKind.SizeAdjust or CssFontFaceDescriptorKind.FontVariant =>
            CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "R4:font-face:" + name),
        _ => CssPropertyResult.Rejected(CssPropertyStatus.UnsupportedProperty)
    };
}
