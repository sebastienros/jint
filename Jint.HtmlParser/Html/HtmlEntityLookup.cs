using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

// https://html.spec.whatwg.org/multipage/parsing.html#named-character-reference-state
// Retain the longest terminal match while advancing through an immutable trie.
internal sealed class HtmlEntityLookup
{
    private readonly record struct Entry(int FirstEdge, int EdgeCount, string? Value);
    private readonly record struct Edge(char Character, int Target);

    private readonly Entry[] _entries;
    private readonly Edge[] _edges;

    internal HtmlEntityLookup(Dictionary<string, string> values)
    {
        var children = new List<Dictionary<char, int>> { new() };
        var terminals = new List<string?> { null };
        foreach (var pair in values)
        {
            var state = 0;
            foreach (var character in pair.Key)
            {
                if (!children[state].TryGetValue(character, out var next))
                {
                    next = children.Count;
                    children[state].Add(character, next);
                    children.Add(new Dictionary<char, int>());
                    terminals.Add(null);
                }
                state = next;
            }
            terminals[state] = pair.Value;
        }

        _entries = new Entry[children.Count];
        _edges = new Edge[children.Count - 1];
        var offset = 0;
        for (var i = 0; i < children.Count; i++)
        {
            var edges = new List<KeyValuePair<char, int>>(children[i]);
            edges.Sort(static (left, right) => left.Key.CompareTo(right.Key));
            _entries[i] = new Entry(offset, edges.Count, terminals[i]);
            foreach (var edge in edges) _edges[offset++] = new Edge(edge.Key, edge.Value);
        }
    }

    internal bool TryAdvance(int state, char character, out int next, out string? value)
    {
        var entry = _entries[state];
        var low = entry.FirstEdge;
        var high = low + entry.EdgeCount;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var edge = _edges[middle];
            if (character < edge.Character) high = middle;
            else if (character > edge.Character) low = middle + 1;
            else
            {
                next = edge.Target;
                value = _entries[next].Value;
                return true;
            }
        }
        next = 0;
        value = null;
        return false;
    }
}
