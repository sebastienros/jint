namespace Jint.HtmlParser;

// HTML §4.12.3: https://html.spec.whatwg.org/multipage/scripting.html#the-template-element.
// These are native template identity slots, not parser-stack state. Adoption
// preserves references; constructing/cloning an element starts without them.
internal sealed class HtmlTemplatePatchState
{
    private HtmlTemplatePatchState(Node insertionTarget, ProcessingInstruction startMarker, ProcessingInstruction? endMarker)
    {
        InsertionTarget = insertionTarget;
        StartMarker = startMarker;
        EndMarker = endMarker;
    }

    internal Node InsertionTarget { get; }
    internal ProcessingInstruction StartMarker { get; }
    internal ProcessingInstruction? EndMarker { get; }

    internal static void Install(Element template, Node insertionTarget,
        ProcessingInstruction startMarker, ProcessingInstruction? endMarker)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(insertionTarget);
        ArgumentNullException.ThrowIfNull(startMarker);
        if (template.NamespaceUri != Namespaces.Html || template.LocalName != "template" ||
            insertionTarget is not (Element or DocumentFragment) ||
            !ReferenceEquals(startMarker.ParentNode, insertionTarget) ||
            endMarker is not null && !ReferenceEquals(endMarker.ParentNode, insertionTarget))
            throw new InvalidOperationException("Template patch markers must identify a native insertion target.");
        template.TemplatePatchState = new HtmlTemplatePatchState(insertionTarget, startMarker, endMarker);
    }
}
