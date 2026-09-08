using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>The selector states whose answer depends on the page environment.</summary>
internal sealed class PagePseudoClassSelectorFactory : IPseudoClassSelectorFactory
{
    private const string Target = "target";

    private static readonly ISelector _target = new TargetSelector();
    private readonly DefaultPseudoClassSelectorFactory _defaults = new();

    /// <inheritdoc />
    public ISelector? Create(string name)
        => string.Equals(name, Target, StringComparison.OrdinalIgnoreCase) ? _target : _defaults.Create(name);

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
