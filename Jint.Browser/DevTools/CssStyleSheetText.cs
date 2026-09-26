using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.DevTools;

/// <summary>The CSSOM serialization and identity-keyed offsets published by the CSS protocol.</summary>
/// <remarks>
/// The offsets index the returned serialization, including its LF line endings and nested rule ranges.
/// Each read takes a fresh bounded snapshot so a CSSOM edit never reuses a stale range table.
/// </remarks>
internal sealed class CssStyleSheetText(CssSerializationSnapshot snapshot)
{
    internal string Text => snapshot.Text;
    internal static CssStyleSheetText Of(CssStyleSheet sheet, CssValueWork work) =>
        new(sheet.SerializeWithRanges(work));
    internal bool TryRangeOf(CssRule rule, out CssRuleRange range)
    {
        if (snapshot.Ranges.TryGetValue(rule, out var source))
        {
            range = new(source.Start, source.End);
            return true;
        }
        range = default;
        return false;
    }
}

/// <summary>Offsets in the exact text published by CSS.getStyleSheetText.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CssRuleRange(int Start, int End);
