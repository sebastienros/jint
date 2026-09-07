using System.Runtime.CompilerServices;
using AngleSharp.Dom;

namespace Jint.Browser.Dom;

/// <summary>
/// The <b>engine-free</b> half of ARIA's element reflection: which elements an
/// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#attr-associated-element">attr-associated</a>
/// IDL attribute was explicitly set to, HTML's attribute change steps over them, and the visibility rule that
/// decides which of them a given element may see.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is a file of its own, and static rather than per-realm.</b> Two consumers need this state and
/// only one of them has an engine. <see cref="AriaElementReflection"/> is the IDL surface and is engine-bound
/// by definition — its <c>FrozenArray</c> identity cache is a JavaScript object. The accessibility tree is
/// not, deliberately: <c>Accessibility/AGENTS.md</c> makes "neither touches an engine" the property the whole
/// directory is built on, and <c>Jint.Tests.Browser/Accessibility/PageFixture</c> has no engine on purpose. A
/// relationship a page set through <c>el.ariaLabelledByElements</c> is nonetheless part of that element's
/// accessible name, so the state it needs has to be reachable without one — the same standing
/// <c>Dom/Views/CssCascade</c> already has, which <c>Accessibility/ElementVisibility</c> calls for the same
/// reason.
/// </para>
/// <para>
/// <b>What lives here is exactly what is engine-free</b>: the explicitly set references, which are
/// <see cref="WeakReference{T}"/>s to AngleSharp elements and nothing else. The frozen array a getter last
/// answered stays on <see cref="DomRealm"/>, because an object belongs to the engine that made it.
/// </para>
/// <para>
/// <b>The reconciliation has to come with the state, or a reader gets a stale answer.</b> HTML drops an
/// explicitly set reference in the content attribute's attribute change steps, and this implementation runs
/// them from the attribute's *value* — see <see cref="Reconcile"/>. Running them only inside the IDL getter
/// would mean a page could set <c>el.ariaLabelledByElements = [x]</c>, then
/// <c>el.setAttribute('aria-labelledby', 'y')</c>, and an accessible name computed before the next read would
/// still see <c>x</c>. So the door every consumer comes through is <c>Explicit</c>, never the field.
/// </para>
/// <para>
/// The table is keyed on the AngleSharp element because this assembly cannot add a field to one — the
/// rationale <c>Collections/DomTokenListMembers</c> states, and the arrangement six other places in this
/// package use, twice inside <c>Accessibility/</c> itself.
/// </para>
/// </remarks>
internal static class AriaElementReferences
{
    /// <summary>
    /// The mixin's element half, IDL attribute to content attribute, in the standard's own order.
    /// <c>aria-activedescendant</c> is the one that reflects a single element; the other seven are
    /// <c>FrozenArray&lt;Element&gt;?</c>, which is why every one of their names is plural.
    /// </summary>
    internal static readonly (string Member, string Attribute, bool Single)[] Reflected =
    [
        ("ariaActiveDescendantElement", "aria-activedescendant", true),
        ("ariaControlsElements", "aria-controls", false),
        ("ariaDescribedByElements", "aria-describedby", false),
        ("ariaDetailsElements", "aria-details", false),
        ("ariaErrorMessageElements", "aria-errormessage", false),
        ("ariaFlowToElements", "aria-flowto", false),
        ("ariaLabelledByElements", "aria-labelledby", false),
        ("ariaOwnsElements", "aria-owns", false),
    ];

    private static readonly ConditionalWeakTable<IElement, Slots> _slots = new();

