namespace Jint.HtmlParser;

// DOM §5.5 content algorithms. Partial ancestor chains use explicit frames.
// https://dom.spec.whatwg.org/#dom-range-deletecontents
// https://dom.spec.whatwg.org/#concept-range-extract
// https://dom.spec.whatwg.org/#concept-range-clone
public sealed partial class DomRange
{
    private sealed class ContentPart(Node source, bool partial, uint offset = 0, uint count = 0)
    {
        internal readonly Node Source = source;
        internal readonly bool Partial = partial;
        internal readonly uint Offset = offset;
        internal readonly uint Count = count;
        internal readonly List<ContentPart> Children = [];
    }
    private sealed class PlanFrame(Node container, List<ContentPart> parts, bool started)
    {
        internal readonly Node Container = container;
        internal readonly List<ContentPart> Parts = parts;
        internal Node? Child = container.FirstChild;
        internal uint Index;
        internal bool Started = started;
    }
    private static HashSet<Node> Ancestors(Node node)
    {
        var result = new HashSet<Node>();
        for (Node? current = node; current is not null; current = current.ParentNode) result.Add(current);
        return result;
    }
    private static List<ContentPart> PlanContents(BoundaryPoint start, BoundaryPoint end)
    {
        var result = new List<ContentPart>();
        var startNode = start.Container.Node!;
        var endNode = end.Container.Node!;
        if (ReferenceEquals(startNode, endNode) && IsData(startNode))
        {
            result.Add(new(startNode, true, start.Offset, end.Offset - start.Offset)); return result;
        }
        var startPath = Ancestors(startNode); var endPath = Ancestors(endNode);
        var common = startNode;
        while (!endPath.Contains(common)) common = common.ParentNode!;
        var frames = new Stack<PlanFrame>();
        frames.Push(new(common, result, ReferenceEquals(common, startNode)));
        while (frames.TryPeek(out var frame))
        {
            if (frame.Child is not { } child) { frames.Pop(); continue; }
            var index = frame.Index++;
            frame.Child = child.NextSibling;
            if (ReferenceEquals(frame.Container, startNode) && index < start.Offset) continue;
            if (ReferenceEquals(frame.Container, endNode) && index >= end.Offset) { frames.Pop(); continue; }
            var onStart = startPath.Contains(child); var onEnd = endPath.Contains(child);
            if (!frame.Started)
            {
                if (!onStart) continue;
                frame.Started = true;
            }
            if (onEnd) frame.Child = null;
            if (!onStart && !onEnd) { frame.Parts.Add(new(child, false)); continue; }
            if (IsData(child))
            {
                var from = onStart ? start.Offset : 0;
                var to = onEnd ? end.Offset : BoundaryOrder.GetLength(new(child));
                frame.Parts.Add(new(child, true, from, to - from));
            }
            else
            {
                var part = new ContentPart(child, true); frame.Parts.Add(part);
                frames.Push(new(child, part.Children, !onStart || ReferenceEquals(child, startNode)));
            }
        }
        return result;
    }
    private static BoundaryPoint CollapsePoint(BoundaryPoint start, BoundaryPoint end)
    {
        if (LiveTraversalTracking.Contains(start.Container.Node!, end.Container.Node!)) return start;
        var endPath = Ancestors(end.Container.Node!);
        var reference = start.Container.Node!;
        while (reference.ParentNode is { } parent && !endPath.Contains(parent)) reference = parent;
        return new(new(reference.ParentNode!), LiveTraversalTracking.IndexOf(reference) + 1);
    }
    /// <summary>Deletes selected data and nodes, preserving the DOM algorithm's intermediate mutation phases.</summary>
    public void DeleteContents()
    {
        using var mutation = new RangeMutationScope(LiveTraversalTracking.DocumentOf(Start.Container));
        using var change = Changing();
        if (Collapsed) return;
        var start = Start; var end = End;
        var plan = PlanContents(start, end);
        if (start.Container.Equals(end.Container) && start.Container.Node is { } data && IsData(data))
        {
            NativeCharacterData.ReplaceData(data, start.Offset, end.Offset - start.Offset, string.Empty); return;
        }
        var collapse = CollapsePoint(start, end); Repair(true, collapse); Repair(false, collapse);
        var frames = new Stack<IEnumerator<ContentPart>>(); frames.Push(plan.GetEnumerator());
        try
        {
            while (frames.TryPeek(out var frame))
            {
                if (!frame.MoveNext()) { frame.Dispose(); frames.Pop(); continue; }
                var part = frame.Current;
                if (!part.Partial) part.Source.ParentNode!.RemoveChild(part.Source);
                else if (IsData(part.Source)) NativeCharacterData.ReplaceData(part.Source, part.Offset, part.Count, string.Empty);
                else frames.Push(part.Children.GetEnumerator());
            }
        }
        finally { while (frames.TryPop(out var frame)) frame.Dispose(); }
    }
    /// <summary>Moves fully selected nodes and copies partial ancestors into a new document fragment.</summary>
    public DocumentFragment ExtractContents() => CopyContents(true);
    /// <summary>Creates a document fragment containing copies of selected data and nodes, without changing this range.</summary>
    public DocumentFragment CloneContents() => CopyContents(false);
    private sealed class CopyFrame(List<ContentPart> parts, DocumentFragment output, Node? finishTarget)
    {
        internal readonly List<ContentPart> Parts = parts;
        internal readonly DocumentFragment Output = output;
        internal readonly Node? FinishTarget = finishTarget;
        internal int Index;
    }
    private DocumentFragment CopyContents(bool extract)
    {
        using var mutation = new RangeMutationScope(LiveTraversalTracking.DocumentOf(Start.Container));
        using var change = Changing();
        var document = LiveTraversalTracking.DocumentOf(Start.Container);
        var fragment = document.CreateDocumentFragment();
        if (Collapsed) return fragment;
        var start = Start; var end = End;
        var plan = PlanContents(start, end);
        // The only possible doctypes are common-ancestor contained children.
        if (plan.Exists(static part => part.Source is DocumentType)) throw Error("HierarchyRequestError");
        var sameData = start.Container.Equals(end.Container) && IsData(start.Container.Node!);
        if (extract && !sameData)
        {
            var collapse = CollapsePoint(start, end); Repair(true, collapse); Repair(false, collapse);
        }
        var frames = new Stack<CopyFrame>(); frames.Push(new(plan, fragment, null));
        while (frames.TryPeek(out var frame))
        {
            if (frame.Index == frame.Parts.Count)
            {
                frames.Pop();
                frame.FinishTarget?.AppendChild(frame.Output);
                continue;
            }
            var part = frame.Parts[frame.Index++];
            if (!part.Partial)
            {
                frame.Output.AppendChild(extract ? part.Source : part.Source.CloneNode(true));
                continue;
            }
            var clone = part.Source.CloneNode();
            if (IsData(part.Source))
            {
                var selected = NativeCharacterData.SubstringData(part.Source, part.Offset, part.Count);
                NativeCharacterData.ReplaceData(clone, 0, NativeCharacterData.GetLength(clone), selected);
                frame.Output.AppendChild(clone);
                if (extract) NativeCharacterData.ReplaceData(part.Source, part.Offset, part.Count, string.Empty);
            }
            else
            {
                frame.Output.AppendChild(clone);
                frames.Push(new(part.Children, part.Source.OwnerDocument!.CreateDocumentFragment(), clone));
            }
        }
        return fragment;
    }
    /// <summary>Prevalidates and inserts a node at the start, splitting Text or CDATA when required.</summary>
    public void InsertNode(DomNodeIdentity identity)
    {
        using var mutation = new RangeMutationScope(LiveTraversalTracking.DocumentOf(Start.Container), identity.IsValid ? LiveTraversalTracking.DocumentOf(identity) : null);
        using var change = Changing();
        if (!identity.IsValid) throw new ArgumentException("A valid identity is required.", nameof(identity));
        var node = identity.Node;
        var start = Start.Container.Node;
        if (node is null || start is null || start is ProcessingInstruction or Comment ||
            start is Text or CDataSection && start.ParentNode is null || ReferenceEquals(start, node)) throw Error("HierarchyRequestError");
        var reference = start is Text or CDataSection ? start : ChildAt(start, Start.Offset);
        var parent = reference?.ParentNode ?? start;
        parent.EnsurePreInsert(node, reference);
        if (start is Text or CDataSection) reference = NativeCharacterData.SplitText(start, Start.Offset);
        if (ReferenceEquals(node, reference)) reference = reference.NextSibling;
        node.ParentNode?.RemoveChild(node);
        var offset = reference is null ? (uint) parent.ChildCount : LiveTraversalTracking.IndexOf(reference);
        offset += node is DocumentFragment ? (uint) node.ChildCount : 1;
        parent.InsertBefore(node, reference);
        if (Collapsed) Repair(false, new(new(parent), offset));
    }
    private static Node? ChildAt(Node node, uint index)
    {
        var child = node.FirstChild;
        for (uint i = 0; i < index && child is not null; i++) child = child.NextSibling;
        return child;
    }
    /// <summary>Extracts the selection, inserts a wrapper, appends the contents, and selects the wrapper.</summary>
    public void SurroundContents(DomNodeIdentity identity)
    {
        using var mutation = new RangeMutationScope(LiveTraversalTracking.DocumentOf(Start.Container), identity.IsValid ? LiveTraversalTracking.DocumentOf(identity) : null);
        using var change = Changing();
        if (!identity.IsValid) throw new ArgumentException("A valid identity is required.", nameof(identity));
        if (!Start.Container.Equals(End.Container))
        {
            var startPath = Ancestors(Start.Container.Node!); var endPath = Ancestors(End.Container.Node!);
            foreach (var node in startPath)
                if (!endPath.Contains(node) && node is not Text and not CDataSection) throw Error("InvalidStateError");
            foreach (var node in endPath)
                if (!startPath.Contains(node) && node is not Text and not CDataSection) throw Error("InvalidStateError");
        }
        if (identity.Node is Document or DocumentType or DocumentFragment) throw Error("InvalidNodeTypeError");
        var fragment = ExtractContents();
        var parent = identity.Node;
        if (parent?.FirstChild is not null) parent.ReplaceChildren();
        InsertNode(identity);
        parent!.AppendChild(fragment);
        SelectNode(identity);
    }
}
