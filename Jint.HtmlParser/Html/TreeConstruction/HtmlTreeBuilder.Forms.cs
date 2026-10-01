using System;
using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    private Dictionary<Node, Node> _rootCache = new(ReferenceEqualityComparer.Instance);
    private Document? _rootDocument;
    private ulong _rootDocumentStamp;
    private List<Node> _rootPath = [];
    private Element? _pendingFormElement;
    private Element? _pendingFormOwner;
    private InsertionLocation _pendingFormLocation;
    private Node? _pendingFormTarget;
    private bool _pendingFormFosterParenting;
    private bool _pendingFormLocationDirty;
    private int _pendingOwnerAttributeIndex;
    private Node? _rootCursor;
    private Node? _resolvedRoot;
    private Node? _intendedParentRoot;
    private bool _resolvingFormRoot;

    // Host code may move/adopt open elements between drives. Native mutation
    // stamps invalidate cached ancestry; parser insertions update it directly.
    // A saturated stamp cannot prove stability and therefore is never cached.
    internal void BeginDriveRootTracking()
    {
        if (_rootDocument is null) return;
        if (_rootDocumentStamp != _rootDocument.MutationStamp) InvalidateRootCache();
        else if (_rootDocumentStamp == ulong.MaxValue)
        {
            // No host boundary occurs inside a pending insertion. Discard
            // reusable proofs, but let its saved walk finish across quotas.
            _rootCache = new Dictionary<Node, Node>(ReferenceEqualityComparer.Instance);
        }
    }

    internal void EndDriveRootTracking()
    {
        if (_rootDocument is not null) _rootDocumentStamp = _rootDocument.MutationStamp;
    }

    private void InvalidateRootCache()
    {
        if (_rootDocument is null) return;
        // Replacement does not hide a walk of every cached node behind a quota.
        _rootCache = new Dictionary<Node, Node>(ReferenceEqualityComparer.Instance);
        if (_pendingFormElement is null) return;
        _rootPath = [];
        _pendingFormLocationDirty = true;
        _pendingOwnerAttributeIndex = 0;
        _rootCursor = _pendingFormLocation.Parent;
        _resolvedRoot = null;
        _intendedParentRoot = null;
        _resolvingFormRoot = false;
    }

    private void TrackRootDocument(Node node)
    {
        var document = node as Document ?? node.OwnerDocument!;
        if (ReferenceEquals(_rootDocument, document)) return;
        InvalidateRootCache();
        _rootDocument = document;
        _rootDocumentStamp = document.MutationStamp;
    }

    private void TrackInsertedRoot(Node node, Node parent)
    {
        if (_rootDocument is null) return;
        if (!ReferenceEquals(_rootDocument, parent as Document ?? parent.OwnerDocument)) return;
        if (parent is Document || parent is DocumentFragment)
            _rootCache[node] = parent;
        else if (_rootCache.TryGetValue(parent, out var root))
            _rootCache[node] = root;
    }

    // HTML Standard §13.2.6.1 (2026-09-25). Keep the fresh element off-tree
    // until the intended parent and form are proven to share an ordinary root.
    // The caller's parser-stack operations can finish, but no next token or host
    // request is processed before this cooperative insertion commit finishes.
    private bool DeferFormInsertion(Element element, InsertionLocation location, Node target)
    {
        if (_fragmentContext is not null || _form is null || IsParsingTemplateContents ||
            !ReferenceEquals(element.OwnerDocument, _form.OwnerDocument) ||
            !HtmlFormState.IsFormAssociated(element) ||
            element.FormAssociationState?.IsFormAssociatedCustomElement == true ||
            HtmlFormState.IsListed(element) && element.GetAttributeNodeNS(null, "form") is not null)
            return false;
        if (_pendingFormElement is not null) throw new InvalidOperationException("Form insertion is already pending.");
        _pendingFormElement = element;
        _pendingFormOwner = _form;
        _pendingFormLocation = location;
        _pendingFormTarget = target;
        _pendingFormFosterParenting = _fosterParenting;
        _pendingFormLocationDirty = false;
        _pendingOwnerAttributeIndex = 0;
        _rootCursor = location.Parent;
        _resolvedRoot = null;
        _intendedParentRoot = null;
        _resolvingFormRoot = false;
        TrackRootDocument(location.Parent);
        TrackRootDocument(_form);
        return true;
    }

    private void AdvanceFormInsertion()
    {
        while (_remaining > 0)
        {
            if (_pendingFormLocationDirty)
            {
                var savedFosterParenting = _fosterParenting;
                _fosterParenting = _pendingFormFosterParenting;
                _pendingFormLocation = FindAdjustedInsertionLocation(_pendingFormTarget);
                _fosterParenting = savedFosterParenting;
                _pendingFormLocationDirty = false;
                _rootCursor = _pendingFormLocation.Parent;
                TrackRootDocument(_rootCursor);
                // Switching the cache document invalidates the previous proof.
                _pendingFormLocationDirty = false;
                Charge(1);
                continue;
            }
            var owner = _pendingFormLocation.Parent as Document ?? _pendingFormLocation.Parent.OwnerDocument!;
            if (!ReferenceEquals(_pendingFormElement!.OwnerDocument, owner))
            {
                // Preflight the fresh node's attribute adoption under this quota;
                // the native ownership commit preserves its stack identity.
                if (_pendingOwnerAttributeIndex < _pendingFormElement.AttributeCount)
                {
                    _pendingOwnerAttributeIndex++;
                    Charge(1);
                    continue;
                }
                _pendingFormElement.AdoptInto(owner);
                Charge(1);
                continue;
            }
            // A host can adopt just the insertion target, or move the whole form
            // subtree while this walk is suspended. Different node documents
            // prove different ordinary trees without switching cache documents.
            if (!ReferenceEquals(owner, _pendingFormOwner!.OwnerDocument))
            {
                CommitFormInsertion(associate: false);
                return;
            }
            if (_resolvedRoot is null)
            {
                var node = _rootCursor!;
                TrackRootDocument(node);
                if (_rootCache.TryGetValue(node, out var cached)) _resolvedRoot = cached;
                else if (node.ParentNode is null) _resolvedRoot = node;
                else
                {
                    _rootPath.Add(node);
                    _rootCursor = node.ParentNode;
                }
                Charge(1);
                continue;
            }
            if (_rootPath.Count > 0)
            {
                var index = _rootPath.Count - 1;
                _rootCache[_rootPath[index]] = _resolvedRoot;
                _rootPath.RemoveAt(index);
                Charge(1);
                continue;
            }
            if (!_resolvingFormRoot)
            {
                _intendedParentRoot = _resolvedRoot;
                _resolvedRoot = null;
                _rootCursor = _pendingFormOwner;
                _resolvingFormRoot = true;
                Charge(1);
                continue;
            }
            CommitFormInsertion(ReferenceEquals(_intendedParentRoot, _resolvedRoot));
            return;
        }
    }

    private void CommitFormInsertion(bool associate)
    {
        if (associate) HtmlFormAssociation.AssociateFromParser(_pendingFormElement!, _pendingFormOwner!);
        InsertAt(_pendingFormLocation, _pendingFormElement!);
        _pendingFormElement = null;
        _pendingFormOwner = null;
        _pendingFormTarget = null;
        _rootCursor = null;
        _resolvedRoot = null;
        _intendedParentRoot = null;
        Charge(1);
    }
}
