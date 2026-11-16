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

    // The bodies of grouping rules — @media, @supports, @layer, @container — split as the sheet's own rule list does,
    // once for every copy of the sheet a site's pages fetch. A body's rules keep the offsets of the sheet they were
    // split from, so a copy shares them only at the same offset and with the same text, which every span they hold
    // then covers in that copy too.
    private const int MaxBodyEntries = 8192;
    private readonly Dictionary<BodyKey, CssRawRule[]> _bodies = new(BodyKeyComparer.Instance);
    private readonly Queue<BodyKey> _bodyOrder = new();

    // Each sheet's text is retained once, however many of its bodies are cached, and charged once.
    private readonly Dictionary<string, int> _bodySources = new(ReferenceEqualityComparer.Instance);
    private long _bodyUnits;

    private readonly record struct BodyKey(string Source, int Start, int Length, CssRuleBodyKind Kind, bool IsClosed)
    {
        internal ReadOnlySpan<char> Text => Source.AsSpan(Start, Length);
    }

    private sealed class BodyKeyComparer : IEqualityComparer<BodyKey>
    {
        internal static readonly BodyKeyComparer Instance = new();

        public bool Equals(BodyKey x, BodyKey y)
            => x.Start == y.Start && x.Length == y.Length && x.Kind == y.Kind && x.IsClosed == y.IsClosed
                && (ReferenceEquals(x.Source, y.Source) || x.Text.SequenceEqual(y.Text));

        public int GetHashCode(BodyKey key) => HashCode.Combine(string.GetHashCode(key.Text), key.Start, key.Kind);
    }

    internal CssRawRule[] ParseBody(CssRuleBody body, CssValueWork work)
    {
        work.CheckCancellation();
        var text = body.Text;
        work.Charge(text.Span.Length);
        if (text.Span.Length > MaxUnits) return CssParser.ParseRuleList(body, work);
        var key = new BodyKey(text.Source, text.Span.Start, text.Span.Length, body.Kind, body.IsClosed);
        CssRawRule[]? rules;
        lock (_gate) _bodies.TryGetValue(key, out rules);
        if (rules is not null)
        {
            work.Charge(rules.Length);
            work.CheckCancellation();
            return rules;
        }

        rules = CssParser.ParseRuleList(body, work);
        var units = (long) text.Span.Length + ((long) rules.Length * 64);
        work.CheckCancellation();
        if (units <= MaxUnits)
        {
            lock (_gate)
            {
                if (_bodies.TryAdd(key, rules))
                {
                    _bodyOrder.Enqueue(key);
                    _bodyUnits += units;
                    if (_bodySources.TryGetValue(key.Source, out var references))
                    {
                        _bodySources[key.Source] = references + 1;
                    }
                    else
                    {
                        _bodySources.Add(key.Source, 1);
                        _bodyUnits += key.Source.Length;
                    }

                    while (_bodies.Count > MaxBodyEntries || _bodyUnits > MaxUnits)
                    {
                        var oldest = _bodyOrder.Dequeue();
                        _bodyUnits -= oldest.Length + ((long) _bodies[oldest].Length * 64);
                        _bodies.Remove(oldest);
                        if (_bodySources[oldest.Source] == 1)
                        {
                            _bodySources.Remove(oldest.Source);
                            _bodyUnits -= oldest.Source.Length;
                        }
                        else
                        {
                            _bodySources[oldest.Source]--;
                        }
                    }
                }
            }
        }

        work.CheckCancellation();
        return rules;
    }
}
