using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    private RuleIndex? _index;
    private List<int>? _candidateBuffer;

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
        private readonly int[] _seen;
        private int _generation;

        internal RuleIndex(List<(CssStyleRule Rule, NativeCssOrigin Origin, long Order, string? NamespaceUri, NativeCssLayer Layer)> rules,
            CssValueWork work)
        {
            _seen = new int[rules.Count];
            var keys = new List<SelectorSubjectKey>();
            for (var i = 0; i < rules.Count; i++)
            {
                work.Charge(1);
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

        /// <summary>Fills <paramref name="candidates"/> with rule positions that may match <paramref name="element"/>, ascending.</summary>
        internal void Collect(Element element, List<int> candidates, CssValueWork work)
        {
            candidates.Clear();
            if (++_generation == int.MaxValue)
            {
                Array.Clear(_seen);
                _generation = 1;
            }
            var sorted = true;
            Add(candidates, _universal, ref sorted, work);
            if (_types.Count != 0 && _types.TryGetValue(element.LocalName, out var typed)) Add(candidates, typed, ref sorted, work);
            if (_ids.Count != 0 || _classes.Count != 0)
                for (uint i = 0; i < (uint) element.AttributeCount; i++)
                {
                    work.Charge(1);
                    var attribute = element.GetAttributeAt(i)!;
                    if (attribute.NamespaceUri is not null) continue;
                    if (attribute.LocalName == "id")
                    {
                        if (_ids.TryGetValue(attribute.Value, out var identified)) Add(candidates, identified, ref sorted, work);
                    }
                    else if (attribute.LocalName == "class" && _classes.Count != 0)
                        AddClasses(candidates, attribute.Value, ref sorted, work);
                }
            if (!sorted) candidates.Sort();
        }

        // DOM §2.3 ordered set parser: class names are separated by ASCII whitespace.
        private void AddClasses(List<int> candidates, string value, ref bool sorted, CssValueWork work)
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
                                Add(candidates, entry.Rules, ref sorted, work);
                                break;
                            }
                }
                start = end;
            }
        }

        private void Add(List<int> candidates, List<int> bucket, ref bool sorted, CssValueWork work)
        {
            foreach (var index in bucket)
            {
                if (_seen[index] == _generation) continue;
                _seen[index] = _generation;
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
}
