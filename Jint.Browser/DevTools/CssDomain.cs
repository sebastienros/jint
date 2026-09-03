using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Session;
using ProtocolCss = Jint.DevTools.Protocol.CSS;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>CSS</c> domain: the two questions AngleSharp.Css can answer about a node, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two read commands, and every editing command is honestly <c>-32601</c>.</b>
/// <c>getComputedStyleForNode</c> is the cascade AngleSharp.Css resolves — the same one
/// <c>window.getComputedStyle</c> answers from, so a front end and a page are told one story — and
/// <c>getInlineStylesForNode</c> is the element's own <c>style</c> attribute. What is <i>not</i> here is
/// everything that names a style sheet: <c>getMatchedStylesForNode</c>, <c>setStyleTexts</c>,
/// <c>addRule</c>, the rule-usage tracking and the <c>styleSheetAdded</c> stream. Each of them needs a
/// stable identifier for a sheet and a range inside its source text, which means owning the CSSOM's
/// serialization end to end; that is AngleSharp's half of this package and re-implementing it is the one
/// thing <c>Jint.Browser</c> is not for.
/// </para>
/// <para>
/// <b>A computed value here has no layout behind it.</b> A property the style sheets, the inline style or
/// the user-agent defaults settled resolves; a <i>used</i> value that would need a box — a percentage width,
/// anything resolved against a containing block — is the empty string.
/// <c>Dom/Views/ReadOnlyStyleDeclaration</c> says the same thing about the script-side member, and both are
/// the same declaration.
/// </para>
/// <para>
/// <b>The one thing that can go wrong is AngleSharp.Css not being registered</b>, and it is not a
/// hypothetical: <c>ComputeCurrentStyle()</c> throws <c>InvalidOperationException("Sequence contains no
/// elements")</c> rather than answering an empty declaration when the CSS services are absent
/// (<c>Accessibility/ElementVisibility</c> carries the same guard for the same reason). A page's own
/// browsing context always registers them; a refusal names the cause rather than erupting.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/CSS/"/>.
/// </para>
/// </remarks>
internal sealed class CssDomain : CSSDomainBase
{
    private readonly PageTarget _target;

    internal CssDomain(PageTarget target)
    {
        _target = target;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// No <c>styleSheetAdded</c> follows, because no style sheet is published: a client is told about the
    /// sheets it can then read and edit, and there are none it can.
    /// </remarks>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/CSS/#method-getComputedStyleForNode — every
    /// property the cascade settled for one element.
    /// </summary>
    protected override ValueTask<ProtocolCss.GetComputedStyleForNodeResponse> GetComputedStyleForNodeAsync(
        ProtocolCss.GetComputedStyleForNodeRequest parameters,
        CommandContext context)
    {
        var element = Element(parameters.NodeId);
        var computed = Computed(element);
        var properties = new List<ProtocolCss.CSSComputedStyleProperty>(computed.Length);

        for (var i = 0; i < computed.Length; i++)
        {
            var name = computed[i];
            properties.Add(new ProtocolCss.CSSComputedStyleProperty
            {
                Name = name,
                Value = computed.GetPropertyValue(name) ?? "",
            });
        }

        return new ValueTask<ProtocolCss.GetComputedStyleForNodeResponse>(new ProtocolCss.GetComputedStyleForNodeResponse
        {
            ComputedStyle = [.. properties],

            // The appearance-base flag is a Chrome rendering detail about form-control styling; there is no
            // rendering, so the honest answer is the one a control with the classic appearance gives.
            ExtraFields = new ProtocolCss.ComputedStyleExtraFields { IsAppearanceBase = false },
        });
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/CSS/#method-getInlineStylesForNode — what the
    /// element's own <c>style</c> attribute declares.
    /// </summary>
    /// <remarks>
    /// <c>attributesStyle</c> is absent rather than empty: it is the declaration a browser synthesizes from
    /// presentational attributes such as <c>&lt;body bgcolor&gt;</c> and <c>&lt;td width&gt;</c>, and
    /// AngleSharp maps those in its default sheet rather than into a declaration this can publish. An element
    /// with no <c>style</c> attribute answers an empty declaration, which is what Chrome does.
    /// </remarks>
    protected override ValueTask<ProtocolCss.GetInlineStylesForNodeResponse> GetInlineStylesForNodeAsync(
        ProtocolCss.GetInlineStylesForNodeRequest parameters,
        CommandContext context)
    {
        var element = Element(parameters.NodeId);
        var inline = element.GetStyle();
        var properties = new List<ProtocolCss.CSSProperty>(inline?.Length ?? 0);

        for (var i = 0; i < (inline?.Length ?? 0); i++)
        {
            var name = inline![i];
            var value = inline.GetPropertyValue(name) ?? "";

            properties.Add(new ProtocolCss.CSSProperty
            {
                Name = name,
                Value = value,
                Important = string.Equals(inline.GetPropertyPriority(name), "important", StringComparison.Ordinal),
                Text = name + ": " + value,
            });
        }

        return new ValueTask<ProtocolCss.GetInlineStylesForNodeResponse>(new ProtocolCss.GetInlineStylesForNodeResponse
        {
            InlineStyle = new ProtocolCss.CSSStyle
            {
                CssProperties = [.. properties],
                ShorthandEntries = [],
                CssText = element.GetAttribute("style") ?? "",
            },
        });
    }

    /// <summary>The element a <c>nodeId</c> names, in the <c>DOM</c> domain's own wording.</summary>
    private IElement Element(int nodeId)
    {
        var node = _target.Nodes.ByNodeId(nodeId) ?? Throw.ServerError<INode>("Could not find node with given id");
        return node as IElement ?? Throw.ServerError<IElement>("Node is not an Element");
    }

    /// <summary>The cascade for one element, or a refusal naming the service that is missing.</summary>
    private static ICssStyleDeclaration Computed(IElement element)
    {
        try
        {
            return element.ComputeCurrentStyle();
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException)
        {
            return Throw.ServerError<ICssStyleDeclaration>(
                "Computed style is not available",
                "the document's browsing context has no AngleSharp.Css services registered, so the cascade cannot be resolved");
        }
    }
}
