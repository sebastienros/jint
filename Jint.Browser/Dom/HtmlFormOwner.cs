using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Dom;

/// <summary>HTML's association between a form-associated element and its form owner.</summary>
/// <remarks>
/// <para>
/// <b>The one reader of the <c>form</c> content attribute in this package.</b> Every lane that asks which
/// form a control belongs to comes through here — the <c>form</c> IDL attribute, the entry list, interactive
/// validation, the default button, implicit submission, a radio button group, <c>:default</c> and the
/// event-handler scope chain — because a control and the controls it competes with have to be judged by one
/// rule: the form a submission gathers entries for is the same form that validated them.
/// </para>
/// <para>
/// AngleSharp's <c>HtmlElement.GetAssignedForm()</c> walks to an ancestor <c>form</c> <i>first</i> and reads
/// the <c>form</c> content attribute only when it found none, which inverts the standard's priority: a
/// control inside one form pointing at another was owned by the one containing it
/// (<a href="https://github.com/sebastienros/jint/issues/3939">#3939</a>). What this cannot reach is
/// <c>form.elements</c>, whose membership is AngleSharp's own; <see href="divergences.md"/> records both.
/// </para>
/// </remarks>
internal static class HtmlFormOwner
{
    /// <summary>HTML's <c>form</c> content attribute — the explicit association, and the only one read.</summary>
    private const string FormAttribute = "form";

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#reset-the-form-owner — the
    /// form owner <paramref name="element"/> ends up with, or <see langword="null"/> when it has none and for
    /// every element that cannot have one.
    /// </summary>
    internal static IHtmlFormElement? Of(IElement element)
        => IsFormAssociated(element) ? Resolve(element, IsListed(element)) : null;

    /// <summary>
    /// The elements of <paramref name="form"/>'s tree whose form owner is that form, in tree order — the
    /// inventory HTML's entry list, its static validity check, the default button and implicit submission all
    /// start from.
    /// </summary>
    /// <remarks>
    /// <b>It is deliberately not <c>form.elements</c>.</b> That collection is AngleSharp's, its membership is
    /// AngleSharp's ownership rule, and it excludes image buttons besides — so a form's default button read
    /// from it could never be one. The walk starts at the form's own tree root, because an explicitly
    /// associated control is a control the form does not contain.
    /// </remarks>
    internal static IEnumerable<IElement> ControlsOf(IHtmlFormElement form)
    {
        foreach (var node in form.GetRoot().GetDescendants())
        {
            if (node is IElement element && ReferenceEquals(Of(element), form))
            {
                yield return element;
            }
        }
    }

    /// <summary>
    /// The same algorithm for a form-associated custom element, whose category is its definition's
    /// <c>formAssociated</c> rather than its local name — the one half of the question the element itself
    /// cannot answer, so the caller that can see the registry supplies it.
    /// </summary>
    internal static IHtmlFormElement? OfFormAssociatedCustomElement(IElement element)
        => Resolve(element, listed: true);

    /// <summary>
    /// The <c>form</c> IDL attribute: a listed element's own form owner, and a borrowed one for the three
    /// elements that carry the member without being form-associated at all.
    /// </summary>
    internal static IHtmlFormElement? FormIdlOf(IHtmlElement element) => element switch
    {
        // https://html.spec.whatwg.org/multipage/forms.html#dom-label-form — "return the label element's
        // labeled control's form owner (which can still be null)", so a label labelling nothing has none and
        // a label is never itself associated with anything.
        IHtmlLabelElement label => HtmlLabelAssociation.ControlFor(label) is { } control ? Of(control) : null,

        // https://html.spec.whatwg.org/multipage/form-elements.html#the-legend-element — "if the legend has a
        // fieldset element as its parent, ... the same value as the form IDL attribute on that fieldset
        // element. Otherwise, it must return null." Its parent, and not an ancestor.
        IHtmlLegendElement legend => legend.ParentElement is IHtmlFieldSetElement fieldSet ? Of(fieldSet) : null,

        // https://html.spec.whatwg.org/multipage/form-elements.html#dom-option-form — "let select be this's
        // nearest ancestor select ... return select's form owner", which is what answers for an option inside
        // an optgroup and answers null for one in no select at all.
        IHtmlOptionElement option => NearestSelect(option) is { } select ? Of(select) : null,

        _ => Of(element),
    };

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#form-associated-element — the elements that can
    /// have a form owner at all, which is why a <c>div</c> inside a <c>form</c> has none and resolves an
    /// unqualified name in a handler attribute against the document instead.
    /// </summary>
    internal static bool IsFormAssociated(IElement element) => IsListed(element) || element is IHtmlImageElement;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#category-listed — the elements that carry a
    /// <c>form</c> content attribute, and so the only ones an explicit association is read for. <c>img</c> is
    /// form-associated without being listed, so its owner is only ever an ancestor. <c>keygen</c> is the one
    /// entry no current standard defines: HTML 5.1 section 4.10.13 made it a listed element, AngleSharp still
    /// models one, and taking it out of this set would be a new rule rather than the standard's.
    /// </summary>
    internal static bool IsListed(IElement element) => element is IHtmlButtonElement
        or IHtmlFieldSetElement
        or IHtmlInputElement
        or IHtmlObjectElement
        or IHtmlOutputElement
        or IHtmlSelectElement
        or IHtmlTextAreaElement
        or IHtmlKeygenElement;

