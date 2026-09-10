using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Events;
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
    private const string Focus = "focus";
    private const string FocusWithin = "focus-within";
    private const string InRange = "in-range";
    private const string Indeterminate = "indeterminate";
    private const string Invalid = "invalid";
    private const string Link = "link";
    private const string Open = "open";
    private const string OutOfRange = "out-of-range";
    private const string PlaceholderShown = "placeholder-shown";
    private const string ReadOnly = "read-only";
    private const string ReadWrite = "read-write";

    /// <summary>The content attribute, whose name is not the one either selector goes by.</summary>
    private const string ReadOnlyAttributeName = "readonly";
    private const string Target = "target";
    private const string Valid = "valid";
    private const string Visited = "visited";

    private static readonly ISelector _target = new TargetSelector();

    /// <summary>§4.10.5.4: the seven type states the <c>min</c> and <c>max</c> attributes apply to.</summary>
    private static readonly string[] _minAndMaxApplyTo =
        ["date", "month", "week", "time", "datetime-local", "number", "range"];

    /// <summary>§4.10.5.3.10: the seven type states the <c>placeholder</c> attribute applies to.</summary>
    private static readonly string[] _placeholderAppliesTo =
        ["text", "search", "url", "tel", "email", "password", "number"];

    /// <summary>§4.10.5.3.6: the twelve type states the <c>readonly</c> attribute applies to.</summary>
    private static readonly string[] _readOnlyAppliesTo =
    [
        "text", "search", "url", "tel", "email", "password",
        "date", "month", "week", "time", "datetime-local", "number",
    ];
    private readonly DefaultPseudoClassSelectorFactory _defaults = new();
    private readonly ISelector _anyLink;
    private readonly ISelector _closed;
    private readonly ISelector _default;
    private readonly ISelector _disabled;
    private readonly ISelector _enabled;
    private readonly ISelector _focus;
    private readonly ISelector _focusWithin;
    private readonly ISelector _inRange;
    private readonly ISelector _indeterminate;
    private readonly ISelector _invalid;
    private readonly ISelector _link;
    private readonly ISelector _open;
    private readonly ISelector _outOfRange;
    private readonly ISelector _placeholderShown;
    private readonly ISelector _readOnly;
    private readonly ISelector _readWrite;
    private readonly ISelector _valid;
    private readonly ISelector _visited;

    internal PagePseudoClassSelectorFactory(PageRuntime runtime)
    {
        // The page's own focus, resolved once. BrowserEventRealm is per engine and this factory is built per
        // document load on the page loop, so the realm is looked up here rather than on every element a
        // selector is matched against.
        var events = BrowserEventRealm.Of(runtime.Engine);
        _focus = new FocusSelector(Default(Focus), events);
        _focusWithin = new FocusWithinSelector(Default(FocusWithin), events);

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
        _indeterminate = new IndeterminateSelector(Default(Indeterminate));
        _placeholderShown = new PlaceholderShownSelector(Default(PlaceholderShown));

        // The disabled selector rather than a second reading of the attribute: §4.10.5.3.6's "mutable" is
        // "not read-only and not disabled", and being disabled is the whole of §4.15 that :disabled owns.
        _readWrite = new ReadWriteStateSelector(Default(ReadWrite), _disabled, readOnly: false);
        _readOnly = new ReadWriteStateSelector(Default(ReadOnly), _disabled, readOnly: true);
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

        if (string.Equals(name, Indeterminate, StringComparison.OrdinalIgnoreCase))
        {
            return _indeterminate;
        }

        if (string.Equals(name, PlaceholderShown, StringComparison.OrdinalIgnoreCase))
        {
            return _placeholderShown;
        }

        if (string.Equals(name, ReadWrite, StringComparison.OrdinalIgnoreCase))
        {
            return _readWrite;
        }

        if (string.Equals(name, ReadOnly, StringComparison.OrdinalIgnoreCase))
        {
            return _readOnly;
        }

        if (string.Equals(name, Focus, StringComparison.OrdinalIgnoreCase))
        {
            return _focus;
        }

        if (string.Equals(name, FocusWithin, StringComparison.OrdinalIgnoreCase))
        {
            return _focusWithin;
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
    /// HTML §4.16.3: <c>:focus</c> matches the element which <b>has the focus</b>, and Selectors §9.5 makes
    /// <c>:focus-within</c> that element together with every element containing it. AngleSharp answers both
    /// from <c>IElement.IsFocused</c>, a flag nothing in this package sets — its own
    /// <c>IHtmlElement.DoFocus()</c> assigns neither that flag nor <c>IDocument.ActiveElement</c>, which is
    /// why <see cref="FocusController"/> is the page's focus model — so before this every element answered
    /// <see langword="false"/> to both while <c>document.activeElement</c> named the focused one.
    /// </summary>
    /// <remarks>
    /// <c>:focus-visible</c> is deliberately left as AngleSharp's, and so matches nothing: Selectors §9.4
    /// makes it <c>:focus</c> <i>plus</i> "the UA has determined that a focus ring or other indicator should
    /// be drawn", which is a heuristic about a rendering this browser does not produce. Answering it as
    /// <c>:focus</c> would be a guess rather than a reading, and <c>Dom/divergences.md</c> records it.
    /// </remarks>
    private sealed class FocusSelector(ISelector defaults, BrowserEventRealm events) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope) => ReferenceEquals(events.FocusedElement, element);

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);
    }

    /// <summary>
    /// Selectors §9.5: an element which has the focus or contains an element which has it. The walk goes up
    /// from the focused element rather than down from the candidate, because focus is one element: an
    /// ancestor chain per match is the depth of the tree, where AngleSharp's own answer enumerates the
    /// candidate's whole subtree through a LINQ pipeline it allocates per element.
    /// </summary>
    private sealed class FocusWithinSelector(ISelector defaults, BrowserEventRealm events) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
        {
            for (var ancestor = events.FocusedElement; ancestor is not null; ancestor = ancestor.ParentElement)
            {
                if (ReferenceEquals(ancestor, element))
                {
                    return true;
                }
            }

            return false;
        }

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);
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

            if (!IsAnyOf(input.Type, _minAndMaxApplyTo))
            {
                return false;
            }

            return IsSpecified(input, "min") || IsSpecified(input, "max");
        }

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

    /// <summary>Whether <paramref name="value"/> is an ASCII case-insensitive match for one of the keywords.</summary>
    private static bool IsAnyOf(string value, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (string.Equals(value, keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The topmost element above <paramref name="element"/>, which is the tree the scans walk.</summary>
    private static IElement RootElementOf(IElement element)
    {
        var root = element;
        while (root.ParentElement is { } parent)
        {
            root = parent;
        }

        return root;
    }

    /// <summary>
    /// HTML §4.16.3: <c>:placeholder-shown</c> matches an <c>input</c> or a <c>textarea</c> whose placeholder
    /// is currently being presented to the user, which §4.10.5.3.10 and §4.10.11 make "the attribute applies,
    /// it is not empty, and the control's value is". AngleSharp's <c>IsPlaceholderShown()</c> asks any
    /// <c>input</c> for a non-empty placeholder and an empty value — so a submit button with a placeholder
    /// matches — and never answers for a <c>textarea</c> at all.
    /// </summary>
    private sealed class PlaceholderShownSelector(ISelector defaults) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
        {
            if (element is IHtmlTextAreaElement textArea)
            {
                return HasAPlaceholder(textArea) && string.IsNullOrEmpty(textArea.Value);
            }

            return element is IHtmlInputElement input
                && IsAnyOf(input.Type, _placeholderAppliesTo)
                && HasAPlaceholder(input)
                && string.IsNullOrEmpty(input.Value);
        }

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);

        /// <summary>An empty placeholder presents nothing, so it is not shown.</summary>
        private static bool HasAPlaceholder(IElement element)
            => element.GetAttribute("placeholder") is { Length: > 0 };
    }

    /// <summary>
    /// HTML §4.16.3's three <c>:read-write</c> categories — an <c>input</c> the <c>readonly</c> attribute
    /// applies to and which is mutable, a <c>textarea</c> with no <c>readonly</c> attribute which is not
    /// disabled, and an element which is an editing host or editable and is neither of those two — and
    /// <c>:read-only</c>, which matches <b>all other HTML elements</b>. That last word is load-bearing: an
    /// SVG or a MathML element is in neither class, where AngleSharp's <c>IsReadOnly()</c> falls through to
    /// <c>return true</c> for everything that is not an <c>IHtmlElement</c>. Its <c>:read-write</c> asks only
    /// "not disabled and not read-only", with no applicability test, so a checkbox is user-alterable.
    /// </summary>
    private sealed class ReadWriteStateSelector(ISelector defaults, ISelector disabled, bool readOnly) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
            => element is IHtmlElement && IsUserAlterable(element, scope) != readOnly;

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);

        private bool IsUserAlterable(IElement element, IElement? scope)
        {
            // §4.10.5.3.6: mutable is "the readonly attribute is not specified and the element is not
            // disabled", and being disabled is what the :disabled selector beside this one already decides.
            if (element is IHtmlInputElement input)
            {
                return IsAnyOf(input.Type, _readOnlyAppliesTo)
                    && !input.HasAttribute(ReadOnlyAttributeName)
                    && !disabled.Match(element, scope);
            }

            // §4.10.11: a textarea has no applicability question, only the attribute and the state.
            if (element is IHtmlTextAreaElement)
            {
                return !element.HasAttribute(ReadOnlyAttributeName) && !disabled.Match(element, scope);
            }

            return IsEditable(element);
        }

        /// <summary>
        /// The third category. <c>Events/ContentEditing.HostOf</c> is the package's own reading of
        /// <c>contenteditable</c> — its file records why AngleSharp's <c>IsContentEditable</c> cannot be used,
        /// since it answers <see langword="false"/> for the attribute written without a value — and returns
        /// the nearest editing host, which is what "is an editing host or editable" asks for. A document in
        /// design mode is an editing host of its own, so everything in it is editable.
        /// </summary>
        private static bool IsEditable(IElement element)
        {
            if (ContentEditing.HostOf(element) is not null)
            {
                return true;
            }

            return element.Owner is { } document
                && string.Equals(document.DesignMode, "on", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// HTML §4.16.3's three <c>:indeterminate</c> categories: a checkbox whose <c>indeterminate</c> IDL
    /// attribute is set, a radio button whose §4.10.5.1.16 radio button group holds no checked member, and a
    /// <c>progress</c> element with <b>no</b> <c>value</c> content attribute. AngleSharp has the first, reads
    /// the third as an attribute whose value is empty rather than one that is absent — so
    /// <c>&lt;progress value=""&gt;</c> is indeterminate there and determinate here — and has no radio rule at
    /// all, so every unchecked radio button in the document answered <see langword="false"/>.
    /// </summary>
    private sealed class IndeterminateSelector(ISelector defaults) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
        {
            if (element is IHtmlProgressElement)
            {
                return !element.HasAttribute("value");
            }

            if (element is not IHtmlInputElement input)
            {
                return false;
            }

            if (string.Equals(input.Type, "checkbox", StringComparison.OrdinalIgnoreCase))
            {
                return input.IsIndeterminate;
            }

            return IsARadioButton(input) && !TheRadioButtonGroupOfHasACheckedMember(input);
        }

        public void Accept(ISelectorVisitor visitor) => defaults.Accept(visitor);

        private static bool IsARadioButton(IHtmlInputElement input)
            => string.Equals(input.Type, "radio", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// §4.10.5.1.16: a radio button's group is every radio button in the same tree with the same form
        /// owner and a <c>name</c> that is a compatibility caseless match for its own. A button carrying no
        /// <c>name</c> matches that last condition with nothing, so its group is itself alone — which is why
        /// the scan starts from its own checkedness and only widens when there is a name to widen by.
        /// </summary>
        private static bool TheRadioButtonGroupOfHasACheckedMember(IHtmlInputElement radio)
        {
            if (radio.IsChecked)
            {
                return true;
            }

            if (radio.Name is not { Length: > 0 } name)
            {
                return false;
            }

            var owner = radio.Form;
            var root = RootElementOf(radio);

            for (var candidate = root; candidate is not null; candidate = NextInSubtree(candidate, root))
            {
                if (candidate is IHtmlInputElement other
                    && other.IsChecked
                    && IsARadioButton(other)
                    && ReferenceEquals(other.Form, owner)
                    && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

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
