namespace Jint.HtmlParser;

// State that only forms, slots, shadow trees, live ranges, iterators and mutation observers
// give a node. Parsed documents rarely need any of it, so it lives in one lazily allocated
// object instead of widening every node.
internal class NodeRareData
{
    internal NativeIdIndex? IdIndex;
    internal HtmlFormIndex? FormIndex;
    internal HtmlRadioGroupIndex? RadioIndex;
    internal HtmlFormWorkProbe? FormWorkProbe;
    internal Element? StoredAssignedSlot;
    internal WeakReference<Element>? ManualSlot;
    internal ShadowRoot? TreeShadowRoot;
    internal EndpointBucket? RangeEndpoints;
    internal List<WeakReference<DomNodeIterator>>? RootIterators;
    internal int IteratorRootSweepCursor;
    internal List<NodeMutationRegistration>? MutationRegistrations;
}

internal sealed class ElementRareData : NodeRareData
{
    internal object? AttributeStructureIdentity;
    internal HtmlFormAssociationState? FormAssociationState;
    internal SlotElementState? SlotState;
    internal HtmlTemplatePatchState? TemplatePatchState;
    internal HtmlOptionCore? OptionCore;
    internal HtmlSelectCore? SelectCore;
    internal ShadowRoot? AttachedShadowRoot;
    internal CustomElementRegistryIdentity? CustomElementRegistry;
    internal HtmlElementState? HtmlState;
    internal string? IsValue;
    internal DocumentFragment? TemplateContent;
}
