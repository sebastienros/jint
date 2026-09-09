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
    // The pseudo-class HTML calls "default"; the name is taken by the helper which reads AngleSharp's own.
    private const string DefaultState = "default";
    private const string Disabled = "disabled";
    private const string Enabled = "enabled";
    private const string Link = "link";
    private const string Target = "target";
    private const string Visited = "visited";

    private static readonly ISelector _target = new TargetSelector();
    private readonly DefaultPseudoClassSelectorFactory _defaults = new();
    private readonly ISelector _anyLink;
    private readonly ISelector _default;
    private readonly ISelector _disabled;
    private readonly ISelector _enabled;
    private readonly ISelector _link;
    private readonly ISelector _visited;

    internal PagePseudoClassSelectorFactory()
    {
        _default = new DefaultSelector(Default(DefaultState));
        _enabled = new DisabledStateSelector(Default(Enabled), disabled: false);
        _disabled = new DisabledStateSelector(Default(Disabled), disabled: true);
        _link = new LinkStateSelector(Default(Link), visited: false);
        _visited = new LinkStateSelector(Default(Visited), visited: true);
        _anyLink = new AnyLinkSelector(Default(AnyLink), _link, _visited);
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

        private static bool IsOneOf(string value, string first, string second)
            => string.Equals(value, first, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, second, StringComparison.OrdinalIgnoreCase);
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
