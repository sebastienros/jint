using System;
using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.4.7, adoption agency algorithm (2026-09-22).
    // Each scan and move is a separate continuation point. The outer limit of
    // eight is prescribed by HTML and has no relation to the Drive work quota.
    private enum AdoptionStage
    {
        Idle, Outer, FindFormatting, FindOpen, FindScope, FindFurthest,
        PopWithoutBlock, Inner, RemoveInnerOpen, RecreateInner, ResolveMove, CheckMove, MoveLast,
        CreateReplacement, TransferChildren, AppendReplacement,
        ReplaceFormatting, RemoveFormattingOpen, InsertReplacementOpen,
        GenericFind, GenericImplied, GenericPop
    }

    private AdoptionStage _adoptionStage;
    private string? _adoptionSubject;
    private int _adoptionOuter;
    private int _adoptionInner;
    private LinkedListNode<FormattingEntry>? _adoptionCursor;
    private LinkedListNode<FormattingEntry>? _adoptionBookmarkBefore;
    private FormattingElementEntry? _adoptionFormatting;
    private Element? _adoptionCommonAncestor;
    private Element? _adoptionFurthestBlock;
    private Element? _adoptionLastNode;
    private Element? _adoptionReplacement;
    private Node? _adoptionTransferCursor;
    private InsertionLocation? _adoptionMoveLocation;
    private Node? _adoptionMoveAncestor;
    private bool _adoptionMoveBlocked;
    private int _adoptionScan;
    private int _adoptionFormattingIndex;
    private int _adoptionFurthestIndex;
    private int _adoptionNodeIndex;
    private int _adoptionGenericTarget;
    private int _adoptionShiftIndex = -1;
    private int _adoptionInsertIndex = -1;
    private int _adoptionInsertCursor;

    private enum SpecialFormattingStartStage
    {
        Idle, FindAnchor, AdoptAnchor, CleanupAnchorList, CleanupAnchorOpen,
        Reconstruct, CheckNobr, AdoptNobr, ReconstructNobr, Insert
    }

    private SpecialFormattingStartStage _specialFormattingStartStage;
    private LinkedListNode<FormattingEntry>? _specialFormattingCursor;
    private Element? _specialOldAnchor;
    private int _specialOpenScan;

    private bool TrySpecialFormattingStart(string name)
    {
        if (_specialFormattingStartStage == SpecialFormattingStartStage.Idle)
        {
            _specialFormattingStartStage = name == "a"
                ? SpecialFormattingStartStage.FindAnchor : SpecialFormattingStartStage.Reconstruct;
            _specialFormattingCursor = _formatting.Last;
        }
        var advanced = false;
        while (true)
        {
            if (_remaining <= 0 && advanced) return false;
            switch (_specialFormattingStartStage)
            {
                case SpecialFormattingStartStage.FindAnchor:
                    if (_specialFormattingCursor is null || _specialFormattingCursor.Value is FormattingMarker)
                    {
                        _specialFormattingStartStage = SpecialFormattingStartStage.Reconstruct;
                        break;
                    }
                    var candidate = (FormattingElementEntry) _specialFormattingCursor.Value;
                    _specialFormattingCursor = _specialFormattingCursor.Previous;
                    Charge(1);
                    advanced = true;
                    if (candidate.Name == "a" && candidate.Element.NamespaceUri == Namespaces.Html)
                    {
                        Error("nested-anchor");
                        _specialOldAnchor = candidate.Element;
                        _specialFormattingStartStage = SpecialFormattingStartStage.AdoptAnchor;
                    }
                    break;
                case SpecialFormattingStartStage.AdoptAnchor:
                    if (!TryAdoptionAgency("a")) return false;
                    _specialFormattingStartStage = SpecialFormattingStartStage.CleanupAnchorList;
                    advanced = true;
                    break;
                case SpecialFormattingStartStage.CleanupAnchorList:
                    if (_formattingByElement.TryGetValue(_specialOldAnchor!, out var oldEntry))
                        RemoveFormattingEntry(oldEntry);
                    _specialOpenScan = _open.Count - 1;
                    _specialFormattingStartStage = SpecialFormattingStartStage.CleanupAnchorOpen;
                    Charge(1);
                    advanced = true;
                    break;
                case SpecialFormattingStartStage.CleanupAnchorOpen:
                    if (_adoptionShiftIndex >= 0)
                    {
                        if (!TryRemoveAdoptionOpen(_specialOpenScan)) return false;
                        _specialFormattingStartStage = SpecialFormattingStartStage.Reconstruct;
                        advanced = true;
                        break;
                    }
                    if (_specialOpenScan < 0)
                    {
                        _specialFormattingStartStage = SpecialFormattingStartStage.Reconstruct;
                        break;
                    }
                    if (ReferenceEquals(_open[_specialOpenScan], _specialOldAnchor))
                    {
                        if (!TryRemoveAdoptionOpen(_specialOpenScan)) return false;
                        _specialFormattingStartStage = SpecialFormattingStartStage.Reconstruct;
                    }
                    else _specialOpenScan--;
                    Charge(1);
                    advanced = true;
                    break;
                case SpecialFormattingStartStage.Reconstruct:
                    if (!TryReconstructFormatting()) return false;
                    _specialFormattingStartStage = name == "nobr"
                        ? SpecialFormattingStartStage.CheckNobr : SpecialFormattingStartStage.Insert;
                    if (name == "nobr") _specialOpenScan = _open.Count - 1;
                    advanced = true;
                    break;
                case SpecialFormattingStartStage.CheckNobr:
                    if (_specialOpenScan < 0)
                    {
                        _specialFormattingStartStage = SpecialFormattingStartStage.Insert;
                        break;
                    }
                    var examined = _open[_specialOpenScan--];
                    if (IsHtmlElement(examined, "nobr"))
                    {
                        Error("nested-nobr");
                        _specialFormattingStartStage = SpecialFormattingStartStage.AdoptNobr;
                    }
                    else if (IsScopeBoundary(examined))
                        _specialFormattingStartStage = SpecialFormattingStartStage.Insert;
                    Charge(1);
                    advanced = true;
                    break;
                case SpecialFormattingStartStage.AdoptNobr:
                    if (!TryAdoptionAgency("nobr")) return false;
                    _specialFormattingStartStage = SpecialFormattingStartStage.ReconstructNobr;
                    advanced = true;
                    break;
                case SpecialFormattingStartStage.ReconstructNobr:
                    if (!TryReconstructFormatting()) return false;
                    _specialFormattingStartStage = SpecialFormattingStartStage.Insert;
                    advanced = true;
                    break;
                case SpecialFormattingStartStage.Insert:
                    if (_pendingFormattingElement is null)
                        _pendingFormattingElement = InsertTokenElement();
                    if (!TryAddFormattingElement()) return false;
                    _specialFormattingStartStage = SpecialFormattingStartStage.Idle;
                    _specialOldAnchor = null;
                    _specialFormattingCursor = null;
                    return true;
                default: throw new InvalidOperationException("Invalid formatting-start continuation.");
            }
        }
    }

    private bool TryAdoptionAgency(string subject)
    {
        if (_adoptionStage == AdoptionStage.Idle)
        {
            _adoptionSubject = subject;
            _adoptionOuter = 0;
            _adoptionStage = AdoptionStage.Outer;
            // The current-node shortcut is valid only for an element absent
            // from the entire formatting list, including older marker scopes.
            if (IsHtmlElement(Current, subject) && !_formattingByElement.ContainsKey(Current))
            {
                Pop();
                EndAdoption();
                return true;
            }
        }

        var advanced = false;
        while (true)
        {
            if (_remaining <= 0 && advanced) return false;
            switch (_adoptionStage)
            {
                case AdoptionStage.Outer:
                    if (_adoptionOuter == 8) { EndAdoption(); return true; }
                    _adoptionOuter++;
                    _adoptionCursor = _formatting.Last;
                    _adoptionStage = AdoptionStage.FindFormatting;
                    Charge(1);
                    advanced = true;
                    break;
                case AdoptionStage.FindFormatting:
                    if (_adoptionCursor is null || _adoptionCursor.Value is FormattingMarker)
                    {
                        _adoptionScan = _open.Count - 1;
                        _adoptionStage = AdoptionStage.GenericFind;
                        break;
                    }
                    var candidate = (FormattingElementEntry) _adoptionCursor.Value;
                    _adoptionCursor = _adoptionCursor.Previous;
                    Charge(1);
                    advanced = true;
                    if (candidate.Name == _adoptionSubject && candidate.Element.NamespaceUri == Namespaces.Html)
                    {
                        _adoptionFormatting = candidate;
                        _adoptionScan = _open.Count - 1;
                        _adoptionStage = AdoptionStage.FindOpen;
                    }
                    break;
                case AdoptionStage.FindOpen:
                    if (_adoptionScan < 0)
                    {
                        Error("adoption-formatting-not-open");
                        RemoveFormattingEntry(_adoptionFormatting!);
                        EndAdoption();
                        return true;
                    }
                    if (ReferenceEquals(_open[_adoptionScan], _adoptionFormatting!.Element))
                    {
                        _adoptionFormattingIndex = _adoptionScan;
                        _adoptionScan = _open.Count - 1;
                        _adoptionStage = AdoptionStage.FindScope;
                    }
                    else _adoptionScan--;
                    Charge(1);
                    advanced = true;
                    break;
                case AdoptionStage.FindScope:
                    if (_adoptionScan == _adoptionFormattingIndex)
                    {
                        if (!ReferenceEquals(Current, _adoptionFormatting!.Element))
                            Error("misnested-formatting-end-tag");
                        _adoptionScan = _adoptionFormattingIndex + 1;
                        _adoptionStage = AdoptionStage.FindFurthest;
                    }
                    else if (IsScopeBoundary(_open[_adoptionScan]))
                    {
                        Error("adoption-formatting-out-of-scope");
                        EndAdoption();
                        return true;
                    }
                    else _adoptionScan--;
                    Charge(1);
                    advanced = true;
                    break;
                case AdoptionStage.FindFurthest:
                    if (_adoptionScan == _open.Count)
                    {
                        _adoptionStage = AdoptionStage.PopWithoutBlock;
                        break;
                    }
                    if (IsSpecialElement(_open[_adoptionScan]))
                    {
                        _adoptionFurthestIndex = _adoptionScan;
                        _adoptionFurthestBlock = _open[_adoptionScan];
                        _adoptionCommonAncestor = _open[_adoptionFormattingIndex - 1];
                        _adoptionBookmarkBefore = _adoptionFormatting!.Node!.Next;
                        _adoptionLastNode = _adoptionFurthestBlock;
                        _adoptionNodeIndex = _adoptionFurthestIndex;
                        _adoptionInner = 0;
                        _adoptionStage = AdoptionStage.Inner;
                    }
                    else _adoptionScan++;
                    Charge(1);
                    advanced = true;
                    break;
                case AdoptionStage.PopWithoutBlock:
                    if (_open.Count > _adoptionFormattingIndex)
                    {
                        Pop();
                        advanced = true;
                        break;
                    }
                    RemoveFormattingEntry(_adoptionFormatting!);
                    EndAdoption();
                    return true;
                case AdoptionStage.Inner:
                    _adoptionInner++;
                    _adoptionNodeIndex--;
                    if (_adoptionNodeIndex == _adoptionFormattingIndex)
                    {
                        _adoptionStage = AdoptionStage.ResolveMove;
                        Charge(1);
                        advanced = true;
                        break;
                    }
                    var node = _open[_adoptionNodeIndex];
                    if (_adoptionInner > 3 && _formattingByElement.TryGetValue(node, out var overThreshold))
                        RemoveFormattingEntry(overThreshold);
                    _adoptionStage = _formattingByElement.ContainsKey(node)
                        ? AdoptionStage.RecreateInner : AdoptionStage.RemoveInnerOpen;
                    Charge(1);
                    advanced = true;
                    break;
                case AdoptionStage.RemoveInnerOpen:
                    if (!TryRemoveAdoptionOpen(_adoptionNodeIndex)) return false;
                    _adoptionFurthestIndex--;
                    _adoptionStage = AdoptionStage.Inner;
                    advanced = true;
                    break;
                case AdoptionStage.RecreateInner:
                    var old = _open[_adoptionNodeIndex];
                    var entry = _formattingByElement[old];
                    var recreated = CreateFromFormattingEntry(entry);
                    ReplaceAdoptionOpen(_adoptionNodeIndex, recreated);
                    _formattingByElement.Remove(old);
                    entry.Element = recreated;
                    _formattingByElement.Add(recreated, entry);
                    if (ReferenceEquals(_adoptionLastNode, _adoptionFurthestBlock))
                        _adoptionBookmarkBefore = entry.Node!.Next;
                    recreated.AppendChild(_adoptionLastNode!);
                    _adoptionLastNode = recreated;
                    Charge(1);
                    _adoptionStage = AdoptionStage.Inner;
                    advanced = true;
                    break;
                case AdoptionStage.ResolveMove:
                    _adoptionMoveLocation = FindAdjustedInsertionLocation(_adoptionCommonAncestor);
                    _adoptionMoveAncestor = _adoptionMoveLocation.Value.Parent;
                    _adoptionMoveBlocked = false;
                    _adoptionStage = AdoptionStage.CheckMove;
                    advanced = true;
                    break;
                case AdoptionStage.CheckMove:
                    if (_adoptionMoveAncestor is null)
                    {
                        _adoptionStage = AdoptionStage.MoveLast;
                        Charge(1);
                        advanced = true;
                        break;
                    }
                    if (ReferenceEquals(_adoptionMoveAncestor, _adoptionLastNode))
                    {
                        _adoptionMoveBlocked = true;
                        _adoptionStage = AdoptionStage.MoveLast;
                    }
                    else _adoptionMoveAncestor = _adoptionMoveAncestor.ParentNode ??
                        (_adoptionMoveAncestor as DocumentFragment)?.Host;
                    Charge(1);
                    advanced = true;
                    break;
                case AdoptionStage.MoveLast:
                    var location = _adoptionMoveLocation!.Value;
                    if (_adoptionMoveBlocked ||
                        location.Parent is Document { DocumentElement: { } documentElement } &&
                            !ReferenceEquals(documentElement, _adoptionLastNode) ||
                        ReferenceEquals(location.Before, _adoptionLastNode) ||
                        location.Before is not null && !ReferenceEquals(location.Before.ParentNode, location.Parent))
                        _adoptionLastNode!.ParentNode?.RemoveChild(_adoptionLastNode);
                    else
                        location.Parent.InsertBefore(_adoptionLastNode!, location.Before);
                    Charge(1);
                    _adoptionStage = AdoptionStage.CreateReplacement;
                    advanced = true;
                    break;
                case AdoptionStage.CreateReplacement:
                    _adoptionReplacement = CreateFromFormattingEntry(_adoptionFormatting!);
                    _adoptionTransferCursor = _adoptionFurthestBlock!.FirstChild;
                    _adoptionStage = AdoptionStage.TransferChildren;
                    advanced = true;
                    break;
                case AdoptionStage.TransferChildren:
                    if (_adoptionTransferCursor is null)
                    {
                        _adoptionStage = AdoptionStage.AppendReplacement;
                        break;
                    }
                    var child = _adoptionTransferCursor;
                    _adoptionTransferCursor = child.NextSibling;
                    _adoptionReplacement!.AppendChild(child);
                    Charge(1);
                    advanced = true;
                    break;
                case AdoptionStage.AppendReplacement:
                    _adoptionFurthestBlock!.AppendChild(_adoptionReplacement!);
                    Charge(1);
                    _adoptionStage = AdoptionStage.ReplaceFormatting;
                    advanced = true;
                    break;
                case AdoptionStage.ReplaceFormatting:
                    var original = _adoptionFormatting!;
                    var replacement = new FormattingElementEntry(_adoptionReplacement!, original.Name,
                        original.Attributes, original.AttributeCounts, original.AttributeWork, original.Key, original.Marker);
                    RemoveFormattingEntry(original);
                    replacement.Node = _adoptionBookmarkBefore is null
                        ? _formatting.AddLast(replacement)
                        : _formatting.AddBefore(_adoptionBookmarkBefore, replacement);
                    IndexFormattingEntry(replacement);
                    // The selected entry was the last with this subject after
                    // the marker. Since the key includes the name, appending
                    // to its bucket preserves bucket order after the bookmark
                    // moves past entries of other names.
                    _adoptionStage = AdoptionStage.RemoveFormattingOpen;
                    Charge(1);
                    advanced = true;
                    break;
                case AdoptionStage.RemoveFormattingOpen:
                    if (!TryRemoveAdoptionOpen(_adoptionFormattingIndex)) return false;
                    _adoptionFurthestIndex--;
                    _adoptionStage = AdoptionStage.InsertReplacementOpen;
                    advanced = true;
                    break;
                case AdoptionStage.InsertReplacementOpen:
                    if (!TryInsertAdoptionOpen(_adoptionFurthestIndex + 1, _adoptionReplacement!)) return false;
                    _adoptionStage = AdoptionStage.Outer;
                    advanced = true;
                    break;
                case AdoptionStage.GenericFind:
                    if (_adoptionScan < 0)
                    {
                        Error("unexpected-end-tag");
                        EndAdoption();
                        return true;
                    }
                    var generic = _open[_adoptionScan];
                    if (IsHtmlElement(generic, _adoptionSubject!))
                    {
                        _adoptionGenericTarget = _adoptionScan;
                        _adoptionStage = AdoptionStage.GenericImplied;
                    }
                    else if (IsSpecialElement(generic))
                    {
                        Error("unexpected-end-tag");
                        EndAdoption();
                        return true;
                    }
                    else _adoptionScan--;
                    Charge(1);
                    advanced = true;
                    break;
                case AdoptionStage.GenericImplied:
                    if (!TryGenerateImpliedEndTags(_adoptionSubject)) return false;
                    if (!ReferenceEquals(Current, _open[_adoptionGenericTarget]))
                        Error("misnested-end-tag");
                    _adoptionStage = AdoptionStage.GenericPop;
                    advanced = true;
                    break;
                case AdoptionStage.GenericPop:
                    if (_open.Count > _adoptionGenericTarget)
                    {
                        Pop();
                        advanced = true;
                        break;
                    }
                    EndAdoption();
                    return true;
                default: throw new InvalidOperationException("Invalid adoption continuation.");
            }
        }
    }

    private static bool IsSpecialElement(Element element) => element.NamespaceUri == Namespaces.Html &&
        IsSpecial(element.LocalName);

    private Element CreateFromFormattingEntry(FormattingElementEntry entry)
    {
        var element = _document.CreateParsedElement(Namespaces.Html, entry.Name, null);
        if (entry.Attributes.Length != 0)
        {
            element.InitializeParsedAttributes(entry.Attributes, _cancellationToken);
            Charge(entry.AttributeWork);
        }
        Charge(1);
        return element;
    }

    private void IndexFormattingEntry(FormattingElementEntry entry)
    {
        var bucketKey = (entry.Marker, entry.Key);
        if (!_formattingByKey.TryGetValue(bucketKey, out var bucket))
            _formattingByKey[bucketKey] = bucket = [];
        bucket.Add(entry);
        _formattingByElement.Add(entry.Element, entry);
        Charge(1);
    }

    private void RemoveFormattingEntry(FormattingElementEntry entry)
    {
        if (ReferenceEquals(_adoptionBookmarkBefore, entry.Node))
            _adoptionBookmarkBefore = entry.Node!.Next;
        _formatting.Remove(entry.Node!);
        UnindexFormatting(entry);
        Charge(1);
    }

    private void ReplaceAdoptionOpen(int index, Element replacement)
    {
        var original = _open[index];
        RemoveIndexes(original, index);
        _openIdentity.Remove(original);
        if (!AllowedOpenAtEof(original.LocalName)) _unexpectedOpenCount--;
        _open[index] = replacement;
        _openIdentity.Add(replacement);
        AddIndexesAt(replacement, index);
        if (!AllowedOpenAtEof(replacement.LocalName)) _unexpectedOpenCount++;
        Charge(1);
    }

    private void AddIndexesAt(Element element, int index)
    {
        static void Insert(List<int> indexes, int value)
        {
            var position = indexes.BinarySearch(value);
            indexes.Insert(position < 0 ? ~position : position, value);
        }
        if (!_nameIndexes.TryGetValue(element.LocalName, out var names))
            _nameIndexes[element.LocalName] = names = [];
        Insert(names, index);
        if (IsSpecialElement(element))
        {
            Insert(_specialIndexes, index);
            if (element.LocalName is not ("address" or "div" or "p" or "li")) Insert(_liStops, index);
            if (element.LocalName is not ("address" or "div" or "p" or "dd" or "dt")) Insert(_ddDtStops, index);
        }
        if (IsScopeBoundary(element)) Insert(_scopeStops, index);
        if (IsResetModeElement(element)) Insert(_resetModeIndexes, index);
        Charge(1L + names.Count + _specialIndexes.Count + _liStops.Count + _ddDtStops.Count +
            _scopeStops.Count + _resetModeIndexes.Count);
    }

    private bool TryRemoveAdoptionOpen(int index)
    {
        if (_adoptionShiftIndex < 0)
        {
            if (index == _open.Count - 1) { Pop(); return true; }
            var removed = _open[index];
            _openIdentity.Remove(removed);
            if (!AllowedOpenAtEof(removed.LocalName)) _unexpectedOpenCount--;
            RemoveIndexes(removed, index);
            _adoptionShiftIndex = index;
            Charge(1);
        }
        var moved = false;
        while (_adoptionShiftIndex < _open.Count - 1)
        {
            if (_remaining <= 0 && moved) return false;
            var item = _open[_adoptionShiftIndex + 1];
            _open[_adoptionShiftIndex] = item;
            ShiftIndexes(item, _adoptionShiftIndex + 1);
            _adoptionShiftIndex++;
            moved = true;
        }
        _open.RemoveAt(_open.Count - 1);
        _adoptionShiftIndex = -1;
        return true;
    }

    private bool TryInsertAdoptionOpen(int index, Element element)
    {
        if (_adoptionInsertIndex < 0)
        {
            CheckDepth();
            _adoptionInsertIndex = index;
            _adoptionInsertCursor = _open.Count - 1;
            _open.Add(element);
            Charge(1);
        }
        var shifted = false;
        while (_adoptionInsertCursor >= index)
        {
            if (_remaining <= 0 && shifted) return false;
            var item = _open[_adoptionInsertCursor];
            _open[_adoptionInsertCursor + 1] = item;
            ShiftIndexesUp(item, _adoptionInsertCursor);
            _adoptionInsertCursor--;
            shifted = true;
        }
        _open[index] = element;
        _openIdentity.Add(element);
        AddIndexesAt(element, index);
        if (!AllowedOpenAtEof(element.LocalName)) _unexpectedOpenCount++;
        _adoptionInsertIndex = -1;
        Charge(1);
        return true;
    }

    private void ShiftIndexesUp(Element element, int oldIndex)
    {
        static void Shift(List<int> indexes, int index)
        {
            var position = indexes.BinarySearch(index);
            if (position < 0) throw new InvalidOperationException("HTML stack index was not found.");
            indexes[position] = index + 1;
        }
        Shift(_nameIndexes[element.LocalName], oldIndex);
        if (IsSpecialElement(element))
        {
            Shift(_specialIndexes, oldIndex);
            if (element.LocalName is not ("address" or "div" or "p" or "li")) Shift(_liStops, oldIndex);
            if (element.LocalName is not ("address" or "div" or "p" or "dd" or "dt")) Shift(_ddDtStops, oldIndex);
        }
        if (IsScopeBoundary(element)) Shift(_scopeStops, oldIndex);
        if (IsResetModeElement(element)) Shift(_resetModeIndexes, oldIndex);
        Charge(1);
    }

    private void EndAdoption()
    {
        _adoptionStage = AdoptionStage.Idle;
        _adoptionSubject = null;
        _adoptionCursor = null;
        _adoptionBookmarkBefore = null;
        _adoptionFormatting = null;
        _adoptionCommonAncestor = null;
        _adoptionFurthestBlock = null;
        _adoptionLastNode = null;
        _adoptionReplacement = null;
        _adoptionTransferCursor = null;
        _adoptionMoveLocation = null;
        _adoptionMoveAncestor = null;
    }
}
