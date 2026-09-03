using System.Runtime.InteropServices;

namespace Jint.Browser.CustomElements;

/// <summary>
/// https://dom.spec.whatwg.org/#concept-element-custom-element-state.
/// </summary>
internal enum CustomElementState
{
    /// <summary>The element could become custom: its name is a valid custom element name, or it carries an <c>is</c>.</summary>
    Undefined,

    /// <summary>The element can never become custom, which is every ordinary element.</summary>
    Uncustomized,

    /// <summary>An upgrade is running: the constructor has been entered and has not finished.</summary>
    Precustomized,

    /// <summary>The constructor threw, or the element it produced was not the element being upgraded.</summary>
    Failed,

    /// <summary>The element is a custom element and its reactions run.</summary>
    Custom,
}

/// <summary>Which lifecycle callback a queued reaction will invoke.</summary>
internal enum CustomElementReactionKind
{
    /// <summary>https://html.spec.whatwg.org/multipage/custom-elements.html#concept-upgrade-an-element.</summary>
    Upgrade,

    /// <inheritdoc cref="CustomElementDefinition.ConnectedCallback" />
    Connected,

    /// <inheritdoc cref="CustomElementDefinition.ConnectedCallback" />
    Disconnected,

    /// <inheritdoc cref="CustomElementDefinition.ConnectedCallback" />
    Adopted,

    /// <inheritdoc cref="CustomElementDefinition.ConnectedCallback" />
    AttributeChanged,
}

/// <summary>
/// One entry of an element's
/// <a href="https://html.spec.whatwg.org/multipage/custom-elements.html#concept-element-custom-element-reaction-queue">custom
/// element reaction queue</a>.
/// </summary>
/// <param name="Kind">Which callback to invoke, or an upgrade.</param>
/// <param name="Definition">The definition the reaction was enqueued against.</param>
/// <param name="Name">The attribute's local name, for <see cref="CustomElementReactionKind.AttributeChanged"/>.</param>
/// <param name="OldValue">The attribute's value before the change, or <see langword="null"/> when it was absent.</param>
/// <param name="NewValue">The attribute's value after the change, or <see langword="null"/> when it was removed.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct CustomElementReaction(
    CustomElementReactionKind Kind,
    CustomElementDefinition Definition,
    string? Name,
    string? OldValue,
    string? NewValue);

/// <summary>
/// Everything one element carries because it is, or could become, a custom element: DOM's custom element
/// state, its definition, its <c>is</c> value and its reaction queue.
/// </summary>
/// <remarks>
/// <para>
/// It hangs off a <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey,TValue}"/> keyed on
/// the AngleSharp element, for the reason the wrapper cache is one: AngleSharp's element has no slot for any
/// of this and adding one is not ours to do, and an element the tree and script have both dropped must take
/// its state with it.
/// </para>
/// <para>
/// <see cref="Attributes"/> is what makes <c>attributeChangedCallback</c>'s <i>old</i> value answerable at
/// all: AngleSharp's <c>IAttributeObserver</c> reports the element, the name and the <b>new</b> value, so the
/// previous one has to be remembered here — the same "the state is what it was last reconciled against"
/// shape the events bridge uses for a handler content attribute. It holds only the definition's observed
/// attributes, so an element observing nothing allocates nothing.
/// </para>
/// </remarks>
internal sealed class CustomElementRecord
{
    /// <summary>DOM's custom element state.</summary>
    internal CustomElementState State { get; set; } = CustomElementState.Undefined;

    /// <summary>The definition the element was upgraded against, or <see langword="null"/>.</summary>
    internal CustomElementDefinition? Definition { get; set; }

    /// <summary>
    /// The element's <c>is</c> value — the name the definition was registered under, for a customized
    /// built-in. It is deliberately not the <c>is</c> content attribute: <c>createElement</c>'s option sets
    /// this and adds no attribute, which is what the standard says and what a page can tell apart.
    /// </summary>
    internal string? IsValue { get; set; }

    /// <summary>Whether the definition this element was upgraded against declared <c>formAssociated</c>.</summary>
    internal bool FormAssociated { get; set; }

    /// <summary>The element's reaction queue, drained in order when the element queue reaches it.</summary>
    internal Queue<CustomElementReaction> Reactions { get; } = new();

    /// <summary>Whether the element is already on the reaction lane's element queue.</summary>
    internal bool Queued { get; set; }

    /// <summary>The last-known value of each observed attribute; see the remarks on this type.</summary>
    internal Dictionary<string, string?>? Attributes { get; set; }
}
