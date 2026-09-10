using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>The selector states whose default AngleSharp answer a page must refine.</summary>
internal sealed class PagePseudoClassSelectorFactory : IPseudoClassSelectorFactory
{
    private const string AnyLink = "any-link";
    private const string Closed = "closed";
    // The pseudo-class HTML calls "default"; the name is taken by the helper which reads AngleSharp's own.
    private const string DefaultState = "default";
    private const string Disabled = "disabled";
    private const string Enabled = "enabled";
    private const string InRange = "in-range";
    private const string Invalid = "invalid";
    private const string Link = "link";
    private const string Open = "open";
    private const string OutOfRange = "out-of-range";
    private const string Target = "target";
    private const string Valid = "valid";
    private const string Visited = "visited";

    private static readonly ISelector _target = new TargetSelector();
    private readonly DefaultPseudoClassSelectorFactory _defaults = new();
    private readonly ISelector _anyLink;
    private readonly ISelector _closed;
    private readonly ISelector _default;
    private readonly ISelector _disabled;
    private readonly ISelector _enabled;
    private readonly ISelector _inRange;
    private readonly ISelector _invalid;
    private readonly ISelector _link;
    private readonly ISelector _open;
    private readonly ISelector _outOfRange;
    private readonly ISelector _valid;
    private readonly ISelector _visited;

    internal PagePseudoClassSelectorFactory()
    {
        _default = new DefaultSelector(Default(DefaultState));
        _enabled = new DisabledStateSelector(Default(Enabled), disabled: false);
        _disabled = new DisabledStateSelector(Default(Disabled), disabled: true);
        _link = new LinkStateSelector(Default(Link), visited: false);
        _visited = new LinkStateSelector(Default(Visited), visited: true);
        _anyLink = new AnyLinkSelector(Default(AnyLink), _link, _visited);
        _open = new OpenStateSelector(Default(Open), Open, closed: false);

        // The one selector here with no AngleSharp default to keep: :closed is not in
        // DefaultPseudoClassSelectorFactory's table at all, so before this the whole selector failed to
        // parse and every API a page could spell it in raised a SyntaxError.
        _closed = new OpenStateSelector(defaults: null, Closed, closed: true);
        _inRange = new RangeStateSelector(Default(InRange), outOfRange: false);
        _outOfRange = new RangeStateSelector(Default(OutOfRange), outOfRange: true);
        _valid = new ValidityStateSelector(Default(Valid), invalid: false);
        _invalid = new ValidityStateSelector(Default(Invalid), invalid: true);
    }

    /// <inheritdoc />
    public ISelector? Create(string name)
    {
        if (string.Equals(name, Target, StringComparison.OrdinalIgnoreCase))
        {
            return _target;
        }

        if (string.Equals(name, Enabled, StringComparison.OrdinalIgnoreCase))
        {
            return _enabled;
        }

        if (string.Equals(name, Disabled, StringComparison.OrdinalIgnoreCase))
        {
            return _disabled;
        }

        if (string.Equals(name, DefaultState, StringComparison.OrdinalIgnoreCase))
        {
            return _default;
        }

        if (string.Equals(name, Link, StringComparison.OrdinalIgnoreCase))
        {
            return _link;
        }

        if (string.Equals(name, AnyLink, StringComparison.OrdinalIgnoreCase))
        {
            return _anyLink;
        }

        if (string.Equals(name, Open, StringComparison.OrdinalIgnoreCase))
        {
            return _open;
        }

        if (string.Equals(name, Closed, StringComparison.OrdinalIgnoreCase))
        {
            return _closed;
        }

        if (string.Equals(name, InRange, StringComparison.OrdinalIgnoreCase))
        {
            return _inRange;
        }

        if (string.Equals(name, OutOfRange, StringComparison.OrdinalIgnoreCase))
        {
            return _outOfRange;
        }

        if (string.Equals(name, Valid, StringComparison.OrdinalIgnoreCase))
        {
            return _valid;
        }

        if (string.Equals(name, Invalid, StringComparison.OrdinalIgnoreCase))
        {
            return _invalid;
        }

        return string.Equals(name, Visited, StringComparison.OrdinalIgnoreCase) ? _visited : _defaults.Create(name);
    }