    /// <summary>The index of <paramref name="attribute"/> in <see cref="Reflected"/>, or -1.</summary>
    /// <remarks>
    /// A scan of eight rather than a dictionary: it is called once per accessible name and once per role, and
    /// eight ordinal comparisons of interned literals are cheaper than the hash.
    /// </remarks>
    internal static int IndexOf(string attribute)
    {
        for (var i = 0; i < Reflected.Length; i++)
        {
            if (string.Equals(Reflected[i].Attribute, attribute, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The elements <paramref name="element"/> explicitly set the member reflecting
    /// <paramref name="attribute"/> to and can currently see, or <see langword="null"/> when nothing was set
    /// through the IDL attribute or the attribute change steps have dropped it — in which case the content
    /// attribute's idrefs decide, and the caller resolves those its own way.
    /// </summary>
    /// <remarks>
    /// This is the accessibility tree's whole door onto element reflection. It never allocates for an element
    /// that has no ARIA relationship, and it takes no engine, no realm and no document.
    /// </remarks>
    internal static IElement[]? Explicit(IElement element, string attribute)
    {
        var index = IndexOf(attribute);
        if (index < 0 || !_slots.TryGetValue(element, out var slots))
        {
            return null;
        }

        return Explicit(element, slots, index, attribute);
    }

    /// <summary>The same, for a caller that already holds the element's slots and the member's index.</summary>
    internal static IElement[]? Explicit(IElement element, Slots slots, int index, string attribute)
    {
        var references = Reconcile(element, slots, index, attribute);
        if (references is null)
        {
            return null;
        }

        var visible = new List<IElement>(references.Length);

        foreach (var reference in references)
        {
            if (reference.TryGetTarget(out var candidate) && IsVisibleFrom(element, candidate))
            {
                visible.Add(candidate);
            }
        }

        return [.. visible];
    }

    /// <summary>Records what an IDL setter was given, or clears the member when it was given null.</summary>
    internal static void Set(IElement element, int index, WeakReference<IElement>[]? references)
        => _slots.GetOrCreateValue(element).Explicit[index] = references;

    /// <summary>The element's slots, created on first use.</summary>
    internal static Slots SlotsFor(IElement element) => _slots.GetOrCreateValue(element);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#reflecting-content-attributes-in-idl-attributes
    /// — the content attribute's <b>attribute change steps</b>, which drop the explicitly set elements
    /// whenever anything else writes it.
    /// </summary>
    /// <remarks>
    /// Read from the attribute's <i>value</i> rather than from an observer, deliberately: AngleSharp's
    /// <c>IAttributeObserver</c> reports neither a namespaced write, nor an <c>Attr</c> node's value, nor the
    /// parser, and it is the page runtime that registers one at all — while the value says it without any of
    /// them, because the IDL setter writes the empty string and nothing else here does. The one case it reads
    /// differently from the standard is a page writing that same empty string by hand, which keeps a
    /// reference where a browser drops it; both answer no elements from the ids, and <c>Dom/AGENTS.md</c>
    /// records it.
    /// </remarks>
    private static WeakReference<IElement>[]? Reconcile(IElement element, Slots slots, int index, string attribute)
    {
        var value = element.GetAttribute(attribute);

        if (value is null || value.Length != 0)
        {
            slots.Explicit[index] = null;
            return null;
        }

        return slots.Explicit[index];
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#attr-associated-element — whether
    /// <paramref name="candidate"/> may be exposed as an attr-associated element of
    /// <paramref name="element"/>: the candidate's root has to be a shadow-including inclusive ancestor of
    /// the element's root.
    /// </summary>
    /// <remarks>
    /// Read from the <i>element</i> outwards, which is the direction that makes it readable: the same tree is
    /// allowed, a tree the element's own tree is nested inside is allowed — a node in a shadow tree may point
    /// at the light DOM around its host — and a tree nested inside the element's is not, because a reference
    /// into a shadow tree would cross an encapsulation boundary the page put there on purpose. Two detached
    /// subtrees are two roots and so cannot see each other, and one detached subtree is a root of its own, so
    /// a relationship inside it keeps working while it is out of the document.
    /// </remarks>
    private static bool IsVisibleFrom(IElement element, IElement candidate)
    {
        var target = DomNodeMembers.Root(candidate);
        var root = DomNodeMembers.Root(element);

        while (true)
        {
            if (ReferenceEquals(root, target))
            {
                return true;
            }

            if (root is IShadowRoot { Host: { } host })
            {
                root = DomNodeMembers.Root(host);
                continue;
            }

            return false;
        }
    }

    /// <summary>One element's explicitly set references, one entry per member of <see cref="Reflected"/>.</summary>
    internal sealed class Slots
    {
        /// <summary>
        /// A member's entry is <see langword="null"/> when it has never been set through the IDL attribute,
        /// or when the attribute change steps have dropped it — which is not the same as an empty array,
        /// because an empty array is a relationship to nothing and null means the content attribute decides.
        /// </summary>
        internal readonly WeakReference<IElement>[]?[] Explicit = new WeakReference<IElement>[]?[Reflected.Length];
    }
}