    /// <summary>Steps 4 and 5 of "reset the form owner", which is the whole of what a getter can observe.</summary>
    /// <remarks>
    /// Steps 1 to 3 are the algorithm's bookkeeping over a <i>stored</i> owner — the parser-inserted flag, and
    /// the early return that keeps an association a mutation did not disturb. Nothing here stores one: the
    /// owner is computed at every read, so the two steps that decide it are the only two to run, and what they
    /// answer is what a stored owner would have been reset to.
    /// </remarks>
    private static IHtmlFormElement? Resolve(IElement element, bool listed)
    {
        // Step 4, and it is the *presence* of the attribute that selects this branch. So `form=""` leaves the
        // owner null rather than falling through: no element's ID can equal the empty string, DOM unsetting
        // the ID of an element whose `id` attribute is empty. Step 5's "otherwise" is an otherwise of this
        // step's condition and not of its result, which is what gives a missing id, a non-form target and the
        // empty string the same answer — none — while a form still contains the control.
        if (listed && element.HasAttribute(FormAttribute) && IsConnected(element))
        {
            var id = element.GetAttribute(FormAttribute) ?? string.Empty;

            // "If the first element in element's tree, in tree order, to have an ID that is identical to
            // element's form content attribute's value, is a form element": the lookup itself is DOM's and
            // stays AngleSharp's, over the element's own tree — its shadow root when it is in one, and never
            // the document tree above that root.
            return id.Length > 0 && RootOf(element) is INonElementParentNode root
                ? root.GetElementById(id) as IHtmlFormElement
                : null;
        }

        // Step 5: "if element has an ancestor form element, then associate element with the nearest such
        // ancestor form element." The walk is over element parents, so it stops at a fragment or a shadow
        // root, neither of which can be a form.
        for (var current = element.ParentElement; current is not null; current = current.ParentElement)
        {
            if (current is IHtmlFormElement form)
            {
                return form;
            }
        }

        return null;
    }

    /// <summary>The nearest ancestor <c>select</c> of <paramref name="element"/>, or none.</summary>
    private static IHtmlSelectElement? NearestSelect(IElement element)
    {
        for (var current = element.ParentElement; current is not null; current = current.ParentElement)
        {
            if (current is IHtmlSelectElement select)
            {
                return select;
            }
        }

        return null;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#connected, shadow-including — the step-4 condition a disconnected subtree
    /// fails, which is why a control in a detached tree keeps the ancestor form it is inside however its
    /// <c>form</c> attribute reads.
    /// </summary>
    private static bool IsConnected(INode node)
    {
        for (INode? current = node; current is not null;)
        {
            if (current is IDocument)
            {
                return true;
            }

            current = current is IShadowRoot shadow ? shadow.Host : current.Parent;
        }

        return false;
    }

    /// <summary>The root of <paramref name="node"/>'s own tree, which a shadow boundary ends.</summary>
    private static INode RootOf(INode node)
    {
        while (node.Parent is { } parent)
        {
            node = parent;
        }

        return node;
    }
}
