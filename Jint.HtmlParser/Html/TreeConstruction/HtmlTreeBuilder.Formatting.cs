using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.4.3 and §13.2.6.4.7 (2026-09-22).
    // Prepared attributes are an owned copy of the creation token. Once an
    // entry takes the array, neither the tokenizer nor the builder changes it.
    // The list and both of its indexes are intrusive: an entry is its own link in each of them,
    // so pushing a formatting element allocates the entry and nothing else.
    private abstract class FormattingEntry
    {
        internal FormattingEntry? ListPrevious;
        internal FormattingEntry? ListNext;
    }

    private sealed class FormattingMarker(FormattingMarker? previous) : FormattingEntry
    {
        internal FormattingMarker? Previous { get; } = previous;
        // The newest entry of each formatting name in this marker's scope.
        internal FormattingElementEntry?[]? NameBuckets;
    }

    // HTML Standard §13.2.4.3: the formatting elements are exactly these fourteen HTML names, so the
    // per-scope name index is a fixed array rather than a dictionary hashing a (marker, name) key.
    private const int FormattingNameCount = 14;

    private static int FormattingNameIndex(string name) => name switch
    {
        "a" => 0,
        "b" => 1,
        "big" => 2,
        "code" => 3,
        "em" => 4,
        "font" => 5,
        "i" => 6,
        "nobr" => 7,
        "s" => 8,
        "small" => 9,
        "strike" => 10,
        "strong" => 11,
        "tt" => 12,
        "u" => 13,
        _ => -1
    };

    // Up to this many attributes, entries are compared by scanning their attribute arrays; larger
    // sets get a counted map so hostile input cannot make each comparison quadratic.
    private const int CountedFormattingAttributeThreshold = 8;

    private readonly record struct FormattingAttribute(string? NamespaceUri, string LocalName, string Value);

    private sealed class FormattingElementEntry(Element element, string name, ParserAttribute[] attributes,
        Dictionary<FormattingAttribute, int>? attributeCounts, long attributeWork, ulong key,
        FormattingMarker? marker) : FormattingEntry
    {
        internal Element Element = element;
        internal FormattingElementEntry? NamePrevious;
        internal FormattingElementEntry? NameNext;
        internal FormattingElementEntry? KeyPrevious;
        internal FormattingElementEntry? KeyNext;
        internal readonly string Name = name;
        internal readonly ParserAttribute[] Attributes = attributes;
        // Null up to CountedFormattingAttributeThreshold attributes.
        internal readonly Dictionary<FormattingAttribute, int>? AttributeCounts = attributeCounts;
        internal readonly long AttributeWork = attributeWork;
        internal readonly ulong Key = key;
        internal readonly FormattingMarker? Marker = marker;
    }

    private struct FormattingKeyChain
    {
        internal FormattingElementEntry? First;
        internal FormattingElementEntry? Last;
    }

    private FormattingEntry? _formattingLast;
    private int _formattingCount;
    private readonly Dictionary<Element, FormattingElementEntry> _formattingByElement = new(ReferenceEqualityComparer.Instance);
    // Name buckets of the scope before any marker; a marker's own buckets live on the marker.
    private readonly FormattingElementEntry?[] _rootFormattingNames = new FormattingElementEntry?[FormattingNameCount];
    // The marker is part of the index: entries in an older scope must not
    // lengthen either lookup or removal in a younger scope.
    private readonly Dictionary<(FormattingMarker? Marker, ulong Key), FormattingKeyChain> _formattingByKey = [];
    private readonly HashSet<Element> _openIdentity = new(ReferenceEqualityComparer.Instance);
    private FormattingMarker? _lastFormattingMarker;
    // Null is idle; otherwise this is the next entry examined or recreated.
    private FormattingEntry? _reconstructionNode;
    private bool _reconstructionForward;
    private Dictionary<FormattingAttribute, int>? _preparedFormattingAttributeCounts;
    private ulong _preparedFormattingKey;
    private bool _preparedFormattingKeyInitialized;
    private Element? _pendingFormattingElement;
    private bool _pendingFormattingCandidatesStarted;
    private FormattingElementEntry? _pendingFormattingCandidate;
    private int _pendingFormattingEquivalentCount;
    private FormattingElementEntry? _pendingFormattingEarliest;
    private Dictionary<FormattingAttribute, int>.Enumerator _pendingFormattingCompareCursor;
    private int _pendingFormattingCompareIndex;
    private bool _pendingFormattingComparing;

    private void ResetFormattingToken()
    {
        _preparedFormattingAttributeCounts = null;
        _preparedFormattingKey = 0;
        _preparedFormattingKeyInitialized = false;
        _pendingFormattingElement = null;
        _pendingFormattingCandidatesStarted = false;
        _pendingFormattingCandidate = null;
        _pendingFormattingEquivalentCount = 0;
        _pendingFormattingEarliest = null;
        _pendingFormattingComparing = false;
    }

    private void PushFormattingMarker()
    {
        var marker = new FormattingMarker(_lastFormattingMarker);
        InsertFormattingBefore(null, marker);
        _lastFormattingMarker = marker;
        Charge(1);
    }

    private bool TryClearFormattingToMarker()
    {
        var removed = false;
        while (_formattingLast is { } entry)
        {
            if (_remaining <= 0 && removed) return false;
            UnlinkFormatting(entry);
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
        ReadOnlySpan<ParserAttribute> attributes = PreparedAttributes;
        var attributeCounts = _preparedFormattingAttributeCounts;
        var bucketKey = (_lastFormattingMarker, _preparedFormattingKey);
        if (!_pendingFormattingCandidatesStarted)
        {
            _pendingFormattingCandidate = _formattingByKey.TryGetValue(bucketKey, out var chain) ? chain.First : null;
            _pendingFormattingCandidatesStarted = true;
        }

        // The bucket is only an index. A hash collision never establishes
        // equivalence, and the earliest of three equivalent entries is removed.
        var advanced = false;
        while (_pendingFormattingCandidate is { } candidate)
        {
            if (_remaining <= 0 && advanced) return false;
            if (!_pendingFormattingComparing)
            {
                Charge(1);
                advanced = true;
                if (candidate.Element.NamespaceUri != element.NamespaceUri || candidate.Name != element.LocalName ||
                    candidate.Attributes.Length != attributes.Length)
                {
                    _pendingFormattingCandidate = candidate.KeyNext;
                    continue;
                }
                if (attributeCounts is not null) _pendingFormattingCompareCursor = attributeCounts.GetEnumerator();
                _pendingFormattingCompareIndex = 0;
                _pendingFormattingComparing = true;
            }

            while (_pendingFormattingComparing)
            {
                if (_remaining <= 0 && advanced) return false;
                if (!TryCompareNextFormattingAttribute(candidate, attributes, attributeCounts, out var equal, out var work))
                {
                    _pendingFormattingEarliest ??= candidate;
                    _pendingFormattingEquivalentCount++;
                    _pendingFormattingCandidate = candidate.KeyNext;
                    _pendingFormattingComparing = false;
                    Charge(1);
                    advanced = true;
                    break;
                }
                Charge(work);
                advanced = true;
                if (equal) continue;
                _pendingFormattingCandidate = candidate.KeyNext;
                _pendingFormattingComparing = false;
            }
        }
        if (_remaining <= 0 && advanced) return false;

        var bookkeepingWork = 3L;
        if (_pendingFormattingEquivalentCount >= 3 && _pendingFormattingEarliest is { } earliest)
        {
            UnlinkFormatting(earliest);
            _formattingByElement.Remove(earliest.Element);
            UnindexFormattingName(earliest);
            UnindexFormattingKey(earliest);
            bookkeepingWork += 4;
        }

        var entry = new FormattingElementEntry(element, element.LocalName, attributes.ToArray(), attributeCounts,
            _preparedAttributeWork, _preparedFormattingKey, _lastFormattingMarker);
        InsertFormattingBefore(null, entry);
        _formattingByElement.Add(element, entry);
        IndexFormattingName(entry);
        IndexFormattingKey(entry);
        ResetFormattingToken();
        Charge(bookkeepingWork);
        return true;
    }

    // Compares one attribute of the pending token against a candidate with the same attribute
    // count; false once every attribute has been compared equal.
    private bool TryCompareNextFormattingAttribute(FormattingElementEntry candidate,
        ReadOnlySpan<ParserAttribute> attributes, Dictionary<FormattingAttribute, int>? attributeCounts,
        out bool equal, out long work)
    {
        if (attributeCounts is not null)
        {
            if (!_pendingFormattingCompareCursor.MoveNext())
            {
                equal = true;
                work = 0;
                return false;
            }
            var pair = _pendingFormattingCompareCursor.Current;
            equal = candidate.AttributeCounts!.TryGetValue(pair.Key, out var count) && count == pair.Value;
            work = 1L + (pair.Key.NamespaceUri?.Length ?? 0) + pair.Key.LocalName.Length + pair.Key.Value.Length;
            return true;
        }

        if (_pendingFormattingCompareIndex == attributes.Length)
        {
            equal = true;
            work = 0;
            return false;
        }
        var attribute = attributes[_pendingFormattingCompareIndex++];
        equal = CountFormattingAttribute(candidate.Attributes, attribute) == CountFormattingAttribute(attributes, attribute);
        work = 1L + (attribute.NamespaceUri?.Length ?? 0) + attribute.LocalName.Length + attribute.ValueSlice.Length;
        return true;
    }

    private static int CountFormattingAttribute(ReadOnlySpan<ParserAttribute> attributes, in ParserAttribute attribute)
    {
        var count = 0;
        foreach (ref readonly var item in attributes)
        {
            if (item.NamespaceUri == attribute.NamespaceUri && item.LocalName == attribute.LocalName &&
                item.ValueSlice.Span.SequenceEqual(attribute.ValueSlice.Span))
                count++;
        }
        return count;
    }

    private void InsertFormattingBefore(FormattingEntry? next, FormattingEntry entry)
    {
        var previous = next is null ? _formattingLast : next.ListPrevious;
        entry.ListPrevious = previous;
        entry.ListNext = next;
        if (previous is not null) previous.ListNext = entry;
        if (next is null) _formattingLast = entry;
        else next.ListPrevious = entry;
        _formattingCount++;
    }

    private void UnlinkFormatting(FormattingEntry entry)
    {
        var previous = entry.ListPrevious;
        var next = entry.ListNext;
        if (previous is not null) previous.ListNext = next;
        if (next is null) _formattingLast = previous;
        else next.ListPrevious = previous;
        entry.ListPrevious = null;
        entry.ListNext = null;
        _formattingCount--;
    }

    internal int FormattingEntryCount => _formattingCount;

    private void IndexFormattingKey(FormattingElementEntry entry)
    {
        ref var chain = ref CollectionsMarshal.GetValueRefOrAddDefault(_formattingByKey, (entry.Marker, entry.Key), out _);
        entry.KeyPrevious = chain.Last;
        entry.KeyNext = null;
        if (chain.Last is null) chain.First = entry;
        else chain.Last.KeyNext = entry;
        chain.Last = entry;
    }

    private void UnindexFormattingKey(FormattingElementEntry entry)
    {
        var bucketKey = (entry.Marker, entry.Key);
        ref var chain = ref CollectionsMarshal.GetValueRefOrNullRef(_formattingByKey, bucketKey);
        var previous = entry.KeyPrevious;
        var next = entry.KeyNext;
        if (previous is null) chain.First = next;
        else previous.KeyNext = next;
        if (next is null) chain.Last = previous;
        else next.KeyPrevious = previous;
        entry.KeyPrevious = null;
        entry.KeyNext = null;
        if (chain.First is null) _formattingByKey.Remove(bucketKey);
    }

    private void UnindexFormatting(FormattingElementEntry entry)
    {
        _formattingByElement.Remove(entry.Element);
        UnindexFormattingName(entry);
        UnindexFormattingKey(entry);
        Charge(3);
    }

    private FormattingElementEntry?[] FormattingNameBuckets(FormattingMarker? marker)
        => marker is null ? _rootFormattingNames : marker.NameBuckets ??= new FormattingElementEntry?[FormattingNameCount];

    /// <summary>The newest entry named <paramref name="name"/> after the last marker, or null.</summary>
    private FormattingElementEntry? LastFormattingEntryNamed(string name)
    {
        var index = FormattingNameIndex(name);
        if (index < 0) return null;
        return (_lastFormattingMarker is null ? _rootFormattingNames : _lastFormattingMarker.NameBuckets)?[index];
    }

    private void IndexFormattingName(FormattingElementEntry entry)
    {
        ref var last = ref FormattingNameBuckets(entry.Marker)[FormattingNameIndex(entry.Name)];
        entry.NamePrevious = last;
        entry.NameNext = null;
        if (last is not null) last.NameNext = entry;
        last = entry;
    }

    private void UnindexFormattingName(FormattingElementEntry entry)
    {
        ref var last = ref FormattingNameBuckets(entry.Marker)[FormattingNameIndex(entry.Name)];
        var previous = entry.NamePrevious;
        var next = entry.NameNext;
        if (previous is not null) previous.NameNext = next;
        if (next is null) last = previous;
        else next.NamePrevious = previous;
        entry.NamePrevious = null;
        entry.NameNext = null;
    }

    private bool TryReconstructFormatting()
    {
        if (_reconstructionNode is null)
        {
            if (_formattingLast is null or FormattingMarker ||
                _formattingLast is FormattingElementEntry last && _openIdentity.Contains(last.Element))
                return true;
            _reconstructionNode = _formattingLast;
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
                if (node is FormattingMarker ||
                    node is FormattingElementEntry prior && _openIdentity.Contains(prior.Element))
                {
                    _reconstructionNode = node.ListNext;
                    _reconstructionForward = true;
                }
                else if (node.ListPrevious is null)
                    _reconstructionForward = true;
                else
                    _reconstructionNode = node.ListPrevious;
                Charge(1);
                advanced = true;
                continue;
            }

            var entry = (FormattingElementEntry) node;
            // Check before creating or linking anything: depth failures must
            // leave this entry and the tree at their previous identities.
            var recreated = InsertElement(entry.Name, entry.Attributes, attributeWork: entry.AttributeWork,
                isValue: entry.Element.IsValue);
            _formattingByElement.Remove(entry.Element);
            entry.Element = recreated;
            _formattingByElement.Add(recreated, entry);
            Charge(1);
            _reconstructionNode = node.ListNext;
            advanced = true;
            if (_customElementReactionsBoundary) return false;
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
        key ^= (ulong) _token.Attributes.Length * 0x9e3779b97f4a7c15UL;
        _preparedFormattingKey = key;
        _preparedFormattingKeyInitialized = true;
        Charge(1L + name.Length);
    }

    private long PrepareFormattingAttribute(HtmlAttribute attribute)
    {
        InitializeFormattingKey();
        var key = new FormattingAttribute(null, attribute.Name, attribute.Value);
        if (_token.Attributes.Length > CountedFormattingAttributeThreshold)
        {
            var counts = _preparedFormattingAttributeCounts ??= [];
            counts.TryGetValue(key, out var count);
            counts[key] = count + 1;
        }

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
