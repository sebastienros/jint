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
/// the user-agent defaults settled resolves; a percentage resolves against the page's viewport rather than
/// a containing block, which is what <c>Runtime/PageRenderDevice</c> reports.
/// <c>Dom/Views/ReadOnlyStyleDeclaration</c> says the same thing about the script-side member, and both are
/// the same declaration.
/// </para>
/// <para>
/// <b>The cascade can refuse to compute, and it is not a hypothetical</b>: a unit AngleSharp.Css has no
/// conversion for, or a document whose browsing context has no CSS services at all, is a CLR exception out
/// of <c>ComputeCurrentStyle()</c> rather than a declaration it skipped. <c>Dom/Views/CssCascade</c> is the
/// one guard every caller shares, and here a cascade it cannot compute is a refusal naming the cause rather
/// than an exception erupting into the protocol.
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
                Value = Dom.Views.CssCascade.ValueOf(computed, name) ?? "",
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

    /// <summary>The cascade for one element, or a refusal naming what could not be resolved.</summary>
    /// <remarks>
    /// A refusal rather than an empty declaration, because this domain has no way to say "some of it":
    /// a client reading an empty list would read it as a page that declares nothing.
    /// </remarks>
    private static ICssStyleDeclaration Computed(IElement element)
        => Dom.Views.CssCascade.Of(element)
        ?? Throw.ServerError<ICssStyleDeclaration>(
            "Computed style is not available",
            "the document's cascade could not be resolved: either its browsing context has no AngleSharp.Css "
            + "services registered, or a declaration in the matching cascade uses a unit AngleSharp.Css cannot convert");
}
