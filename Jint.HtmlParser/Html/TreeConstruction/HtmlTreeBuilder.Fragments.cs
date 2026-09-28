using System;
using System.Threading;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    private Element? _fragmentContext;
    private Element? _fragmentShadowHost;
    private Element? _fragmentRoot;
    private DocumentFragment? _fragmentResult;
    private Element? _fragmentSourceContext;
    private Node? _fragmentAncestor;
    private int _fragmentAttributeIndex;
    private bool _fragmentBootstrapPending;

    internal bool FragmentBootstrapPending => _fragmentBootstrapPending;
    private Element AdjustedCurrent => _fragmentContext is not null && _open.Count == 1 ? _fragmentContext : Current;

    internal void InitializeFragment(Element context, DocumentFragment result)
    {
        _fragmentSourceContext = context;
        if (_context.AllowDeclarativeShadowRoots) _fragmentShadowHost = context;
        _fragmentContext = _document.CreateParsedElement(context.NamespaceUri, context.LocalName, context.Prefix, null);
        _fragmentResult = result;
        _fragmentAncestor = context;
        _fragmentBootstrapPending = true;
        var textMode = context.NamespaceUri != Namespaces.Html ? HtmlTextMode.Data : HtmlFragmentTextModeLookup.Match(context.LocalName) switch
        {
            HtmlFragmentTextModeKind.Title or HtmlFragmentTextModeKind.Textarea => HtmlTextMode.RcData,
            HtmlFragmentTextModeKind.Style or HtmlFragmentTextModeKind.Xmp or HtmlFragmentTextModeKind.Iframe or HtmlFragmentTextModeKind.Noembed or HtmlFragmentTextModeKind.Noframes => HtmlTextMode.RawText,
            HtmlFragmentTextModeKind.Script => HtmlTextMode.ScriptData,
            HtmlFragmentTextModeKind.Noscript when _scriptingEnabled => HtmlTextMode.RawText,
            HtmlFragmentTextModeKind.Plaintext => HtmlTextMode.PlainText,
            _ => HtmlTextMode.Data
        };
        _tokenizer.InitializeFragmentTextMode(textMode);
    }

    // Bootstrap scans yield with the same budget as token/tree work. The external
    // context is neither pushed nor mutated, and its ancestors do not count as depth.
    internal void AdvanceFragmentBootstrap(long quota, CancellationToken cancellationToken)
    {
        _remaining = quota;
        _cancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        if (_fragmentRoot is null)
        {
            CheckDepth();
            _fragmentRoot = _document.CreateParsedElement(Namespaces.Html, "html", null, null);
            _document.AppendParsedChild(_fragmentRoot);
            Push(_fragmentRoot);
            if (_fragmentContext is { NamespaceUri: Namespaces.Html, LocalName: "template" })
            {
                _templateModes.Add(Mode.InTemplate);
                Charge(1);
            }
        }
        // Only annotation-xml's fake creation-token encoding is consumed by the
        // current algorithms. Other attributes have no fragment bootstrap effect.
        while (_fragmentAttributeIndex < _fragmentSourceContext!.AttributeCount && _remaining > 0)
        {
            var attribute = _fragmentSourceContext.GetAttributeAt((uint) _fragmentAttributeIndex++)!;
            if (_fragmentContext is { NamespaceUri: Namespaces.MathMl, LocalName: "annotation-xml" } &&
                attribute.NamespaceUri is null && attribute.LocalName == "encoding" &&
                (AsciiEquals(attribute.Value, "text/html") || AsciiEquals(attribute.Value, "application/xhtml+xml")))
                _annotationXmlHtmlIntegration.Add(_fragmentContext);
            Charge(1L + attribute.LocalName.Length + attribute.Value.Length);
        }
        if (_fragmentAttributeIndex < _fragmentSourceContext.AttributeCount) return;
        while (_fragmentAncestor is not null && _remaining > 0)
        {
            var ancestor = _fragmentAncestor;
            _fragmentAncestor = ancestor.ParentNode;
            Charge(1);
            if (ancestor is Element { NamespaceUri: Namespaces.Html, LocalName: "form" } form)
            {
                _form = form;
                _fragmentAncestor = null;
            }
        }
        if (_fragmentAncestor is not null || _remaining <= 0) return;
        ResetInsertionMode();
        _fragmentSourceContext = null;
        _fragmentBootstrapPending = false;
    }

}
