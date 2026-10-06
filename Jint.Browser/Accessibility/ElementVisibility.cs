using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Styling;

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
    private readonly DomReadWork? _work;
    private readonly NativeCssQueryDiagnostics? _diagnostics;
    private bool _cascadeAvailable = true;
    private bool _cascadeAnswered;

    internal ElementVisibility(bool useComputedStyle, DomReadWork? work = null,
        NativeCssQueryDiagnostics? diagnostics = null)
    {
        _useComputedStyle = useComputedStyle;
        _work = work;
        _diagnostics = diagnostics;
    }

    internal CssCascade.Traversal? CreateTraversal(Document? document)
    {
        _work?.Check();
        Action? checkpoint = _work is null ? null : _work.Check;
        var traversal = _useComputedStyle && _cascadeAvailable ? CssCascade.Traversal.For(document,
            scope: CssCascade.StyleScope.Visibility, diagnostics: _diagnostics,
            cancellationToken: _work?.Token ?? default, checkpoint: checkpoint) : null;
        _work?.Check();
        return traversal;
    }

    private NativeCssComputedStyle? ComputedOf(Element element, CssCascade.Traversal? traversal) =>
        (traversal ?? CreateTraversal(element.OwnerDocument))?.Of(element);

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
    internal AxIgnoredReason ReasonFor(Element element, CssCascade.Traversal? traversal = null)
        => ReasonFor(element, ariaHiddenCounts: true, traversal);

    /// <summary>
    /// Returns the reason <paramref name="element"/> is not rendered, or <see cref="AxIgnoredReason.None"/>.
    /// </summary>
    /// <remarks>
    /// The same verdict without <c>aria-hidden</c>, which removes a node from the accessibility tree and
    /// changes nothing about the rendering. It is what the text and markdown extractors ask, because a
    /// decorative marker is still text on the page.
    /// </remarks>
    internal AxIgnoredReason RenderingReasonFor(Element element, CssCascade.Traversal? traversal = null)
        => ReasonFor(element, ariaHiddenCounts: false, traversal);

    private AxIgnoredReason ReasonFor(Element element, bool ariaHiddenCounts, CssCascade.Traversal? traversal)
    {
        if (Attribute(element, "hidden") is not null)
        {
            return AxIgnoredReason.Hidden;
        }

        if (ariaHiddenCounts && string.Equals(Attribute(element, "aria-hidden"), "true", StringComparison.OrdinalIgnoreCase))
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
    internal (string? Display, string? Visibility) Style(Element element, CssCascade.Traversal? traversal = null)
    {
        if (_useComputedStyle && _cascadeAvailable)
        {
            _work?.Check();
            if (ComputedOf(element, traversal) is { } computed
                && Dom.Views.CssCascade.ValueOf(computed, "display") is { } display
                && Dom.Views.CssCascade.ValueOf(computed, "visibility") is { } visibility)
            {
                _work?.Check();
                _cascadeAnswered = true;
                return (display, visibility);
            }

            Latch();
        }

        _work?.Check();
        return InlineStyle(element);
    }

    /// <summary>
    /// Reads the element's computed <c>white-space-collapse</c>, or <see langword="null"/> when the cascade cannot
    /// answer.
    /// </summary>
    internal string? WhiteSpaceCollapse(Element element, CssCascade.Traversal? traversal = null)
    {
        if (!_useComputedStyle || !_cascadeAvailable)
        {
            return null;
        }

        _work?.Check();
        if (ComputedOf(element, traversal) is { } computed
            && Dom.Views.CssCascade.ValueOf(computed, "white-space-collapse") is { } whiteSpace)
        {
            _work?.Check();
            _cascadeAnswered = true;
            if (whiteSpace.Length != 0) return whiteSpace;
            // The renderless declaration store retains white-space as text, not a typed shorthand.
            return Dom.Views.CssCascade.ValueOf(computed, "white-space") switch
            {
                "pre" or "pre-wrap" or "break-spaces" => "preserve",
                "pre-line" => "preserve-breaks",
                _ => "collapse"
            };
        }

        Latch();
        _work?.Check();
        return null;
    }

    /// <summary>
    /// Stops querying the shared cascade only when it has never answered.
    /// </summary>
    /// <remarks>
    /// Once a cascade has answered, a later element-specific refusal must not disable visibility rules
    /// for the rest of the walk. Only an unavailable cascade may switch the entire query to inline fallback.
    /// </remarks>
    private void Latch()
    {
        if (!_cascadeAnswered)
        {
            _cascadeAvailable = false;
        }
    }

    private string? Attribute(Element element, string name)
        => _work is null ? element.GetAttribute(name) : _work.Attribute(element, name);

    private (string? Display, string? Visibility) InlineStyle(Element element)
    {
        var style = Attribute(element, "style");
        if (string.IsNullOrEmpty(style))
        {
            return (null, null);
        }

        // Charge the captured input to the host read budget; native parsing also polls below.
        if (_work is not null)
        {
            for (var i = 0; i < style.Length; i++) _work.Step();
        }
        // Inline-only extraction uses the same declaration filtering/importance as the cascade.
        var token = _work?.Token ?? default;
        var work = new CssValueWork(token, () => _work?.Check());
        var block = CssDeclarationBlock.Parse(style, CssDeclarationContext.Style, null, work, token);
        var display = block.ResolveProperty("display", work)?.Value;
        var visibility = block.ResolveProperty("visibility", work)?.Value;
        _work?.Check();
        return (display, visibility);
    }
}
