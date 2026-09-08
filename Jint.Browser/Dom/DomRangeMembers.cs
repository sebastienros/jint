using AngleSharp.Dom;
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

            DomFailures.Refuse(realm.Engine, member, DomExceptionNames.WrongDocument, "the point and range have different roots.");
        }

        if (node is IDocumentType)
        {
            DomFailures.Refuse(realm.Engine, member, DomExceptionNames.InvalidNodeType, "a doctype cannot contain a boundary point.");
        }

        var length = node is IAttr ? 0 : node is ICharacterData data ? data.Length : node.ChildNodes.Length;
        if (offset > (uint) length)
        {
            DomFailures.Refuse(realm.Engine, member, DomExceptionNames.IndexSize, "the offset is past the end of the node.");
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
