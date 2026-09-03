using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.CustomElements;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/custom-elements.html#custom-element-definition">A custom
/// element definition</a>: what <c>define</c> read off the constructor, settled once and never re-read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here is read at <c>define</c> time, in the specification's order</b>, which is observable:
/// a page that changes <c>observedAttributes</c> or replaces <c>connectedCallback</c> afterwards changes
/// nothing, and one whose <c>observedAttributes</c> getter throws never gets a definition at all.
/// </para>
/// <para>
/// <see cref="ConstructionStack"/> is the specification's, and it is what makes <c>super()</c> answer the
/// element that is being created rather than making a second one. A <see langword="null"/> entry is the
/// <i>already constructed marker</i>: a constructor that reaches the base constructor twice gets an
/// <c>InvalidStateError</c> instead of a second element.
/// </para>
/// </remarks>
internal sealed class CustomElementDefinition
{
    internal CustomElementDefinition(
        string name,
        string localName,
        ObjectInstance constructor,
        ObjectInstance? prototype,
        DomInterfaceDefinition interfaceDefinition)
    {
        Name = name;
        LocalName = localName;
        Constructor = constructor;
        Prototype = prototype;
        Interface = interfaceDefinition;
    }

    /// <summary>The name <c>define</c> was given, which is also what <c>is</c> names.</summary>
    internal string Name { get; }

    /// <summary>
    /// The local name the element has: <see cref="Name"/> for an autonomous custom element, and
    /// <c>options.extends</c> for a customized built-in.
    /// </summary>
    internal string LocalName { get; }

    /// <summary>The constructor, which is also the definition's identity in <c>getName</c>.</summary>
    internal ObjectInstance Constructor { get; }

    /// <summary>
    /// <c>Get(constructor, "prototype")</c> as it was at <c>define</c> time. It is what a wrapper's
    /// <c>[[Prototype]]</c> falls back to when <c>NewTarget</c> carries no object prototype of its own.
    /// </summary>
    internal ObjectInstance? Prototype { get; }

    /// <summary>
    /// The interface whose interface object the <c>HTMLElement</c> constructor must have been reached
    /// through — <c>HTMLElement</c> for an autonomous element, and the built-in's own for a customized one.
    /// </summary>
    /// <remarks>
    /// It stands in for the specification's "valid local names ... that implement the interface of the active
    /// function object": the built-in's element is created once at <c>define</c> time to validate
    /// <c>extends</c>, so the interface it maps to is already known and the check is a reference comparison.
    /// </remarks>
    internal DomInterfaceDefinition Interface { get; }

    /// <summary>Whether this is an autonomous custom element rather than a customized built-in.</summary>
    internal bool IsAutonomous => string.Equals(Name, LocalName, StringComparison.Ordinal);

    /// <summary>https://html.spec.whatwg.org/multipage/custom-elements.html#concept-custom-element-definition-lifecycle-callbacks.</summary>
    internal ICallable? ConnectedCallback { get; set; }

    /// <inheritdoc cref="ConnectedCallback" />
    internal ICallable? DisconnectedCallback { get; set; }

    /// <inheritdoc cref="ConnectedCallback" />
    internal ICallable? AdoptedCallback { get; set; }

    /// <inheritdoc cref="ConnectedCallback" />
    internal ICallable? AttributeChangedCallback { get; set; }

    /// <summary>
    /// The attributes an <c>attributeChangedCallback</c> is enqueued for, read off the constructor's
    /// <c>observedAttributes</c> once and only when there is a callback to receive them.
    /// </summary>
    internal string[] ObservedAttributes { get; set; } = [];

    /// <summary>
    /// <c>static formAssociated</c>. It is recorded on the element and nothing else consults it: this package
    /// has no <c>ElementInternals</c>, so a form-associated custom element takes part in no entry list.
    /// </summary>
    internal bool FormAssociated { get; set; }

    /// <summary>Whether <c>disabledFeatures</c> named <c>"shadow"</c>, which is the half an upgrade honours.</summary>
    internal bool DisableShadow { get; set; }

    /// <summary>
    /// Whether <c>disabledFeatures</c> named <c>"internals"</c>. Parsed so that a page declaring it is not
    /// silently writing to nothing; there is no <c>attachInternals</c> for it to disable.
    /// </summary>
    internal bool DisableInternals { get; set; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#concept-custom-element-definition-construction-stack.
    /// A <see langword="null"/> entry is the already-constructed marker.
    /// </summary>
    internal List<DomNodeObject?> ConstructionStack { get; } = [];

    /// <summary>Whether <paramref name="name"/> is one of the observed attributes.</summary>
    internal bool Observes(string name)
    {
        if (AttributeChangedCallback is null)
        {
            return false;
        }

        foreach (var observed in ObservedAttributes)
        {
            if (string.Equals(observed, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public override string ToString() => Name;
}
