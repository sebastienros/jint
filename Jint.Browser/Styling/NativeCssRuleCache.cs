using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

// CSSOM §6.4.3: preparation may share selector programs and immutable declaration entries,
// never a live rule, parent, sheet, media decision or matching result.
internal sealed class NativeCssRuleCache
{
    // A Bootstrap-based admin theme alone has several thousand rules; every page of a site shares them.
    private const int MaxEntries = 32768;
    private const int MaxUnits = 32 * 1024 * 1024;
    private const int MaxRuleLength = 64 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<Key, Entry> _entries = new(KeyComparer.Instance);
    private readonly Queue<Key> _order = new();

    // Each source text is retained once, however many of its rules are cached, and charged once.
    private readonly Dictionary<string, int> _sources = new(ReferenceEqualityComparer.Instance);
    private int _units;

    private readonly record struct Key(string Text, int Start);

    // A rule is its source text and its offset in it. The texts are the syntax cache's own instances, shared by
    // every sheet parsed from the same source, so identity is enough — and hashing a whole style sheet for each
    // of its rules would cost more than parsing them.
    private sealed class KeyComparer : IEqualityComparer<Key>
    {
        internal static readonly KeyComparer Instance = new();

        public bool Equals(Key x, Key y) => ReferenceEquals(x.Text, y.Text) && x.Start == y.Start;

        public int GetHashCode(Key key) => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key.Text), key.Start);
    }

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
        // Charge the rule's own text and expanded declarations; its source is charged once, below.
        // Oversized rules still parse.
        var units = ((long) raw.Text.Span.Length * 8) + ((long) style.Style.Count * 128) + 256;
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
                    if (_sources.TryGetValue(key.Text, out var references))
                    {
                        _sources[key.Text] = references + 1;
                    }
                    else
                    {
                        _sources.Add(key.Text, 1);
                        _units += key.Text.Length * 2;
                    }

                    while (_entries.Count > MaxEntries || _units > MaxUnits)
                    {
                        var oldest = _order.Dequeue();
                        _units -= _entries[oldest].Units;
                        _entries.Remove(oldest);
                        if (_sources[oldest.Text] == 1)
                        {
                            _sources.Remove(oldest.Text);
                            _units -= oldest.Text.Length * 2;
                        }
                        else
                        {
                            _sources[oldest.Text]--;
                        }
                    }
                }
            }
        }
        work.CheckCancellation();
        return parsed;
    }
}
