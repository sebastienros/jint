using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Session;
using ProtocolCss = Jint.DevTools.Protocol.CSS;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>CSS</c> domain: what the native cascade can answer about a node, and which of a page's rules were
/// used.
/// </summary>
/// <remarks>
/// Computed values and rule matching use the page's native on-demand cascade. Inline reads parse only
/// the requested element's declaration block. Missing computation inputs and incomplete property grammars
/// propagate named failures through the protocol's ordinary error reply.
/// Editing commands and getMatchedStylesForNode remain outside this domain's implemented surface.
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/CSS/"/>.
/// </remarks>
internal sealed partial class CssDomain : CSSDomainBase
{
    private readonly PageTarget _target;

    internal CssDomain(PageTarget target)
    {
        _target = target;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A <c>styleSheetAdded</c> follows for every sheet of the document that is showing, which is what
    /// Chrome does and what a coverage client waits for before it asks for any text.
    /// </remarks>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        await AnnounceAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A disabled domain publishes nothing, so an open coverage window is closed with it rather than left
    /// recording into a report nobody can ask for — and the cascade seam is disarmed with it.
    /// </remarks>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        StopTracking();
        _announced.Clear();
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
        var settled = computed.Enumerate();
        var properties = new List<ProtocolCss.CSSComputedStyleProperty>(settled.Count);

        foreach (var property in settled)
        {
            var name = property.Name;
            properties.Add(new ProtocolCss.CSSComputedStyleProperty
            {
                Name = name,
                Value = property.Text,
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
    /// the native cascade does not synthesize that declaration. An element
    /// with no <c>style</c> attribute answers an empty declaration, which is what Chrome does.
    /// </remarks>
    protected override ValueTask<ProtocolCss.GetInlineStylesForNodeResponse> GetInlineStylesForNodeAsync(
        ProtocolCss.GetInlineStylesForNodeRequest parameters,
        CommandContext context)
    {
        var element = Element(parameters.NodeId);
        var work = Work();
        var inline = CssDeclarationBlock.Parse(element.GetAttribute("style") ?? "",
            CssDeclarationContext.Style, null, work, work.Token);
        var properties = new List<ProtocolCss.CSSProperty>(inline.Count);

        for (var i = 0; i < inline.Count; i++)
        {
            work.Charge(1);
            var name = inline.GetPropertyName(i);
            var value = inline.GetPropertyValue(name, work);

            properties.Add(new ProtocolCss.CSSProperty
            {
                Name = name,
                Value = value,
                Important = string.Equals(inline.GetPropertyPriority(name, work), "important", StringComparison.Ordinal),
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
    private Element Element(int nodeId)
    {
        var node = _target.Nodes.ByNodeId(nodeId) ?? Throw.ServerError<Node>("Could not find node with given id");
        return node as Element ?? Throw.ServerError<Element>("Node is not an Element");
    }

    /// <summary>The cascade for one element, or a refusal naming what could not be resolved.</summary>
    /// <remarks>
    /// A refusal rather than an empty declaration, because this domain has no way to say "some of it":
    /// a client reading an empty list would read it as a page that declares nothing.
    /// </remarks>
    private static NativeCssComputedStyle Computed(Element element)
        => Dom.Views.CssCascade.Of(element)
        ?? Throw.ServerError<NativeCssComputedStyle>(
            "Computed style is not available",
            "the document is not associated with a native styling host");
    private CssValueWork Work()
    {
        var realm = PageRuntime.Find(_target.Runtime.Engine)?.Dom;
        return new CssValueWork(realm?.CancellationToken ?? default, _target.Runtime.Engine.Constraints.Check);
    }
}
