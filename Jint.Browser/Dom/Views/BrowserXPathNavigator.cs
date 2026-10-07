using System.Xml;
using System.Xml.XPath;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// Browser's namespace-ignoring XPath view over the guarded native navigator.
/// The adaptation is confined to this cursor; it never changes the native namespace model.
/// </summary>
internal sealed class BrowserXPathNavigator : XPathNavigator, IXPathAncestorContext
{
    private readonly NativeXPathNavigator _native;

    internal BrowserXPathNavigator(DomRealm realm, DomNodeIdentity context)
        : this((NativeXPathNavigator) (context.Attribute is { } attribute
            ? NativeXPath.CreateNavigator(attribute, (_, _) => realm.Engine.Constraints.Check(), realm.CancellationToken)
            : NativeXPath.CreateNavigator(context.Node!, (_, _) => realm.Engine.Constraints.Check(), realm.CancellationToken)))
    {
    }

    private BrowserXPathNavigator(NativeXPathNavigator native) => _native = native;

    bool IXPathAncestorContext.HasAncestor(string localName, string namespaceUri, bool anyNamespace, bool nodeTest, bool includeSelf)
    {
        _native.CheckRead();
        return (nodeTest || anyNamespace || namespaceUri.Length == 0) &&
            ((IXPathAncestorContext) _native).HasAncestor(localName, "", true, nodeTest, includeSelf);
    }

    double IXPathAncestorContext.CountAncestors(string localName, string namespaceUri, bool anyNamespace, bool nodeTest, bool includeSelf)
    {
        _native.CheckRead();
        return nodeTest || anyNamespace || namespaceUri.Length == 0
            ? ((IXPathAncestorContext) _native).CountAncestors(localName, "", true, nodeTest, includeSelf)
            : 0d;
    }

    internal void CheckRead() => _native.CheckRead();
    internal void ResultWork() => _native.ResultWork();
    internal void PublishResult() => _native.PublishResult();

    public override XPathNavigator Clone() => new BrowserXPathNavigator((NativeXPathNavigator) _native.Clone());
    public override object UnderlyingObject => _native.UnderlyingObject;
    public override XmlNameTable NameTable => _native.NameTable;
    public override XPathNodeType NodeType => _native.NodeType;
    public override string LocalName => _native.LocalName;
    public override string Prefix => _native.Prefix;
    public override string Value => _native.Value;
    public override string BaseURI => _native.BaseURI;
    public override bool IsEmptyElement => _native.IsEmptyElement;
    public override string XmlLang => _native.XmlLang;
    public override bool CanEdit => false;

    public override string Name => _native.UnderlyingObject is Element element
        ? NameTable.Add(element.LocalName) : _native.Name;

    public override string NamespaceURI
    {
        get
        {
            _native.CheckRead();
            return string.Empty;
        }
    }

    public override XmlNodeOrder ComparePosition(XPathNavigator? other)
    {
        _native.CheckRead();
        return other is BrowserXPathNavigator view ? _native.ComparePosition(view._native) : XmlNodeOrder.Unknown;
    }

    public override bool IsSamePosition(XPathNavigator other)
        => other is BrowserXPathNavigator view && _native.IsSamePosition(view._native);
    public override bool MoveTo(XPathNavigator other)
        => other is BrowserXPathNavigator view && _native.MoveTo(view._native);
    public override bool MoveToFirstAttribute() => _native.MoveToFirstAttribute();
    public override bool MoveToNextAttribute() => _native.MoveToNextAttribute();
    public override bool MoveToFirstChild() => _native.MoveToFirstChild();
    public override bool MoveToNext() => _native.MoveToNext();
    public override bool MoveToPrevious() => _native.MoveToPrevious();
    public override bool MoveToParent() => _native.MoveToParent();
    public override bool MoveToId(string id) => _native.MoveToId(id);
    public override void MoveToRoot() => _native.MoveToRoot();

    // The previous Browser navigator exposed no namespace axis. Preserve that
    // Browser policy here while the native navigator retains its complete axis.
    public override bool MoveToFirstNamespace(XPathNamespaceScope namespaceScope)
    {
        _native.CheckRead();
        return false;
    }

    public override bool MoveToNextNamespace(XPathNamespaceScope namespaceScope)
    {
        _native.CheckRead();
        return false;
    }
}
