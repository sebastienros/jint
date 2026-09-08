using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>The selector states whose default AngleSharp answer a page must refine.</summary>
internal sealed class PagePseudoClassSelectorFactory : IPseudoClassSelectorFactory
{
    private const string Enabled = "enabled";
    private const string Target = "target";

    private static readonly ISelector _target = new TargetSelector();
    private readonly DefaultPseudoClassSelectorFactory _defaults = new();
    private readonly ISelector _enabled;

    internal PagePseudoClassSelectorFactory()
    {
        _enabled = new EnabledSelector(_defaults.Create(Enabled)
            ?? throw new InvalidOperationException("AngleSharp no longer supplies the :enabled selector."));
    }

    /// <inheritdoc />
    public ISelector? Create(string name)
    {
        if (string.Equals(name, Target, StringComparison.OrdinalIgnoreCase))
        {
            return _target;
        }

        return string.Equals(name, Enabled, StringComparison.OrdinalIgnoreCase) ? _enabled : _defaults.Create(name);
    }

    /// <summary>Selectors §13.1: only elements which can be disabled can be enabled.</summary>
    private sealed class EnabledSelector(ISelector defaults) : ISelector
    {
        public string Text => defaults.Text;

        public Priority Specificity => defaults.Specificity;

        public bool Match(IElement element, IElement? scope)
            => element is not (IHtmlAnchorElement or IHtmlAreaElement or IHtmlLinkElement)
                && defaults.Match(element, scope);

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
