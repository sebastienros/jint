using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace Jint.HtmlParser.Css.Model;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct CssTextRange(int Start, int End);

internal sealed class CssSerializationSnapshot(string text, Dictionary<CssRule, CssTextRange> ranges)
{
    internal string Text { get; } = text;
    internal IReadOnlyDictionary<CssRule, CssTextRange> Ranges { get; } = new ReadOnlyDictionary<CssRule, CssTextRange>(ranges);
}

