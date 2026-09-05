using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Jint.Browser.Dom.Views;

namespace Jint.Browser.Accessibility;

/// <summary>
/// What "hidden" means to a browser that never lays a page out.
/// </summary>
/// <remarks>
/// Two things a real browser answers from its layout tree are answered from the cascade instead:
/// <c>display: none</c> is walked down from the ancestors because CSS does not inherit it, and
/// <c>visibility</c> is read per element because CSS does. Nothing here can know that an element is
/// off-screen, clipped or covered — those are layout facts, and a headless browser does not have them.
/// </remarks>
internal sealed class ElementVisibility
{
    private readonly bool _useComputedStyle;
    private bool _cascadeAvailable = true;
    private bool _cascadeAnswered;

    internal ElementVisibility(bool useComputedStyle) => _useComputedStyle = useComputedStyle;

    internal CssCascade.Traversal? CreateTraversal(IDocument? document)
        => _useComputedStyle && _cascadeAvailable ? CssCascade.Traversal.For(document) : null;

    /// <summary>
    /// Whether the CSS cascade answered at least once, so a caller can say which source a verdict came from.
    /// </summary>
    internal bool CascadeAvailable => _useComputedStyle && _cascadeAvailable;

    /// <summary>
    /// Returns the reason <paramref name="element"/> is itself hidden, or <see cref="AxIgnoredReason.None"/>.
    /// </summary>
    /// <remarks>
    /// Ancestors are not consulted: the tree walk carries an inherited verdict down, which is both cheaper
    /// than walking up per node and the only way <c>hiddenRoot</c> can name the ancestor that did it.
    /// </remarks>
    internal AxIgnoredReason ReasonFor(IElement element) => ReasonFor(element, ariaHiddenCounts: true, traversal: null);

    /// <summary>
    /// Returns the reason <paramref name="element"/> is not rendered, or <see cref="AxIgnoredReason.None"/>.
    /// </summary>
    /// <remarks>
    /// The same verdict without <c>aria-hidden</c>, which removes a node from the accessibility tree and
    /// changes nothing about the rendering. It is what the text and markdown extractors ask, because a
    /// decorative marker is still text on the page.
    /// </remarks>
    internal AxIgnoredReason RenderingReasonFor(IElement element, CssCascade.Traversal? traversal = null)
        => ReasonFor(element, ariaHiddenCounts: false, traversal);

    private AxIgnoredReason ReasonFor(IElement element, bool ariaHiddenCounts, CssCascade.Traversal? traversal)
    {
        if (element.HasAttribute("hidden"))
        {
            return AxIgnoredReason.Hidden;
        }

        if (ariaHiddenCounts && string.Equals(element.GetAttribute("aria-hidden"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return AxIgnoredReason.AriaHiddenElement;
        }

        var (display, visibility) = Style(element, traversal);

        if (string.Equals(display, "none", StringComparison.OrdinalIgnoreCase))
        {
            return AxIgnoredReason.NotRendered;
        }

        if (string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibility, "collapse", StringComparison.OrdinalIgnoreCase))
        {
            return AxIgnoredReason.NotVisible;
        }

        return AxIgnoredReason.None;
    }

    /// <summary>
    /// Reads the element's <c>display</c> and <c>visibility</c>, from the cascade when it is available and
    /// from the <c>style</c> content attribute when it is not.
    /// </summary>
    internal (string? Display, string? Visibility) Style(IElement element, CssCascade.Traversal? traversal = null)
    {
        if (_useComputedStyle && _cascadeAvailable)
        {
            if ((traversal is null ? CssCascade.Of(element) : traversal.Of(element)) is { } computed
                && Dom.Views.CssCascade.ValueOf(computed, "display") is { } display
                && Dom.Views.CssCascade.ValueOf(computed, "visibility") is { } visibility)
            {
                _cascadeAnswered = true;
                return (display, visibility);
            }

            Latch();
        }

        return InlineStyle(element);
    }

    /// <summary>
    /// Reads the element's declared <c>white-space</c>, or <see langword="null"/> when the cascade cannot
    /// answer.
    /// </summary>
    internal string? WhiteSpace(IElement element)
    {
        if (!_useComputedStyle || !_cascadeAvailable)
        {
            return null;
        }

        if (Dom.Views.CssCascade.Of(element) is { } computed
            && Dom.Views.CssCascade.ValueOf(computed, "white-space") is { } whiteSpace)
        {
            _cascadeAnswered = true;
            return whiteSpace;
        }

        Latch();
        return null;
    }

    /// <summary>
    /// Stops asking AngleSharp.Css, but only while it has never answered.
    /// </summary>
    /// <remarks>
    /// The two failures <c>Dom/Views/CssCascade</c> guards are not the same kind of thing. A document whose
    /// browsing context has no CSS services cannot answer for <i>any</i> element, and asking once per node
    /// of a whole tree walk would be one thrown exception per node — so the first refusal latches. A cascade
    /// that has already answered for some other element is available, and a refusal is then about this
    /// element's own declarations (a unit AngleSharp.Css cannot convert, and <c>width: 20ch</c> is ordinary
    /// modern CSS): latching there would take a page's <c>display: none</c> rules down with it.
    /// </remarks>
    private void Latch()
    {
        if (!_cascadeAnswered)
        {
            _cascadeAvailable = false;
        }
    }

    private static (string? Display, string? Visibility) InlineStyle(IElement element)
    {
        var style = element.GetAttribute("style");
        if (string.IsNullOrEmpty(style))
        {
            return (null, null);
        }

        string? display = null;
        string? visibility = null;

        foreach (var declaration in style.Split(';'))
        {
            var separator = declaration.IndexOf(':', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var name = declaration.AsSpan(0, separator).Trim();
            var value = declaration.AsSpan(separator + 1).Trim();

            if (name.Equals("display", StringComparison.OrdinalIgnoreCase))
            {
                display = value.ToString();
            }
            else if (name.Equals("visibility", StringComparison.OrdinalIgnoreCase))
            {
                visibility = value.ToString();
            }
        }

        return (display, visibility);
    }
}
