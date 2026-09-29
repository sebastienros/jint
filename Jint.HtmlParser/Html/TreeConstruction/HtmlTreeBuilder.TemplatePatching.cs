namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    private TemplateOperation? _templateOperation;

    private enum TemplateStage { ResolveBodyScope, Find, Attributes, Compare, Next, Pair, Snapshot, Remove, CloseStart, CloseEnd }

    private sealed class TemplateOperation
    {
        internal TemplateOperation(Element template, Node scope, string name, Node fallbackTarget)
        {
            Template = template;
            Scope = scope;
            Name = name;
            FallbackTarget = fallbackTarget;
            Cursor = scope.FirstChild;
            if (scope is Element { NamespaceUri: Namespaces.Html, LocalName: "body" } body &&
                body.ParentNode is Element { NamespaceUri: Namespaces.Html, LocalName: "html" } html &&
                ReferenceEquals(html.ParentNode, body.OwnerDocument))
            {
                Stage = TemplateStage.ResolveBodyScope;
                Cursor = html.FirstChild;
            }
            else Stage = TemplateStage.Find;
        }

        internal TemplateOperation(Element template, HtmlTemplatePatchState patch, int stackIndex)
        {
            Template = template;
            Scope = patch.InsertionTarget;
            Name = string.Empty;
            Start = patch.StartMarker;
            End = patch.EndMarker;
            StackIndex = stackIndex;
            Stage = TemplateStage.CloseStart;
        }

        internal Element Template { get; }
        internal Node Scope;
        internal string Name { get; }
        internal Node? FallbackTarget;
        internal TemplateStage Stage;
        internal Node? Cursor;
        internal bool Unwinding;
        internal string? CandidateName;
        internal int CompareOffset;
        internal ProcessingInstruction? Start;
        internal ProcessingInstruction? End;
        internal int Nesting;
        internal List<Node>? Removals;
        internal int RemovalIndex;
        internal int StackIndex;
    }

    // HTML §4.12.3 prepare-content-patching and find-markers. Native identities
    // and saved cursors span yields; no reparsing, wrapper text or live NodeList.
    private bool AdvanceTemplateOperation()
    {
        var operation = _templateOperation!;
        while (_remaining > 0)
        {
            switch (operation.Stage)
            {
                case TemplateStage.ResolveBodyScope:
                    Charge(1);
                    if (operation.Cursor is Element { NamespaceUri: Namespaces.Html, LocalName: "body" or "frameset" } body)
                    {
                        if (ReferenceEquals(body, operation.Scope)) operation.Scope = body.ParentNode!;
                        operation.Cursor = operation.Scope.FirstChild;
                        operation.Stage = TemplateStage.Find;
                    }
                    else if (operation.Cursor is { } candidateBody) operation.Cursor = candidateBody.NextSibling;
                    else { operation.Cursor = operation.Scope.FirstChild; operation.Stage = TemplateStage.Find; }
                    break;
                case TemplateStage.Find:
                    Charge(1);
                    if (operation.Name.Length == 0 || operation.Cursor is null)
                    { FinishPatchFallback(operation); return true; }
                    operation.Stage = operation.Cursor is ProcessingInstruction { Target: "marker" or "start" }
                        ? TemplateStage.Attributes : TemplateStage.Next;
                    break;
                case TemplateStage.Attributes:
                    var candidate = (ProcessingInstruction) operation.Cursor!;
                    var done = candidate.PrepareAttributes((int) Math.Min(int.MaxValue, Math.Max(1, _remaining)), _cancellationToken, out var used);
                    Charge(used);
                    if (!done) return false;
                    operation.CandidateName = candidate.GetAttribute("name");
                    operation.CompareOffset = 0;
                    operation.Stage = TemplateStage.Compare;
                    break;
                case TemplateStage.Compare:
                    if (operation.CandidateName is not { } name || name.Length != operation.Name.Length)
                    { operation.Stage = TemplateStage.Next; break; }
                    while (operation.CompareOffset < name.Length && _remaining > 0)
                    {
                        var i = operation.CompareOffset++;
                        Charge(1);
                        if (name[i] != operation.Name[i]) { operation.Stage = TemplateStage.Next; break; }
                    }
                    if (operation.Stage == TemplateStage.Next) break;
                    if (operation.CompareOffset < name.Length) return false;
                    operation.Start = (ProcessingInstruction) operation.Cursor!;
                    if (operation.Start.Target == "marker")
                    { operation.End = operation.Start; if (!BeginPatchSnapshot(operation)) return true; }
                    else { operation.Cursor = operation.Start.NextSibling; operation.Stage = TemplateStage.Pair; }
                    break;
                case TemplateStage.Next:
                    Charge(1);
                    var current = operation.Cursor!;
                    if (!operation.Unwinding && current.FirstChild is { } child)
                    { operation.Cursor = child; operation.Stage = TemplateStage.Find; }
                    else if (current.NextSibling is { } next)
                    { operation.Cursor = next; operation.Unwinding = false; operation.Stage = TemplateStage.Find; }
                    else if (current.ParentNode is { } parent && !ReferenceEquals(parent, operation.Scope))
                    { operation.Cursor = parent; operation.Unwinding = true; }
                    else { operation.Cursor = null; operation.Stage = TemplateStage.Find; }
                    break;
                case TemplateStage.Pair:
                    Charge(1);
                    if (operation.Cursor is null) { if (!BeginPatchSnapshot(operation)) return true; break; }
                    if (operation.Cursor is ProcessingInstruction instruction)
                    {
                        if (instruction.Target == "start") operation.Nesting++;
                        else if (instruction.Target == "end")
                        {
                            if (operation.Nesting == 0)
                            { operation.End = instruction; if (!BeginPatchSnapshot(operation)) return true; break; }
                            operation.Nesting--;
                        }
                    }
                    operation.Cursor = operation.Cursor.NextSibling;
                    break;
                case TemplateStage.Snapshot:
                    Charge(1);
                    if (operation.Cursor is null || ReferenceEquals(operation.Cursor, operation.End))
                    { operation.Stage = TemplateStage.Remove; break; }
                    operation.Removals!.Add(operation.Cursor);
                    operation.Cursor = operation.Cursor.NextSibling;
                    break;
                case TemplateStage.Remove:
                    if (operation.RemovalIndex == operation.Removals!.Count)
                    { _templateOperation = null; return true; }
                    RemoveTemplateNode(operation.Removals[operation.RemovalIndex++]);
                    break;
                case TemplateStage.CloseStart:
                    RemoveTemplateNode(operation.Start!);
                    operation.Stage = TemplateStage.CloseEnd;
                    break;
                case TemplateStage.CloseEnd:
                    if (operation.End is { } end) RemoveTemplateNode(end);
                    operation.Template.TemplatePatchState = null;
                    ScheduleTemplatePop(operation.StackIndex, reprocess: false);
                    _templateOperation = null;
                    return true;
            }
        }
        return false;
    }

    private bool BeginPatchSnapshot(TemplateOperation operation)
    {
        if (operation.Start!.ParentNode is not { } target || target is not (Element or DocumentFragment) ||
            operation.End is not null && !ReferenceEquals(operation.End.ParentNode, target))
        { FinishPatchFallback(operation); return false; }
        HtmlTemplatePatchState.Install(operation.Template, target, operation.Start, operation.End);
        Charge(1);
        if (ReferenceEquals(operation.Start, operation.End)) { _templateOperation = null; return false; }
        operation.Removals = [];
        operation.Cursor = operation.Start.NextSibling;
        operation.Stage = TemplateStage.Snapshot;
        return true;
    }

    private void FinishPatchFallback(TemplateOperation operation)
    {
        // Spec fallback removes the detached template from the stack while
        // resolving its live destination, then restores that same identity.
        if (!ReferenceEquals(Pop(), operation.Template)) throw new InvalidOperationException("The pending template left the open stack.");
        var location = FindAdjustedInsertionLocation(operation.FallbackTarget);
        var owner = location.Parent as Document ?? location.Parent.OwnerDocument!;
        if (!ReferenceEquals(operation.Template.OwnerDocument, owner)) operation.Template.AdoptInto(owner);
        InsertAt(location, operation.Template);
        Push(operation.Template);
        _templateOperation = null;
    }

    private void RemoveTemplateNode(Node node)
    {
        Charge(1);
        if (node.ParentNode is not { } parent) return;
        InvalidateRootCache();
        parent.RemoveChild(node);
        _cancellationToken.ThrowIfCancellationRequested();
    }

    private void StartDeclarativeShadowTemplate()
    {
        var host = _open.Count == 1 && _fragmentShadowHost is { } context ? context : AdjustedCurrent;
        var location = FindAdjustedInsertionLocation(_headInsertionOverride);
        var template = InsertElement("template", PreparedAttributes, attributeWork: _preparedAttributeWork,
            isValue: _preparedIsValue, onlyAddToStack: true);
        if (host.AttachedShadowRoot is not null) { InsertAt(location, template); return; }
        var mode = template.GetAttribute("shadowrootmode")!;
        // Standalone has no custom-element definitions. Browser's provider only
        // reads its cached definition/state record, outside the normative catch.
        var attachment = _context.ShadowHostContextProvider?.GetShadowAttachmentContext(host)
            ?? new ShadowAttachmentContext(host.OwnerDocument!.CustomElementRegistry, false, false);
        if (_templateKeepRegistryNull) attachment = attachment with { Registry = null };
        _cancellationToken.ThrowIfCancellationRequested();
        ShadowRoot root;
        try
        {
            root = ShadowTree.Attach(host, new ShadowRootInit(AsciiEquals(mode, "open") ? ShadowRootMode.Open : ShadowRootMode.Closed,
                _templateDelegatesFocus, _templateSerializable,
                _templateManualSlotAssignment ? SlotAssignmentMode.Manual : SlotAssignmentMode.Named, _templateClonable),
                attachment);
        }
        catch (DomException)
        { InsertAt(location, template); return; }
        ShadowTree.SetDeclarativeTemplateContent(template, root, _templateKeepRegistryNull);
        Charge(1);
    }
}
