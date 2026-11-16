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
    internal const int MaxEntries = 32768;
    internal const int MaxUnits = 32 * 1024 * 1024;
    internal const int MaxRuleLength = 64 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<Key, Entry> _entries = new(KeyComparer.Instance);
    private readonly Queue<Key> _order = new();

    // Each source text is retained once, however many of its rules are cached, and charged once.
    private readonly Dictionary<string, int> _sources = new(ReferenceEqualityComparer.Instance);
    private int _units;

    private readonly record struct Key(string Text, int Start, int Length)
    {
        internal ReadOnlySpan<char> Rule => Text.AsSpan(Start, Length);
    }

    // A rule parses from its own text alone, so it is keyed by that text and its offset, in whichever instance of
    // its sheet's source: every document fetches its sheets anew. Hashing a whole style sheet for each of its
    // rules would cost more than parsing them, so only the rule's span is hashed and compared.
    private sealed class KeyComparer : IEqualityComparer<Key>
    {
        internal static readonly KeyComparer Instance = new();

        // The same offset too: a compiled selector keeps source positions, which must stay the ones of this text.
        public bool Equals(Key x, Key y)
            => x.Start == y.Start && x.Length == y.Length && (ReferenceEquals(x.Text, y.Text) || x.Rule.SequenceEqual(y.Rule));

        public int GetHashCode(Key key) => HashCode.Combine(string.GetHashCode(key.Rule), key.Start);
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
        // The sheet's source and the rule's span in it: CssSourceText.Text would copy the rule's text out.
        var key = new Key(raw.Text.Source, raw.Text.Span.Start, raw.Text.Span.Length);
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
