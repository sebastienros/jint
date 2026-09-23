using System;
using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.4.3 and §13.2.6.4.7 (2026-09-22).
    // Prepared attributes are an owned copy of the creation token. Once an
    // entry takes the array, neither the tokenizer nor the builder changes it.
    private abstract class FormattingEntry;
    private sealed class FormattingMarker(FormattingMarker? previous) : FormattingEntry
    {
        internal FormattingMarker? Previous { get; } = previous;
    }

    private readonly record struct FormattingAttribute(string? NamespaceUri, string LocalName, string Value);

    private sealed class FormattingElementEntry(Element element, string name, ParserAttribute[] attributes,
        Dictionary<FormattingAttribute, int> attributeCounts, long attributeWork, ulong key,
        FormattingMarker? marker) : FormattingEntry
    {
        internal Element Element = element;
        internal LinkedListNode<FormattingEntry>? Node;
        internal readonly string Name = name;
        internal readonly ParserAttribute[] Attributes = attributes;
        internal readonly Dictionary<FormattingAttribute, int> AttributeCounts = attributeCounts;
        internal readonly long AttributeWork = attributeWork;
        internal readonly ulong Key = key;
        internal readonly FormattingMarker? Marker = marker;
    }

    private readonly LinkedList<FormattingEntry> _formatting = [];
    // The marker is part of the index: entries in an older scope must not
    // lengthen either lookup or removal in a younger scope.
    private readonly Dictionary<(FormattingMarker? Marker, ulong Key), List<FormattingElementEntry>> _formattingByKey = [];
    private readonly HashSet<Element> _openIdentity = new(ReferenceEqualityComparer.Instance);
    private FormattingMarker? _lastFormattingMarker;
    // Null is idle; otherwise this is the next entry examined or recreated.
    private LinkedListNode<FormattingEntry>? _reconstructionNode;
    private bool _reconstructionForward;
    private Dictionary<FormattingAttribute, int>? _preparedFormattingAttributeCounts;
    private ulong _preparedFormattingKey;
    private bool _preparedFormattingKeyInitialized;
    private Element? _pendingFormattingElement;
    private List<FormattingElementEntry>? _pendingFormattingCandidates;
    private int _pendingFormattingCandidateIndex;
    private int _pendingFormattingEquivalentCount;
    private FormattingElementEntry? _pendingFormattingEarliest;
    private Dictionary<FormattingAttribute, int>.Enumerator _pendingFormattingCompareCursor;
    private bool _pendingFormattingComparing;

    private void ResetFormattingToken()
    {
        _preparedFormattingAttributeCounts = null;
        _preparedFormattingKey = 0;
        _preparedFormattingKeyInitialized = false;
        _pendingFormattingElement = null;
        _pendingFormattingCandidates = null;
        _pendingFormattingCandidateIndex = 0;
        _pendingFormattingEquivalentCount = 0;
        _pendingFormattingEarliest = null;
        _pendingFormattingComparing = false;
    }

    private void PushFormattingMarker()
    {
        var marker = new FormattingMarker(_lastFormattingMarker);
        _formatting.AddLast(marker);
        _lastFormattingMarker = marker;
        Charge(1);
    }

    private bool TryClearFormattingToMarker()
    {
        var removed = false;
        while (_formatting.Last is { } last)
        {
            if (_remaining <= 0 && removed) return false;
            var entry = last.Value;
            _formatting.RemoveLast();
            if (entry is FormattingElementEntry element) UnindexFormatting(element);
            Charge(1);
            removed = true;
            if (entry is not FormattingMarker) continue;
            _lastFormattingMarker = ((FormattingMarker) entry).Previous;
            return true;
        }
        throw new InvalidOperationException("HTML formatting marker was not found.");
    }

    private bool TryAddFormattingElement()
    {
        var element = _pendingFormattingElement ?? throw new InvalidOperationException("No formatting start is pending.");
        InitializeFormattingKey();
        var attributes = _preparedAttributes ?? Array.Empty<ParserAttribute>();
        var attributeCounts = _preparedFormattingAttributeCounts ??= [];
        var bucketKey = (_lastFormattingMarker, _preparedFormattingKey);
        if (_pendingFormattingCandidates is null)
        {
            if (!_formattingByKey.TryGetValue(bucketKey, out var bucket))
                _formattingByKey[bucketKey] = bucket = [];
            _pendingFormattingCandidates = bucket;
        }
        var candidates = _pendingFormattingCandidates;

        // The bucket is only an index. A hash collision never establishes
        // equivalence, and the earliest of three equivalent entries is removed.
        var advanced = false;
        while (_pendingFormattingCandidateIndex < candidates.Count)
        {
            if (_remaining <= 0 && advanced) return false;
            var candidate = candidates[_pendingFormattingCandidateIndex];
            if (!_pendingFormattingComparing)
            {
                Charge(1);
                advanced = true;
                if (candidate.Element.NamespaceUri != element.NamespaceUri || candidate.Name != element.LocalName ||
                    candidate.Attributes.Length != attributes.Length)
                {
                    _pendingFormattingCandidateIndex++;
                    continue;
                }
                _pendingFormattingCompareCursor = attributeCounts.GetEnumerator();
                _pendingFormattingComparing = true;
            }

            while (_pendingFormattingComparing)
            {
                if (_remaining <= 0 && advanced) return false;
                if (!_pendingFormattingCompareCursor.MoveNext())
                {
                    _pendingFormattingEarliest ??= candidate;
                    _pendingFormattingEquivalentCount++;
                    _pendingFormattingCandidateIndex++;
                    _pendingFormattingComparing = false;
                    Charge(1);
                    advanced = true;
                    break;
                }
                var pair = _pendingFormattingCompareCursor.Current;
                var equal = candidate.AttributeCounts.TryGetValue(pair.Key, out var count) && count == pair.Value;
                Charge(1L + (pair.Key.NamespaceUri?.Length ?? 0) + pair.Key.LocalName.Length + pair.Key.Value.Length);
                advanced = true;
                if (equal) continue;
                _pendingFormattingCandidateIndex++;
                _pendingFormattingComparing = false;
            }
        }
        if (_remaining <= 0 && advanced) return false;

        var bookkeepingWork = 2L;
        if (_pendingFormattingEquivalentCount >= 3 && _pendingFormattingEarliest is { } earliest)
        {
            _formatting.Remove(earliest.Node!);
            candidates.Remove(earliest);
            bookkeepingWork += 2;
        }

        var entry = new FormattingElementEntry(element, element.LocalName, attributes, attributeCounts,
            _preparedAttributeWork, _preparedFormattingKey, _lastFormattingMarker);
        entry.Node = _formatting.AddLast(entry);
        candidates.Add(entry);
        ResetFormattingToken();
        Charge(bookkeepingWork);
        return true;
    }

    private void UnindexFormatting(FormattingElementEntry entry)
    {
        var bucketKey = (entry.Marker, entry.Key);
        var bucket = _formattingByKey[bucketKey];
        bucket.Remove(entry);
        if (bucket.Count == 0) _formattingByKey.Remove(bucketKey);
        Charge(1);
    }

    private bool TryReconstructFormatting()
    {
        if (_reconstructionNode is null)
        {
            if (_formatting.Last is null || _formatting.Last.Value is FormattingMarker ||
                _formatting.Last.Value is FormattingElementEntry last && _openIdentity.Contains(last.Element))
                return true;
            _reconstructionNode = _formatting.Last;
            _reconstructionForward = false;
        }

        // A quota-one call still advances by one scan or one committed
        // insertion after dispatch spent its unit. Save the cursor on yield.
        var advanced = false;
        while (_reconstructionNode is { } node)
        {
            if (_remaining <= 0 && advanced) return false;
            if (!_reconstructionForward)
            {
                if (node.Value is FormattingMarker ||
                    node.Value is FormattingElementEntry prior && _openIdentity.Contains(prior.Element))
                {
                    _reconstructionNode = node.Next;
                    _reconstructionForward = true;
                }
                else if (node.Previous is null)
                    _reconstructionForward = true;
                else
                    _reconstructionNode = node.Previous;
                Charge(1);
                advanced = true;
                continue;
            }

            var entry = (FormattingElementEntry) node.Value;
            // Check before creating or linking anything: depth failures must
            // leave this entry and the tree at their previous identities.
            var recreated = InsertElement(entry.Name, entry.Attributes, attributeWork: entry.AttributeWork);
            entry.Element = recreated;
            _reconstructionNode = node.Next;
            advanced = true;
        }
        _reconstructionNode = null;
        _reconstructionForward = false;
        return _remaining > 0 || !advanced;
    }

    private void InitializeFormattingKey()
    {
        if (_preparedFormattingKeyInitialized) return;
        var comparer = StringComparer.Ordinal;
        var name = _token.Name!;
        ulong key = (uint) comparer.GetHashCode(Namespaces.Html);
        key = key * 1099511628211UL ^ (uint) comparer.GetHashCode(name);
        key ^= (ulong) _token.Attributes.Count * 0x9e3779b97f4a7c15UL;
        _preparedFormattingKey = key;
        _preparedFormattingKeyInitialized = true;
        Charge(1L + name.Length);
    }

    private long PrepareFormattingAttribute(HtmlAttribute attribute)
    {
        InitializeFormattingKey();
        var key = new FormattingAttribute(null, attribute.Name, attribute.Value);
        var counts = _preparedFormattingAttributeCounts ??= [];
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;

        // Commutative folding ignores source attribute order. Exact multiset
        // comparison still runs for every candidate in this marker's bucket.
        var comparer = StringComparer.Ordinal;
        ulong part = (uint) comparer.GetHashCode(string.Empty);
        part = (part * 1099511628211UL) ^ (uint) comparer.GetHashCode(attribute.Name);
        part = (part * 1099511628211UL) ^ (uint) comparer.GetHashCode(attribute.Value);
        _preparedFormattingKey += (part ^ (part >> 29)) * 0x9e3779b97f4a7c15UL;
        return 1L + attribute.Name.Length + attribute.Value.Length;
    }
}
