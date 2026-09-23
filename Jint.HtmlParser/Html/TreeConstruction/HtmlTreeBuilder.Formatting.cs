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

    private sealed class FormattingElementEntry(Element element, string name, ParserAttribute[] attributes,
        ulong key, FormattingMarker? marker) : FormattingEntry
    {
        internal Element Element = element;
        internal LinkedListNode<FormattingEntry>? Node;
        internal readonly string Name = name;
        internal readonly ParserAttribute[] Attributes = attributes;
        internal readonly ulong Key = key;
        internal readonly FormattingMarker? Marker = marker;
    }

    private readonly LinkedList<FormattingEntry> _formatting = [];
    private readonly Dictionary<ulong, List<FormattingElementEntry>> _formattingByKey = [];
    private readonly HashSet<Element> _openIdentity = new(ReferenceEqualityComparer.Instance);
    private FormattingMarker? _lastFormattingMarker;
    // Null is idle; otherwise this is the next entry examined or recreated.
    private LinkedListNode<FormattingEntry>? _reconstructionNode;
    private bool _reconstructionForward;

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

    private void AddFormattingElement(Element element)
    {
        var attributes = _preparedAttributes ?? Array.Empty<ParserAttribute>();
        var key = FormattingKey(element.NamespaceUri, element.LocalName, attributes);
        if (!_formattingByKey.TryGetValue(key, out var candidates))
            _formattingByKey[key] = candidates = [];

        // The bucket is only an index. A hash collision never establishes
        // equivalence, and the earliest of three equivalent entries is removed.
        var equivalent = 0;
        FormattingElementEntry? earliest = null;
        foreach (var candidate in candidates)
        {
            Charge(1);
            if (!ReferenceEquals(candidate.Marker, _lastFormattingMarker) ||
                !EquivalentFormatting(candidate, element.NamespaceUri, element.LocalName, attributes)) continue;
            earliest ??= candidate;
            equivalent++;
        }
        if (equivalent >= 3 && earliest is not null)
        {
            _formatting.Remove(earliest.Node!);
            candidates.Remove(earliest);
            Charge(2);
        }

        var entry = new FormattingElementEntry(element, element.LocalName, attributes, key, _lastFormattingMarker);
        entry.Node = _formatting.AddLast(entry);
        candidates.Add(entry);
        Charge(2);
    }

    private void UnindexFormatting(FormattingElementEntry entry)
    {
        var bucket = _formattingByKey[entry.Key];
        bucket.Remove(entry);
        if (bucket.Count == 0) _formattingByKey.Remove(entry.Key);
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
            var recreated = InsertElement(entry.Name, entry.Attributes);
            entry.Element = recreated;
            _reconstructionNode = node.Next;
            advanced = true;
        }
        _reconstructionNode = null;
        _reconstructionForward = false;
        return true;
    }

    private ulong FormattingKey(string? namespaceUri, string name, ParserAttribute[] attributes)
    {
        // Commutative attribute folding makes order irrelevant to the index.
        // Exact collision-checked comparison below is authoritative.
        var comparer = StringComparer.Ordinal;
        ulong key = (uint) comparer.GetHashCode(namespaceUri ?? string.Empty);
        key = key * 1099511628211UL ^ (uint) comparer.GetHashCode(name);
        key ^= (ulong) attributes.Length * 0x9e3779b97f4a7c15UL;
        foreach (var attribute in attributes)
        {
            ulong part = (uint) comparer.GetHashCode(attribute.NamespaceUri ?? string.Empty);
            part = (part * 1099511628211UL) ^ (uint) comparer.GetHashCode(attribute.LocalName);
            part = (part * 1099511628211UL) ^ (uint) comparer.GetHashCode(attribute.Value);
            key += (part ^ (part >> 29)) * 0x9e3779b97f4a7c15UL;
            Charge(1L + (attribute.NamespaceUri?.Length ?? 0) + attribute.LocalName.Length + attribute.Value.Length);
        }
        return key;
    }

    private bool EquivalentFormatting(FormattingElementEntry entry, string? namespaceUri, string name,
        ParserAttribute[] attributes)
    {
        if (entry.Element.NamespaceUri != namespaceUri || entry.Name != name ||
            entry.Attributes.Length != attributes.Length) return false;
        var counts = new Dictionary<(string? NamespaceUri, string LocalName, string Value), int>(entry.Attributes.Length);
        foreach (var left in entry.Attributes)
        {
            var key = (left.NamespaceUri, left.LocalName, left.Value);
            counts.TryGetValue(key, out var count);
            counts[key] = count + 1;
            Charge(1L + (left.NamespaceUri?.Length ?? 0) + left.LocalName.Length + left.Value.Length);
        }
        foreach (var right in attributes)
        {
            var key = (right.NamespaceUri, right.LocalName, right.Value);
            if (!counts.TryGetValue(key, out var count) || count == 0) return false;
            counts[key] = count - 1;
            Charge(1L + (right.NamespaceUri?.Length ?? 0) + right.LocalName.Length + right.Value.Length);
        }
        return true;
    }
}
