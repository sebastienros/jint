namespace Jint.HtmlParser.Serialization;

// DOM Parsing §5.2.1.1.2. Effective bindings have reversible changes, so an
// element exit costs only its own bindings rather than copying an ancestor map.
internal sealed class XmlNamespaceScope
{
    private sealed class Binding(string prefix, string uri, LinkedListNode<string> candidate)
    {
        internal string Prefix = prefix;
        internal string Uri = uri;
        internal LinkedListNode<string> Candidate = candidate;
    }

    private readonly record struct Change(string Prefix, Binding? Previous,
        LinkedListNode<string>? PreviousNext);

    private readonly SerializationWork _work;
    private readonly Dictionary<string, Binding> _prefixes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LinkedList<string>> _uris = new(StringComparer.Ordinal);
    private readonly List<Change> _changes = [];
    private long _prefixIndex = 1;

    internal XmlNamespaceScope(SerializationWork work)
    {
        _work = work;
        Bind("xml", Namespaces.Xml);
        _changes.Clear();
    }

    internal int Boundary => _changes.Count;

    internal string? Effective(string prefix)
    {
        _work.Charge(prefix.Length + 1, SerializationStage.Scan);
        return _prefixes.TryGetValue(prefix, out var binding) ? binding.Uri : null;
    }

    internal string? Preferred(string uri, string? preferred)
    {
        _work.Charge(uri.Length + (preferred?.Length ?? 0) + 1, SerializationStage.Scan);
        if (preferred is not null && _prefixes.TryGetValue(preferred, out var binding) && binding.Uri == uri)
        {
            return preferred;
        }

        return _uris.TryGetValue(uri, out var candidates) ? candidates.Last?.Value : null;
    }

    internal void Bind(string prefix, string uri)
    {
        _work.Charge(prefix.Length + uri.Length + 1, SerializationStage.Scan);
        if (_prefixes.TryGetValue(prefix, out var previous) && previous.Uri == uri) return;
        LinkedListNode<string>? previousNext = null;
        if (previous is not null)
        {
            previousNext = previous.Candidate.Next;
            previous.Candidate.List!.Remove(previous.Candidate);
        }

        _work.Poll(SerializationStage.Scan);
        if (!_uris.TryGetValue(uri, out var candidates)) _uris.Add(uri, candidates = new LinkedList<string>());
        var candidate = candidates.AddLast(prefix);
        _prefixes[prefix] = new Binding(prefix, uri, candidate);
        _changes.Add(new Change(prefix, previous, previousNext));
        _work.Poll(SerializationStage.Scan);
    }

    internal string Generate(string uri, HashSet<string> localReserved)
    {
        while (true)
        {
            _work.Charge(1, SerializationStage.Scan);
            var prefix = "ns" + _prefixIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _prefixIndex = checked(_prefixIndex + 1);
            _work.Charge(prefix.Length * 2 + uri.Length, SerializationStage.Scan);
            if (_prefixes.ContainsKey(prefix) || localReserved.Contains(prefix)) continue;
            return prefix;
        }
    }

    internal void Restore(int boundary)
    {
        for (var index = _changes.Count - 1; index >= boundary; index--)
        {
            _work.Charge(1, SerializationStage.Scan);
            var change = _changes[index];
            var current = _prefixes[change.Prefix];
            current.Candidate.List!.Remove(current.Candidate);
            if (change.Previous is null)
            {
                _prefixes.Remove(change.Prefix);
                continue;
            }

            var list = _uris[change.Previous.Uri];
            if (change.PreviousNext is { List: not null } next)
            {
                list.AddBefore(next, change.Previous.Candidate);
            }
            else
            {
                list.AddLast(change.Previous.Candidate);
            }

            _prefixes[change.Prefix] = change.Previous;
        }

        _changes.RemoveRange(boundary, _changes.Count - boundary);
    }
}
