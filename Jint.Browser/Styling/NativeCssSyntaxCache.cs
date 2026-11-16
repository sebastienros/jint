using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

// Only immutable rule boundaries and source slices cross documents here. Rule preparation has a
// separate bounded cache; media, ownership and CSSOM identities remain fresh for each sheet.
internal sealed class NativeCssSyntaxCache
{
    // An admin page alone links a dozen sheets (a framework, a theme, icon fonts, editors) and inlines more, which
    // every page of a site shares: evicting one splits it into rules again on the next page.
    internal const int MaxEntries = 256;
    internal const int MaxUnits = 32 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<string, CssRawRule[]> _rules = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();
    private int _units;

    internal CssRawRule[] Parse(string source, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(source);
        work.CheckCancellation();
        work.Charge(source.Length);
        if (source.Length > MaxUnits) return CssParser.ParseRuleList(source, work);
        CssRawRule[]? rules;
        lock (_gate) _rules.TryGetValue(source, out rules);
        if (rules is not null)
        {
            work.Charge(rules.Length);
            work.CheckCancellation();
            return rules;
        }

        rules = CssParser.ParseRuleList(source, work);
        // Bound both retained text and the number of rule records; oversized input remains uncached.
        var units = (long) source.Length + ((long) rules.Length * 64);
        work.CheckCancellation();
        if (units <= MaxUnits)
        {
            lock (_gate)
            {
                if (_rules.TryAdd(source, rules))
                {
                    _order.Enqueue(source);
                    _units += (int) units;
                    while (_rules.Count > MaxEntries || _units > MaxUnits)
                    {
                        var oldest = _order.Dequeue();
                        _units -= oldest.Length + (_rules[oldest].Length * 64);
                        _rules.Remove(oldest);
                    }
                }
            }
        }
        work.CheckCancellation();
        return rules;
    }
}
