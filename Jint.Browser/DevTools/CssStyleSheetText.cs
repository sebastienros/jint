using System.Text;
using AngleSharp.Css.Dom;

namespace Jint.Browser.DevTools;

/// <summary>
/// The text a client is given for one style sheet, and where every rule in it starts and ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>The text is the CSSOM's serialization, not the bytes the page was authored with, and the two cannot
/// be swapped.</b> <c>CSS.getStyleSheetText</c> and a <c>RuleUsage</c>'s <c>startOffset</c> and
/// <c>endOffset</c> are one answer: a client slices the first with the second — that is the whole of what
/// Puppeteer's and Playwright's CSS coverage do with them — so the offsets must index the string this
/// hands out. AngleSharp keeps the authored text (<c>IStyleSheet.Source</c>) but no <c>ICssRule</c> carries
/// a source position, so there is no way to say where a rule sits inside it. Serializing the sheet here and
/// measuring the same serialization is the only pairing that is internally consistent, and it is what
/// Chrome itself does for a sheet with no source text of its own — a constructed <c>new CSSStyleSheet()</c>
/// is reported with an empty <c>sourceURL</c> and a text built from its rules.
/// </para>
/// <para>
/// <b>It is built on demand and never cached.</b> A cached snapshot would go stale the moment a page
/// inserted a rule, and this domain publishes no <c>styleSheetChanged</c> for a client to notice with; a
/// fresh one always describes the sheet as it is now. Only a client that asked for coverage or for a
/// sheet's text pays for it.
/// </para>
/// <para>
/// <b>The layout is deliberately its own rather than <c>ICssStyleSheet.ToCss()</c>.</b> That serializer
/// joins rules with the platform's newline, which would put a coverage report's offsets four bytes apart on
/// Windows and Linux for the same page; and it emits a grouping rule on one line, which would leave a
/// nested rule with no range of its own. One rule per line, two spaces of nesting, <c>\n</c> everywhere.
/// </para>
/// </remarks>
internal sealed class CssStyleSheetText
{
    private readonly Dictionary<ICssRule, CssRuleRange> _ranges = new(ReferenceEqualityComparer.Instance);

    private CssStyleSheetText(string text)
    {
        Text = text;
    }

    /// <summary>The sheet's text, which is what <c>CSS.getStyleSheetText</c> answers.</summary>
    internal string Text { get; }

    /// <summary>Serializes <paramref name="sheet"/> and measures every rule in it.</summary>
    internal static CssStyleSheetText Of(ICssStyleSheet sheet)
    {
        var builder = new StringBuilder();
        var ranges = new List<(ICssRule Rule, CssRuleRange Range)>();
        Write(sheet.Rules, builder, ranges, indent: "");

        var text = new CssStyleSheetText(builder.ToString());
        foreach (var (rule, range) in ranges)
        {
            text._ranges[rule] = range;
        }

        return text;
    }

    /// <summary>Where <paramref name="rule"/> sits in <see cref="Text"/>, when it sits anywhere.</summary>
    /// <remarks>
    /// A rule with no range is one this serialization does not write on its own line — a style rule nested
    /// inside another style rule, which CSS nesting allows and this writes as part of its parent's text.
    /// A client is told nothing about it rather than told the wrong offsets.
    /// </remarks>
    internal bool TryRangeOf(ICssRule rule, out CssRuleRange range) => _ranges.TryGetValue(rule, out range);

    private static void Write(
        ICssRuleList rules,
        StringBuilder builder,
        List<(ICssRule Rule, CssRuleRange Range)> ranges,
        string indent)
    {
        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            builder.Append(indent);
            var start = builder.Length;

            if (rule is ICssGroupingRule group)
            {
                builder.Append(Prelude(rule)).Append('\n');
                Write(group.Rules, builder, ranges, indent + "  ");
                builder.Append(indent).Append('}');
            }
            else
            {
                builder.Append(rule.CssText);
            }

            ranges.Add((rule, new CssRuleRange(start, builder.Length)));
            builder.Append('\n');
        }
    }

    /// <summary>A grouping rule's own text, up to and including the brace its children sit inside.</summary>
    /// <remarks>
    /// Read off the rule's serialization rather than rebuilt per rule type, so <c>@media</c>,
    /// <c>@supports</c>, <c>@container</c>, <c>@layer</c> and whatever AngleSharp.Css groups next are all
    /// one line of code. A prelude cannot contain a brace, so the first one ends it.
    /// </remarks>
    private static string Prelude(ICssRule rule)
    {
        var text = rule.CssText;
        var brace = text.IndexOf('{');
        return brace < 0 ? text : text[..(brace + 1)];
    }
}

/// <summary>Where one rule starts and ends inside its sheet's text.</summary>
/// <param name="Start">The offset of the rule's first character, its selector or at-keyword included.</param>
/// <param name="End">The offset just past the rule's closing brace.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CssRuleRange(int Start, int End);