    /// <summary>Selectors §8.1: <c>:any-link</c> is exactly <c>:is(:link, :visited)</c>.</summary>
    private sealed class AnyLinkSelector(ISelector defaults, ISelector link, ISelector visited) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
            => link.Match(element, scope) || visited.Match(element, scope);

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);
    }

    private ISelector Default(string name)
        => _defaults.Create(name)
            ?? throw new InvalidOperationException($"AngleSharp no longer supplies the :{name} selector.");

    /// <summary>
    /// HTML §4.16.3: <c>:default</c> matches a submit button which is the default button of its form owner,
    /// an <c>input</c> the <c>checked</c> attribute applies to and which carries it, and an <c>option</c>
    /// carrying <c>selected</c>. Nothing here reads a control's current state: all three are about the
    /// markup a form would be reset to.
    /// </summary>
    private sealed class DefaultSelector(ISelector defaults) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
        {
            if (element is IHtmlOptionElement)
            {
                return element.HasAttribute("selected");
            }

            // §4.10.5.3.5 and §4.10.5.3.6: `checked` applies to exactly these two type states.
            if (element is IHtmlInputElement input && IsOneOf(input.Type, "checkbox", "radio"))
            {
                return element.HasAttribute("checked");
            }

            return IsASubmitButton(element) && IsTheDefaultButtonOfItsForm(element);
        }

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);

        /// <summary>
        /// §4.10.6: a <c>button</c>'s <c>type</c> is an enumerated attribute whose missing <i>and</i>
        /// invalid value defaults are both Submit Button, so only the other two keywords are not one;
        /// §4.10.5.1.20 and §4.10.5.1.21 make <c>submit</c> and <c>image</c> inputs submit buttons too.
        /// </summary>
        private static bool IsASubmitButton(IElement element)
        {
            if (element is IHtmlButtonElement button)
            {
                return !IsOneOf(button.Type, "reset", "button");
            }

            return element is IHtmlInputElement input && IsOneOf(input.Type, "submit", "image");
        }

        /// <summary>
        /// §4.10.21.2: a form's default button is the first submit button in tree order whose form owner is
        /// that form — ownership, not containment, so the <c>form</c> attribute puts a button outside the
        /// form in the running and takes a contained one out of it. The scan stops at the first submit
        /// button the form owns, which is the answer either way, and only a submit button ever starts one.
        /// </summary>
        private static bool IsTheDefaultButtonOfItsForm(IElement element)
        {
            if (FormOwnerOf(element) is not { } form)
            {
                return false;
            }

            var root = element;
            while (root.ParentElement is { } parent)
            {
                root = parent;
            }

            for (var candidate = root; candidate is not null; candidate = NextInTreeOrder(candidate, root))
            {
                if (IsASubmitButton(candidate) && ReferenceEquals(FormOwnerOf(candidate), form))
                {
                    return ReferenceEquals(candidate, element);
                }
            }

            return false;
        }

        private static IHtmlFormElement? FormOwnerOf(IElement element) => element switch
        {
            IHtmlButtonElement button => button.Form,
            IHtmlInputElement input => input.Form,
            _ => null,
        };

        /// <summary>The next element of <paramref name="root"/>'s subtree in tree order, or none.</summary>
        private static IElement? NextInTreeOrder(IElement element, IElement root)
        {
            if (element.FirstElementChild is { } child)
            {
                return child;
            }

            for (IElement? current = element;
                current is not null && !ReferenceEquals(current, root);
                current = current.ParentElement)
            {
                if (current.NextElementSibling is { } sibling)
                {
                    return sibling;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// HTML §4.16.3: <c>:enabled</c> matches every <c>button</c>, <c>input</c>, <c>select</c>,
    /// <c>textarea</c>, <c>optgroup</c>, <c>option</c> and <c>fieldset</c> element which is not actually
    /// disabled, and <c>:disabled</c> matches every element which is. Nothing else has a disabled state, so
    /// a link matches neither; §4.15 defines the state itself, and this page owns the whole of it rather
    /// than the link half of it.
    /// </summary>
    private sealed class DisabledStateSelector(ISelector defaults, bool disabled) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
            => disabled
                ? IsActuallyDisabled(element)
                : HasADisabledState(element) && !IsActuallyDisabled(element);

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);

        /// <summary>
        /// HTML §4.16.3's seven element types. A form-associated custom element belongs here too, and is
        /// left out until this package models one having a disabled state at all: the flag a definition
        /// declares is recorded and nothing consults it, so every such element is enabled either way.
        /// </summary>
        private static bool HasADisabledState(IElement element)
            => element is IHtmlButtonElement or IHtmlInputElement or IHtmlSelectElement or IHtmlTextAreaElement
                or IHtmlOptionsGroupElement or IHtmlOptionElement or IHtmlFieldSetElement;

        /// <summary>HTML §4.15: what it is for an element to be actually disabled.</summary>
        private static bool IsActuallyDisabled(IElement element)
        {
            // §4.10.19.5: a form control is disabled when the attribute is specified on it, whatever its
            // value, or when a disabled fieldset it descends from does not shelter it in a legend.
            if (element is IHtmlButtonElement or IHtmlInputElement or IHtmlSelectElement or IHtmlTextAreaElement)
            {
                return IsDisabledAttributeSpecified(element) || DescendsFromADisabledFieldset(element);
            }

            if (element is IHtmlOptionsGroupElement)
            {
                return IsDisabledAttributeSpecified(element) || NearestAncestorSelectIsDisabled(element);
            }

            // §4.10.10: an option is disabled by its own attribute or by the optgroup it is a child of.
            if (element is IHtmlOptionElement)
            {
                return IsDisabledAttributeSpecified(element)
                    || (element.ParentElement is IHtmlOptionsGroupElement group && IsDisabledAttributeSpecified(group))
                    || NearestAncestorSelectIsDisabled(element);
            }

            // §4.10.15: a fieldset is a disabled fieldset by the same two conditions a control is disabled by.
            return element is IHtmlFieldSetElement
                && (IsDisabledAttributeSpecified(element) || DescendsFromADisabledFieldset(element));
        }

        /// <summary>
        /// <c>disabled</c> is a boolean attribute, so an empty value states it as loudly as any other. This
        /// is the whole of the difference on <c>optgroup</c> and <c>fieldset</c>, where AngleSharp reads the
        /// value rather than the presence and <c>&lt;fieldset disabled&gt;</c> comes back enabled.
        /// </summary>
        private static bool IsDisabledAttributeSpecified(IElement element) => element.HasAttribute(Disabled);

        /// <summary>
        /// Every ancestor fieldset is considered, not only the nearest disabled one: a control inside an
        /// inner fieldset's legend is still disabled by an outer fieldset which has no legend of its own.
        /// The walk carries the child it came through, which is the fieldset's own child on the path, so
        /// comparing it with the first legend answers "is a descendant of it" without a second traversal.
        /// </summary>
        private static bool DescendsFromADisabledFieldset(IElement element)
        {
            var child = element;
            for (var ancestor = element.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
            {
                if (ancestor is IHtmlFieldSetElement
                    && IsDisabledAttributeSpecified(ancestor)
                    && !ReferenceEquals(child, FirstLegendChild(ancestor)))
                {
                    return true;
                }

                child = ancestor;
            }

            return false;
        }

        private static IElement? FirstLegendChild(IElement fieldset)
        {
            for (var child = fieldset.FirstElementChild; child is not null; child = child.NextElementSibling)
            {
                if (child is IHtmlLegendElement)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// §4.15 disables an <c>optgroup</c> or an <c>option</c> whose nearest ancestor <c>select</c> is
        /// disabled, which is how a control inside a disabled fieldset disables the list it owns.
        /// </summary>
        private static bool NearestAncestorSelectIsDisabled(IElement element)
        {
            for (var ancestor = element.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
            {
                if (ancestor is IHtmlSelectElement)
                {
                    return IsDisabledAttributeSpecified(ancestor) || DescendsFromADisabledFieldset(ancestor);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Selectors §8.2, HTML and SVG: every HTML <c>a</c> or <c>area</c> carrying an <c>href</c>, and every
    /// SVG <c>a</c> carrying an <c>href</c> or <c>xlink:href</c>, is in exactly one link-history state. This
    /// browser keeps no visited history, so every such hyperlink is unvisited.
    /// </summary>
    private sealed class LinkStateSelector(ISelector defaults, bool visited) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
        {
            if (element is IHtmlAnchorElement or IHtmlAreaElement)
            {
                return !visited && element.HasAttribute("href");
            }

            if (element is IHtmlElement)
            {
                return false;
            }

            if (IsSvgAnchor(element))
            {
                return !visited && HasSvgLinkAttribute(element) && !HasHyperlinkAncestor(element);
            }

            return defaults.Match(element, scope);
        }

        private static bool IsSvgAnchor(IElement element)
            => string.Equals(element.NamespaceUri, NamespaceNames.SvgUri, StringComparison.Ordinal)
                && string.Equals(element.LocalName, "a", StringComparison.Ordinal);

        private static bool HasSvgLinkAttribute(IElement element)
            => element.HasAttribute(null, "href")
                || element.HasAttribute(NamespaceNames.XLinkUri, "href");

        /// <summary>
        /// SVG 2 §16.2: a nested SVG <c>a</c> ignores its link attributes when any ancestor is a
        /// hyperlink. The parent walk also applies to detached subtrees and allocates no traversal state.
        /// </summary>
        private static bool HasHyperlinkAncestor(IElement element)
        {
            for (var ancestor = element.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
            {
                if (ancestor is IHtmlAnchorElement or IHtmlAreaElement)
                {
                    if (ancestor.HasAttribute("href"))
                    {
                        return true;
                    }
                }
                else if (IsSvgAnchor(ancestor) && HasSvgLinkAttribute(ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);
    }

    /// <summary>
    /// Selectors §10.5 and HTML §4.16.3: <c>:open</c> and <c>:closed</c> are the two states of an element
    /// which has both, so neither is the complement of the other over every element — only over the four
    /// categories HTML gives the pair. A <c>details</c> and a <c>dialog</c> are open exactly while they carry
    /// the boolean <c>open</c> attribute; the other two categories are a drop-down <c>select</c> whose
    /// drop-down box is open and an <c>input</c> whose picker is open, and both of those are opened by a user
    /// interface this browser does not present, so they are always in the closed state rather than in
    /// neither. AngleSharp's <c>IsOpen()</c> is a <c>return false</c> with a to-do beside it.
    /// </summary>
    private sealed class OpenStateSelector(ISelector? defaults, string name, bool closed) : ISelector
    {
        public string Text => defaults?.Text ?? ":" + name;

        public Priority Specificity => defaults?.Specificity ?? Priority.OneClass;

        public bool Match(IElement element, IElement? scope)
            => HasAnOpenAndAClosedState(element) && IsOpen(element) != closed;

        public void Accept(ISelectorVisitor visitor)
        {
            if (defaults is not null)
            {
                defaults.Accept(visitor);
                return;
            }

            visitor.PseudoClass(name);
        }

        /// <summary>HTML §4.16.3's four categories, which is what the pair is defined over.</summary>
        private static bool HasAnOpenAndAClosedState(IElement element)
            => element is IHtmlDetailsElement or IHtmlDialogElement
                || IsADropDownBox(element)
                || SupportsAPicker(element);

        /// <summary>
        /// §4.11.1 and §4.11.4: <c>open</c> is a boolean attribute on both elements, so its presence is the
        /// state. Nothing else here can be open, because opening it would take a user gesture at a rendering.
        /// </summary>
        private static bool IsOpen(IElement element)
            => element is IHtmlDetailsElement or IHtmlDialogElement && element.HasAttribute(Open);

        /// <summary>
        /// §4.10.7: a <c>select</c> is a drop-down box when it has no <c>multiple</c> attribute and its
        /// display size is 1, and the display size is the <c>size</c> attribute parsed as a non-negative
        /// integer — or, when there is none or it does not parse, 4 with <c>multiple</c> and 1 without.
        /// AngleSharp's <c>Size</c> answers 0 for an absent attribute, so the attribute is read here instead.
        /// </summary>
        private static bool IsADropDownBox(IElement element)
            => element is IHtmlSelectElement select && !select.IsMultiple && DisplaySizeOf(select) == 1;

        private static int DisplaySizeOf(IHtmlSelectElement select)
            => TryParseNonNegativeInteger(select.GetAttribute("size"), out var size) ? size : 1;

        /// <summary>
        /// §4.10.5: whether an <c>input</c> supports a picker is implementation-defined but for the File
        /// Upload state, where the standard requires one. This browser shows no picker of its own for any
        /// other type state, so File Upload is the whole of the category here.
        /// </summary>
        private static bool SupportsAPicker(IElement element)
            => element is IHtmlInputElement input
                && string.Equals(input.Type, "file", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// HTML's rules for parsing non-negative integers: leading ASCII whitespace, an optional <c>+</c> and
        /// then at least one ASCII digit, with anything after the digits ignored. A leading <c>-</c> or a
        /// first character that is not a digit is a failure, which is what leaves the display size at its
        /// default.
        /// </summary>
        private static bool TryParseNonNegativeInteger(string? value, out int result)
        {
            result = 0;

            if (value is null)
            {
                return false;
            }

            var at = 0;
            while (at < value.Length && IsAsciiWhitespace(value[at]))
            {
                at++;
            }

            if (at < value.Length && value[at] == '+')
            {
                at++;
            }

            if (at >= value.Length || !char.IsAsciiDigit(value[at]))
            {
                return false;
            }

            var parsed = 0L;
            while (at < value.Length && char.IsAsciiDigit(value[at]))
            {
                parsed = Math.Min((parsed * 10) + (value[at] - '0'), int.MaxValue);
                at++;
            }

            result = (int) parsed;
            return true;
        }

        private static bool IsAsciiWhitespace(char character)
            => character is '\t' or '\n' or '\f' or '\r' or ' ';
    }

    /// <summary>
    /// HTML §4.16.3: <c>:in-range</c> and <c>:out-of-range</c> match an element which is a <b>candidate for
    /// constraint validation</b> and <b>has range limitations</b>, and then split on whether it is suffering
    /// from an underflow or an overflow. AngleSharp's <c>IsInRange()</c> asks neither question — it is "any
    /// <c>IValidation</c> element which is neither overflowing nor underflowing" — so every input the
    /// <c>min</c> and <c>max</c> attributes do not apply to matched <c>:in-range</c>, disabled and read-only
    /// controls with them among them.
    /// </summary>
    private sealed class RangeStateSelector(ISelector defaults, bool outOfRange) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
        {
            if (element is not IHtmlInputElement input || !IsACandidate(input) || !HasRangeLimitations(input))
            {
                return false;
            }

            // §4.10.5.4: the Range state's value sanitization algorithm clamps the value to the nearest
            // boundary point, so a range control suffers neither an underflow nor an overflow whatever its
            // content attribute says. AngleSharp reads the attribute back unclamped and reports both.
            if (IsTheRangeState(input))
            {
                return !outOfRange;
            }

            var validity = input.Validity;
            return (validity.IsRangeUnderflow || validity.IsRangeOverflow) == outOfRange;
        }

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);

        /// <summary>
        /// §4.10.5.4: <c>min</c> and <c>max</c> apply to exactly seven type states, and only there can an
        /// element have a minimum or a maximum at all. The Range state has both by default — 0 and 100 — so
        /// it has range limitations whether or not either attribute is written.
        /// </summary>
        /// <remarks>
        /// The attributes are read by presence rather than by parsing their values, which is the one place
        /// this predicate is looser than the standard: a <c>min</c> that is not a valid string for the type
        /// gives the element no minimum, and answering that would mean a second implementation of HTML's six
        /// value formats beside AngleSharp's own input-type table. <c>Dom/divergences.md</c> records it.
        /// </remarks>
        private static bool HasRangeLimitations(IHtmlInputElement input)
        {
            if (IsTheRangeState(input))
            {
                return true;
            }

            if (!MinAndMaxApplyTo(input.Type))
            {
                return false;
            }

            return IsSpecified(input, "min") || IsSpecified(input, "max");
        }

        private static bool MinAndMaxApplyTo(string type)
            => IsOneOf(type, "date", "month")
                || IsOneOf(type, "week", "time")
                || IsOneOf(type, "datetime-local", "number");

        private static bool IsTheRangeState(IHtmlInputElement input)
            => string.Equals(input.Type, "range", StringComparison.OrdinalIgnoreCase);

        private static bool IsSpecified(IElement element, string name)
            => element.GetAttribute(name) is { Length: > 0 };
    }

    /// <summary>
    /// HTML §4.16.3: <c>:valid</c> matches an element which is a <b>candidate for constraint validation</b>
    /// and satisfies its constraints, a <c>form</c> which owns no failing candidate, and a <c>fieldset</c>
    /// with no failing candidate among its descendants; <c>:invalid</c> is the same three categories the
    /// other way round. AngleSharp's pair is <c>CheckValidity()</c> and its negation, and
    /// <c>CheckValidity()</c> is <c>WillValidate &amp;&amp; Validity.IsValid</c> — so an element §4.10.19.2
    /// <i>bars</i> from constraint validation answers <c>false</c> and comes back <c>:invalid</c>, where the
    /// standard has it match neither. A <c>fieldset</c> is barred and carries no constraints of its own, so
    /// it came back <c>:valid</c> whatever it contained.
    /// </summary>
    private sealed class ValidityStateSelector(ISelector defaults, bool invalid) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
        {
            // Before the IValidation arm, because a fieldset is one and is barred from constraint
            // validation itself: what decides it is what its descendants say.
            if (element is IHtmlFieldSetElement)
            {
                return AFailingCandidateIsADescendantOf(element) == invalid;
            }

            // A form's answer is AngleSharp's: it already walks the controls the form owns and asks each
            // one whether it will validate before it asks whether it is valid, which is the standard's
            // second category.
            if (element is IHtmlFormElement)
            {
                return defaults.Match(element, scope);
            }

            return element is IValidation validation
                && IsACandidate(validation)
                && validation.Validity.IsValid != invalid;
        }

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);

        /// <summary>
        /// Every descendant element, so a control inside a nested <c>fieldset</c> or a <c>legend</c> counts
        /// for the outer one too. A nested fieldset is skipped as a candidate rather than as a subtree,
        /// because it is barred from constraint validation and its own descendants are already on this walk.
        /// </summary>
        private static bool AFailingCandidateIsADescendantOf(IElement fieldset)
        {
            for (var candidate = NextInSubtree(fieldset, fieldset);
                candidate is not null;
                candidate = NextInSubtree(candidate, fieldset))
            {
                if (candidate is IValidation validation && IsACandidate(validation) && !validation.Validity.IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// §4.10.19.2: a submittable element is a candidate for constraint validation unless something bars it
    /// — being disabled or read-only, being an input in the Hidden, Reset Button or Button state, or having a
    /// <c>datalist</c> ancestor. AngleSharp's <c>WillValidate</c> is exactly that question and answers it
    /// well; what its pseudo-classes do wrong is fold the answer into <c>CheckValidity()</c>, where "barred"
    /// and "does not satisfy its constraints" become the same <c>false</c>.
    /// </summary>
    private static bool IsACandidate(IValidation validation) => validation.WillValidate;

    /// <summary>The next element of <paramref name="root"/>'s subtree in tree order, or none.</summary>
    private static IElement? NextInSubtree(IElement element, IElement root)
    {
        if (element.FirstElementChild is { } child)
        {
            return child;
        }

        for (IElement? current = element;
            current is not null && !ReferenceEquals(current, root);
            current = current.ParentElement)
        {
            if (current.NextElementSibling is { } sibling)
            {
                return sibling;
            }
        }

        return null;
    }

    private static bool IsOneOf(string value, string first, string second)
        => string.Equals(value, first, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, second, StringComparison.OrdinalIgnoreCase);

    /// <summary>Selectors §8.2: the target element of a document.</summary>
    private sealed class TargetSelector : ISelector
    {
        public string Text => ":" + Target;

        public Priority Specificity => Priority.OneClass;

        public bool Match(IElement element, IElement? scope)
        {
            if (element.Owner is not IHtmlDocument document)
            {
                return element.IsTarget();
            }

            if (UrlParser.Parse(document.Url)?.Fragment is not { Length: > 0 } fragment)
            {
                return false;
            }

            var indicated = Find(document, fragment);
            if (indicated is null)
            {
                var decoded = PercentEncoding.DecodeToString(fragment);
                indicated = string.Equals(decoded, fragment, StringComparison.Ordinal) ? null : Find(document, decoded);
            }

            return ReferenceEquals(element, indicated);
        }

        /// <summary>HTML's first potential indicated element in document tree order.</summary>
        private static IElement? Find(IHtmlDocument document, string fragment)
        {
            if (document.GetElementById(fragment) is { } byId)
            {
                return byId;
            }

            foreach (var candidate in document.All)
            {
                if (string.Equals(candidate.LocalName, "a", StringComparison.Ordinal)
                    && string.Equals(candidate.GetAttribute("name"), fragment, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        public void Accept(ISelectorVisitor visitor) => visitor.PseudoClass(Target);
    }
}
