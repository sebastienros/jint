using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Native;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>DOM §5.5 point checks over AngleSharp's existing boundary-point ordering.</summary>
internal static class DomRangeMembers
{
    /// <summary>https://dom.spec.whatwg.org/#dom-range-comparepoint</summary>
    internal static JsValue ComparePoint(DomRealm realm, IRange range, JsValue[] arguments)
        => DomConvert.Number(Compare(realm, range, arguments, contains: false));

    /// <summary>https://dom.spec.whatwg.org/#dom-range-ispointinrange</summary>
    internal static JsValue IsPointInRange(DomRealm realm, IRange range, JsValue[] arguments)
        => DomConvert.Bool(Compare(realm, range, arguments, contains: true) == 0);

    /// <summary>https://dom.spec.whatwg.org/#dom-range-clonecontents</summary>
    /// <remarks>
    /// <b>The clone is what needs the hook, not the range.</b> DOM §5.5's clone-the-contents and
    /// extract-the-contents both reach "clone a node" for every partially contained ancestor, and cloning an
    /// element whose name a definition names enqueues an upgrade reaction
    /// (https://dom.spec.whatwg.org/#concept-create-element step 6.2, the synchronous flag unset) — so the
    /// constructors run, in tree order, before the <c>[CEReactions]</c> member returns. AngleSharp's own
    /// clone knows nothing of a definition, and the fragment it answers is where every element it made can
    /// be found at once, which is the same door <c>Node.cloneNode</c> already comes through.
    /// </remarks>
    internal static JsValue CloneContents(DomRealm realm, IRange range)
        => CopyContents(realm, range, extract: false);

    /// <summary>https://dom.spec.whatwg.org/#dom-range-extractcontents</summary>
    /// <remarks>
    /// The extracted fragment holds both the nodes that <i>moved</i> — already custom, and left alone by the
    /// walk — and the clones of the partially contained ancestors, which are the ones an upgrade is for.
    /// </remarks>
    internal static JsValue ExtractContents(DomRealm realm, IRange range)
        => CopyContents(realm, range, extract: true);

    private static JsValue CopyContents(DomRealm realm, IRange range, bool extract)
    {
        // Native range operations clone partial ancestors, so the source and result are not parallel
        // subtrees. Snapshot just the intersecting elements in tree order before extraction moves them;
        // the common ancestor itself is not part of the result. Intersects excludes touching siblings.
        var sources = new List<(IElement Element, string? Namespace)>();
        if (!range.IsCollapsed)
        {
            foreach (var element in Elements(range.CommonAncestor, range))
            {
                sources.Add((element, DomNamespaces.Of(element)));
            }
        }
        var fragment = extract ? range.ExtractContent() : range.CopyContent();
        var copies = Elements(fragment, range: null).ToArray();
        // Refuse a mismatched projection rather than attach one source's provenance to another node.
        // AngleSharp remains responsible for all selection, cloning, movement and range adjustment.
        if (sources.Count != copies.Length)
        {
            throw new InvalidOperationException("The native range result does not match its intersecting elements.");
        }
        for (var i = 0; i < copies.Length; i++)
        {
            var source = sources[i];
            if (source.Element.LocalName != copies[i].LocalName || source.Element.Prefix != copies[i].Prefix)
            {
                throw new InvalidOperationException("The native range result changed element order.");
            }
            DomNamespaces.Created(copies[i], source.Namespace);
        }
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, fragment);
        return realm.WrapNodeValue(fragment);
    }

    private static IEnumerable<IElement> Elements(INode root, IRange? range)
    {
        var pending = new Stack<(INode Node, IRange? Range, bool Shallow)>();
        PushChildren(root, range);
        while (pending.TryPop(out var current))
        {
            if (current.Range is { } selection && !selection.Intersects(current.Node))
            {
                continue;
            }
            if (current.Node is IElement element)
            {
                yield return element;
            }
            if (!current.Shallow)
            {
                PushChildren(current.Node, current.Range);
            }
            // Template contents are outside the range's tree but native deep cloning copies them too.
            if (current.Node is IHtmlTemplateElement template)
            {
                // Native template.Clone(false) still clones each direct content child shallowly (#4108).
                // A template on either boundary is a partial ancestor, hence takes that shallow path.
                var shallow = current.Shallow || current.Range is { } selected
                    && (template.IsInclusiveAncestorOf(selected.Head) || template.IsInclusiveAncestorOf(selected.Tail));
                PushChildren(template.Content, range: null, shallow);
            }
        }

        void PushChildren(INode node, IRange? range, bool shallow = false)
        {
            for (var i = node.ChildNodes.Length - 1; i >= 0; i--)
            {
                pending.Push((node.ChildNodes[i], range, shallow));
            }
        }
    }

    private static int Compare(DomRealm realm, IRange range, JsValue[] arguments, bool contains)
    {
        var member = contains ? "Range.isPointInRange" : "Range.comparePoint";
        var node = DomBindings.Argument<INode>(arguments, 0, member);
        var offset = DomConvert.RequiredUInt32(arguments, 1, member);

        // Both conversions precede the algorithm. A root mismatch precedes even invalid node types and
        // offsets; an attached Attr is still its own root, not its owner's document.
        if (!ReferenceEquals(DomNodeMembers.Root(node), DomNodeMembers.Root(range.Head)))
        {
            if (contains)
            {
                return 1;
            }

            DomFailures.Refuse(realm, member, DomExceptionNames.WrongDocument, "the point and range have different roots.");
        }

        if (node is IDocumentType)
        {
            DomFailures.Refuse(realm, member, DomExceptionNames.InvalidNodeType, "a doctype cannot contain a boundary point.");
        }

        var length = node is IAttr ? 0 : node is ICharacterData data ? data.Length : node.ChildNodes.Length;
        if (offset > (uint) length)
        {
            DomFailures.Refuse(realm, member, DomExceptionNames.IndexSize, "the offset is past the end of the node.");
        }

        // Most queries name the range's container (including a collapsed Attr). They need no temporary
        // range or tree traversal, and both endpoints are included.
        if (ReferenceEquals(node, range.Head) && ReferenceEquals(node, range.Tail))
        {
            return offset < (uint) range.Start ? -1 : offset > (uint) range.End ? 1 : 0;
        }

        // AngleSharp's Contains excludes endpoints, and CompareTo checks text offsets against child
        // count. Its public boundary comparison has neither defect. Clone only its two boundary values;
        // moving this private point cannot mutate the caller's range or the DOM.
        var point = range.Clone();
        point.StartWith(node, (int) offset);
        point.Collapse(toStart: true);
        if (point.CompareBoundaryTo(RangeType.StartToStart, range) == RangePosition.Before)
        {
            return -1;
        }

        return point.CompareBoundaryTo(RangeType.EndToEnd, range) == RangePosition.After ? 1 : 0;
    }
}
