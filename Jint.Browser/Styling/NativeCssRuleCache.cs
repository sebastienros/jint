using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

// CSSOM §6.4.3: preparation may share selector programs and immutable declaration entries,
// never a live rule, parent, sheet, media decision or matching result.
internal sealed class NativeCssRuleCache
{
    private const int MaxEntries = 8192;
    private const int MaxUnits = 8 * 1024 * 1024;
    private const int MaxRuleLength = 64 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<Key, Entry> _entries = new();
    private readonly Queue<Key> _order = new();
    private int _units;

    private readonly record struct Key(string Text, int Start);

    // The declaration prototype is private, unowned and never mutated or returned. Copy creates
    // a separate owner whose writes replace its immutable entry array rather than changing it.
    private sealed record Entry(CompiledSelector Selector, string SelectorText,
        CssDeclarationBlock Declarations, int Units);

    internal CssParsedRule? Parse(CssRawRule raw, CssStyleRule? nestingParent, CssValueWork work)
    {
        work.CheckCancellation();
        if (nestingParent is not null || raw.Kind != CssRuleKind.QualifiedRule || raw.Text.Span.Length > MaxRuleLength)
            return CssParser.ParseRule(raw, work, nestingParent);

        work.Charge(raw.Text.Span.Length);
        var key = new Key(raw.Text.Text, raw.Text.Span.Start);
        Entry? entry;
        lock (_gate) _entries.TryGetValue(key, out entry);
        if (entry is not null)
        {
            work.Charge(entry.Declarations.Count);
            var rule = new CssStyleRule(entry.Selector, entry.SelectorText,
                entry.Declarations.Copy(work), raw.Text.Span);
            work.CheckCancellation();
            return new(rule, raw.Body is { } body ? new(body, CssRuleBodyKind.Style, raw.IsClosed) : null, null);
        }

        var parsed = CssParser.ParseRule(raw, work);
        if (parsed?.Rule is not CssStyleRule style) return parsed;
        // Charge expanded declarations as well as source/program storage. Oversized rules still parse.
        var units = ((long) key.Text.Length * 8) + ((long) style.Style.Count * 128) + 256;
        work.CheckCancellation();
        if (units <= MaxUnits)
        {
            entry = new(style.Selector, style.SelectorText, style.Style.Copy(work), (int) units);
            lock (_gate)
            {
                if (_entries.TryAdd(key, entry))
                {
                    _order.Enqueue(key);
                    _units += entry.Units;
                    while (_entries.Count > MaxEntries || _units > MaxUnits)
                    {
                        var oldest = _order.Dequeue();
                        _units -= _entries[oldest].Units;
                        _entries.Remove(oldest);
                    }
                }
            }
        }
        work.CheckCancellation();
        return parsed;
    }
}
