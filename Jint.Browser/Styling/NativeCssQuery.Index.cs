using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    private RuleIndex? _index;
    private Dictionary<Element, AncestorFilter>? _ancestorFilters;
    private List<int>? _candidateBuffer;
    private int[]? _seenCandidates;
    private int _candidateGeneration;

    /// <summary>
    /// Style rules bucketed by the id, class or type name their selector's subject must carry, as in
    /// LightPanda's StyleManager. It narrows which rules are tried on an element; the matcher still decides.
    /// Keys compare case-insensitively, a superset of quirks-mode and HTML type-name matching, so the
    /// filter can only admit extra rules, never drop one.
    /// </summary>
    private sealed class RuleIndex
    {
        private readonly List<int> _universal = [];
        private readonly Dictionary<string, List<int>> _ids = new(StringComparer.OrdinalIgnoreCase);
        // Keyed by an ASCII-case-folded hash so a class token is looked up as a span, without a substring.
        private readonly Dictionary<int, ClassBucket> _classes = new();
        private readonly Dictionary<string, List<int>> _types = new(StringComparer.OrdinalIgnoreCase);

        // Per rule: the hashes of the keys each branch needs among the subject's ancestors (null: none can rule it
        // out), and whether its subjects are all, or none, pseudo-elements.
        private readonly int[][]?[] _ancestorKeys;
        private readonly bool[] _pseudoOnly;
        private readonly bool[] _noPseudo;

        internal RuleIndex(List<(CssStyleRule Rule, NativeCssOrigin Origin, long Order, string? NamespaceUri, NativeCssLayer Layer)> rules,
            CssValueWork work)
        {
            var keys = new List<SelectorSubjectKey>();
            var ancestors = new List<List<SelectorSubjectKey>>();
            _ancestorKeys = new int[][]?[rules.Count];
            _pseudoOnly = new bool[rules.Count];
            _noPseudo = new bool[rules.Count];
            for (var i = 0; i < rules.Count; i++)
            {
                work.Charge(1);
                var selector = rules[i].Rule.Selector;
                _pseudoOnly[i] = SelectorSubjectKeys.AllSubjectsArePseudoElements(selector);
                _noPseudo[i] = SelectorSubjectKeys.NoSubjectIsAPseudoElement(selector);
                ancestors.Clear();
                if (SelectorSubjectKeys.TryCollectAncestorKeys(selector, ancestors))
                {
                    var branches = new int[ancestors.Count][];
                    for (var b = 0; b < ancestors.Count; b++)
                    {
                        branches[b] = new int[ancestors[b].Count];
                        for (var k = 0; k < ancestors[b].Count; k++) branches[b][k] = Hash(ancestors[b][k].Name);
                    }

                    _ancestorKeys[i] = branches;
                }

                keys.Clear();
                if (!SelectorSubjectKeys.TryCollect(rules[i].Rule.Selector, keys))
                {
                    _universal.Add(i);
                    continue;
                }
                foreach (var key in keys)
                {
                    work.Charge(1);
                    List<int>? bucket;
                    if (key.Kind == SelectorSubjectKeyKind.Class) bucket = ClassBucketFor(key.Name);
                    else
                    {
                        var buckets = key.Kind == SelectorSubjectKeyKind.Id ? _ids : _types;
                        if (!buckets.TryGetValue(key.Name, out bucket)) buckets.Add(key.Name, bucket = []);
                    }
                    // A selector list may key one rule into the same bucket twice.
                    if (bucket.Count == 0 || bucket[^1] != i) bucket.Add(i);
                }
            }
        }

        /// <summary>
        /// Whether the rule at <paramref name="position"/> can match an element — or, when <paramref name="pseudo"/>,
        /// one of its pseudo-elements — whose ancestors are <paramref name="ancestors"/>: a pseudo-element rule never
        /// matches the element itself and an
        /// element rule never a pseudo-element, and a rule needing an ancestor key the element's ancestors lack
        /// cannot match — the selector filter browsers run before matching. A superset, like the index.
        /// </summary>
        internal bool Admits(int position, bool pseudo, in AncestorFilter ancestors)
        {
            if (pseudo ? _noPseudo[position] : _pseudoOnly[position]) return false;
            if (_ancestorKeys[position] is not { } branches) return true;
            foreach (var branch in branches)
            {
                var all = true;
                foreach (var key in branch)
                {
                    if (!ancestors.MayContain(key))
                    {
                        all = false;
                        break;
                    }
                }

                if (all) return true;
            }

            return false;
        }

        /// <summary>Folds <paramref name="element"/>'s own keys — type, id and classes — into <paramref name="filter"/>.</summary>
        internal static void AddKeys(ref AncestorFilter filter, Element element)
        {
            filter.Add(Hash(element.LocalName));
            for (uint i = 0; i < (uint) element.AttributeCount; i++)
            {
                var attribute = element.GetAttributeAt(i)!;
                if (attribute.NamespaceUri is not null) continue;
                if (attribute.LocalName == "id")
                {
                    if (attribute.Value.Length != 0) filter.Add(Hash(attribute.Value));
                }
                else if (attribute.LocalName == "class")
                {
                    var span = attribute.Value.AsSpan();
                    var start = 0;
                    while (start < span.Length)
                    {
                        while (start < span.Length && IsAsciiWhitespace(span[start])) start++;
                        var end = start;
                        while (end < span.Length && !IsAsciiWhitespace(span[end])) end++;
                        if (end > start) filter.Add(Hash(span[start..end]));
                        start = end;
                    }
                }
            }
        }

        /// <summary>Fills <paramref name="candidates"/> with rule positions that may match <paramref name="element"/>, ascending.</summary>
        internal void Collect(Element element, List<int> candidates, int[] seen, ref int generation, CssValueWork work)
        {
            candidates.Clear();
            if (++generation == int.MaxValue)
            {
                Array.Clear(seen);
                generation = 1;
            }
            var sorted = true;
            Add(candidates, _universal, seen, generation, ref sorted, work);
            if (_types.Count != 0 && _types.TryGetValue(element.LocalName, out var typed)) Add(candidates, typed, seen, generation, ref sorted, work);
            if (_ids.Count != 0 || _classes.Count != 0)
                for (uint i = 0; i < (uint) element.AttributeCount; i++)
                {
                    work.Charge(1);
                    var attribute = element.GetAttributeAt(i)!;
                    if (attribute.NamespaceUri is not null) continue;
                    if (attribute.LocalName == "id")
                    {
                        if (_ids.TryGetValue(attribute.Value, out var identified)) Add(candidates, identified, seen, generation, ref sorted, work);
                    }
                    else if (attribute.LocalName == "class" && _classes.Count != 0)
                        AddClasses(candidates, attribute.Value, seen, generation, ref sorted, work);
                }
            if (!sorted) candidates.Sort();
        }

        // DOM §2.3 ordered set parser: class names are separated by ASCII whitespace.
        private void AddClasses(List<int> candidates, string value, int[] seen, int generation, ref bool sorted, CssValueWork work)
        {
            var span = value.AsSpan();
            var start = 0;
            while (start < span.Length)
            {
                work.Charge(1);
                while (start < span.Length && IsAsciiWhitespace(span[start])) start++;
                var end = start;
                while (end < span.Length && !IsAsciiWhitespace(span[end])) end++;
                if (end > start)
                {
                    var token = span[start..end];
                    if (_classes.TryGetValue(Hash(token), out var entry))
                        for (; entry is not null; entry = entry.Next)
                            if (EqualsIgnoringAsciiCase(token, entry.Name))
                            {
                                Add(candidates, entry.Rules, seen, generation, ref sorted, work);
                                break;
                            }
                }
                start = end;
            }
        }

        private static void Add(List<int> candidates, List<int> bucket, int[] seen, int generation, ref bool sorted, CssValueWork work)
        {
            foreach (var index in bucket)
            {
                if (seen[index] == generation) continue;
                seen[index] = generation;
                if (candidates.Count != 0 && candidates[^1] > index) sorted = false;
                candidates.Add(index);
            }
            work.Charge(1);
        }

        private static bool IsAsciiWhitespace(char c) => c is ' ' or '\t' or '\n' or '\f' or '\r';

        private List<int> ClassBucketFor(string name)
        {
            var hash = Hash(name);
            _classes.TryGetValue(hash, out var head);
            for (var entry = head; entry is not null; entry = entry.Next)
                if (EqualsIgnoringAsciiCase(name, entry.Name)) return entry.Rules;
            var created = new ClassBucket(name, head);
            _classes[hash] = created;
            return created.Rules;
        }

        // Class matching is exact, or ASCII case-insensitive in quirks mode: folding ASCII only is a superset of both.
        private static int Hash(ReadOnlySpan<char> name)
        {
            var hash = 2166136261u;
            foreach (var c in name) hash = (hash ^ FoldAscii(c)) * 16777619u;
            return (int) hash;
        }

        private static bool EqualsIgnoringAsciiCase(ReadOnlySpan<char> left, string right)
        {
            if (left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++)
                if (left[i] != right[i] && FoldAscii(left[i]) != FoldAscii(right[i])) return false;
            return true;
        }

        private static char FoldAscii(char c) => c is >= 'A' and <= 'Z' ? (char) (c | 0x20) : c;

        private sealed class ClassBucket(string name, ClassBucket? next)
        {
            internal string Name { get; } = name;
            internal List<int> Rules { get; } = [];
            internal ClassBucket? Next { get; } = next;
        }
    }

    /// <summary>The ancestor filter of <paramref name="element"/>: its parent's, with the parent's own keys.</summary>
    private AncestorFilter AncestorsOf(Element element)
    {
        _ancestorFilters ??= new();
        if (_ancestorFilters.TryGetValue(element, out var known)) return known;
        var filter = default(AncestorFilter);
        if (element.ParentNode is Element parent)
        {
            filter = AncestorsOf(parent);
            RuleIndex.AddKeys(ref filter, parent);
        }

        _ancestorFilters[element] = filter;
        return filter;
    }

    /// <summary>
    /// A 256-bit Bloom filter of the ids, classes and type names of an element's ancestors: a key it lacks is
    /// certainly absent; one it has may be present.
    /// </summary>
    internal struct AncestorFilter
    {
        private ulong _a, _b, _c, _d;

        internal void Add(int hash)
        {
            Set(hash & 255);
            Set((hash >> 8) & 255);
        }

        internal readonly bool MayContain(int hash) => Has(hash & 255) && Has((hash >> 8) & 255);

        private void Set(int bit)
        {
            var mask = 1UL << (bit & 63);
            switch (bit >> 6)
            {
                case 0: _a |= mask; break;
                case 1: _b |= mask; break;
                case 2: _c |= mask; break;
                default: _d |= mask; break;
            }
        }

        private readonly bool Has(int bit)
        {
            var mask = 1UL << (bit & 63);
            return ((bit >> 6) switch { 0 => _a, 1 => _b, 2 => _c, _ => _d } & mask) != 0;
        }
    }
}
