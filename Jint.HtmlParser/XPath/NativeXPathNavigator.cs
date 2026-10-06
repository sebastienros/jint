using System.Xml;
using System.Xml.XPath;

namespace Jint.HtmlParser;

// XPathNavigator is a cursor, not a DOM wrapper: cloned cursors share only a read session.
internal sealed class NativeXPathNavigator : XPathNavigator, IXPathAncestorContext
{
    private readonly XPathReadSession _session;
    private object _position;
    private XPathNamespaceBinding[]? _namespaceAxis;
    private XPathNamespaceScope _namespaceScope;
    private int _namespaceIndex;
    private int _attributeIndex = -1;

    internal NativeXPathNavigator(XPathReadSession session, object position)
    {
        _session = session;
        _position = position;
        _session.Check();
    }

    private NativeXPathNavigator(NativeXPathNavigator source)
    {
        _session = source._session;
        _session.Check();
        _position = source._position;
        _namespaceAxis = source._namespaceAxis;
        _namespaceScope = source._namespaceScope;
        _namespaceIndex = source._namespaceIndex;
        _attributeIndex = source._attributeIndex;
    }

    private void Set(object position)
    {
        _session.Check();
        _position = position;
        _namespaceAxis = null;
        _attributeIndex = -1;
    }

    public override XPathNavigator Clone() => new NativeXPathNavigator(this);
    internal void CheckRead() => _session.Check();
    bool IXPathAncestorContext.HasAncestor(string localName, string namespaceUri, bool anyNamespace, bool nodeTest, bool includeSelf)
        => _session.HasAncestor(_position, localName, namespaceUri, anyNamespace, nodeTest, includeSelf);
    double IXPathAncestorContext.CountAncestors(string localName, string namespaceUri, bool anyNamespace, bool nodeTest, bool includeSelf)
        => _session.CountAncestors(_position, localName, namespaceUri, anyNamespace, nodeTest, includeSelf);
    internal void ResultWork(int units = 1) => _session.Work(units, XPathWorkStage.ResultMaterialization);
    internal void PublishResult() => _session.PublishResult();
    internal object EvaluatePrepared(NativeXPathExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _session.Check();
        var result = expression.EvaluatePrepared(prepared => base.Evaluate(prepared, null));
        _session.Check();
        return result;
    }

    private NotSupportedException OpaqueEvaluation()
    {
        _session.Check();
        return new NotSupportedException("Use NativeXPath.Compile/Evaluate/Select for guarded XPath evaluation.");
    }

    public override XPathExpression Compile(string xpath) => throw OpaqueEvaluation();
    public override object Evaluate(string xpath) => throw OpaqueEvaluation();
    public override object Evaluate(string xpath, IXmlNamespaceResolver? resolver) => throw OpaqueEvaluation();
    public override object Evaluate(XPathExpression expr) => throw OpaqueEvaluation();
    public override object Evaluate(XPathExpression expr, XPathNodeIterator? context) => throw OpaqueEvaluation();
    public override XPathNodeIterator Select(string xpath) => throw OpaqueEvaluation();
    public override XPathNodeIterator Select(string xpath, IXmlNamespaceResolver? resolver) => throw OpaqueEvaluation();
    public override XPathNodeIterator Select(XPathExpression expr) => throw OpaqueEvaluation();
    public override XPathNavigator? SelectSingleNode(string xpath) => throw OpaqueEvaluation();
    public override XPathNavigator? SelectSingleNode(string xpath, IXmlNamespaceResolver? resolver) => throw OpaqueEvaluation();
    public override XPathNavigator? SelectSingleNode(XPathExpression expression) => throw OpaqueEvaluation();
    public override bool Matches(string xpath) => throw OpaqueEvaluation();
    public override bool Matches(XPathExpression expr) => throw OpaqueEvaluation();

    public override XmlNameTable NameTable { get { _session.Check(); return _session.NameTable; } }
    public override object UnderlyingObject { get { _session.Check(); return _position; } }
    public override bool CanEdit { get { _session.Check(); return false; } }
    public override string BaseURI { get { _session.Check(); return ""; } }
    public override bool IsEmptyElement { get { _session.Check(); return false; } }

    public override XPathNodeType NodeType
    {
        get
        {
            _session.Check();
            return _position switch
            {
                Document or DocumentFragment => XPathNodeType.Root,
                Element => XPathNodeType.Element,
                Attr => XPathNodeType.Attribute,
                XPathNamespaceBinding => XPathNodeType.Namespace,
                Text or CDataSection => XPathNodeType.Text,
                Comment => XPathNodeType.Comment,
                ProcessingInstruction => XPathNodeType.ProcessingInstruction,
                _ => throw new InvalidOperationException("Unsupported XPath cursor position.")
            };
        }
    }

    public override string LocalName
    {
        get
        {
            _session.Check();
            var name = _position switch
            {
                Element element => element.LocalName,
                Attr attribute => attribute.LocalName,
                XPathNamespaceBinding binding => binding.Prefix,
                ProcessingInstruction pi => pi.Target,
                _ => ""
            };
            return _session.Atom(name);
        }
    }

    public override string Name
    {
        get
        {
            _session.Check();
            var name = _position switch
            {
                Element element => element.TagName,
                Attr attribute => attribute.Name,
                XPathNamespaceBinding binding => binding.Prefix,
                ProcessingInstruction pi => pi.Target,
                _ => ""
            };
            return _session.Atom(name);
        }
    }

    public override string Prefix
    {
        get
        {
            _session.Check();
            return _session.Atom(_position switch
            {
                Element element => element.Prefix ?? "",
                Attr attribute => attribute.Prefix ?? "",
                _ => ""
            });
        }
    }

    public override string NamespaceURI
    {
        get
        {
            _session.Check();
            return _session.Atom(_position switch
            {
                Element element => element.NamespaceUri ?? "",
                Attr attribute => attribute.NamespaceUri ?? "",
                _ => ""
            });
        }
    }

    public override string Value
    {
        get
        {
            _session.Check();
            var value = _position switch
            {
                Element element => _session.DescendantValue(element),
                Document document => _session.DescendantValue(document),
                DocumentFragment fragment => _session.DescendantValue(fragment),
                Attr attribute => attribute.Value,
                XPathNamespaceBinding binding => binding.NamespaceUri,
                Text or CDataSection => _session.TextValue((Node) _position),
                Node node => XPathReadSession.Data(node),
                _ => ""
            };
            _session.CopyWork(value);
            return value;
        }
    }

    public override string XmlLang
    {
        get
        {
            _session.Check();
            Node? current = _position switch
            {
                Attr attribute => attribute.OwnerElement,
                XPathNamespaceBinding binding => binding.OwnerElement,
                Node node => node,
                _ => null
            };
            while (current is not null)
            {
                _session.Work();
                if (current is Element element)
                {
                    foreach (var attribute in element.Attributes)
                    {
                        _session.Work(1 + attribute.LocalName.Length);
                        if (attribute.NamespaceUri == Namespaces.Xml && attribute.LocalName == "lang")
                        {
                            _session.CopyWork(attribute.Value);
                            return attribute.Value;
                        }
                    }
                }

                current = current.ParentNode;
            }

            _session.Check();
            return "";
        }
    }

    public override bool HasChildren
    {
        get
        {
            _session.Check();
            var result = _position is Node node && _session.First(node) is not null;
            _session.Check();
            return result;
        }
    }

    public override bool HasAttributes
    {
        get
        {
            _session.Check();
            var result = _position is Element element && _session.Attributes(element).Length != 0;
            _session.Check();
            return result;
        }
    }

    public override bool MoveToFirstChild()
    {
        _session.Check();
        if (_position is not Node node || node is not (Document or DocumentFragment or Element)) return false;
        var first = _session.First(node);
        if (first is null) { _session.Check(); return false; }
        Set(first);
        return true;
    }

    public override bool MoveToNext()
    {
        _session.Check();
        if (_position is not Node node || node is Document or DocumentFragment) return false;
        var next = _session.Next(node);
        if (next is null) { _session.Check(); return false; }
        Set(next);
        return true;
    }

    public override bool MoveToFollowing(XPathNodeType type, XPathNavigator? end)
    {
        _session.Check();
        if (end is NativeXPathNavigator native) native._session.Check();
        if (_session.DetachedAttributeRoot is not null) return false;
        var found = base.MoveToFollowing(type, end);
        _session.Check();
        return found;
    }

    public override bool MoveToFollowing(string localName, string namespaceURI, XPathNavigator? end)
    {
        _session.Check();
        if (end is NativeXPathNavigator native) native._session.Check();
        if (_session.DetachedAttributeRoot is not null) return false;
        var found = base.MoveToFollowing(localName, namespaceURI, end);
        _session.Check();
        return found;
    }

    public override bool MoveToPrevious()
    {
        _session.Check();
        if (_position is not Node node || node is Document or DocumentFragment) return false;
        var previous = _session.Previous(node);
        if (previous is null) { _session.Check(); return false; }
        Set(previous);
        return true;
    }

    public override bool MoveToParent()
    {
        _session.Check();
        _session.Work(1, XPathWorkStage.AncestorScan);
        var parent = _position switch
        {
            Attr attribute => attribute.OwnerElement,
            XPathNamespaceBinding binding => binding.OwnerElement,
            Node node => node.ParentNode,
            _ => null
        };
        if (parent is null)
        {
            _session.Work();
            _session.Check();
            return false;
        }
        Set(parent);
        return true;
    }

    public override void MoveToRoot()
    {
        _session.Check();
        Set(_session.RootIdentity);
    }

    public override bool MoveToFirstAttribute()
    {
        _session.Check();
        if (_position is not Element element) return false;
        var attributes = _session.Attributes(element);
        if (attributes.Length == 0) return false;
        Set(attributes[0]);
        _attributeIndex = 0;
        return true;
    }

    public override bool MoveToNextAttribute()
    {
        _session.Check();
        if (_position is not Attr attribute || attribute.OwnerElement is not { } owner) return false;
        var attributes = _session.Attributes(owner);
        var index = _attributeIndex >= 0 && _attributeIndex < attributes.Length && ReferenceEquals(attributes[_attributeIndex], attribute)
            ? _attributeIndex : _session.AttributeOrder(attribute);
        if (index < 0 || index + 1 == attributes.Length) return false;
        Set(attributes[index + 1]);
        _attributeIndex = index + 1;
        return true;
    }

    public override bool MoveToFirstNamespace(XPathNamespaceScope scope)
    {
        _session.Check();
        if (_position is not Element element) return false;
        var bindings = _session.Namespaces(element, scope);
        if (bindings.Length == 0) return false;
        Set(bindings[0]);
        _namespaceAxis = bindings;
        _namespaceScope = scope;
        _namespaceIndex = 0;
        return true;
    }

    public override bool MoveToNextNamespace(XPathNamespaceScope scope)
    {
        _session.Check();
        if (_position is not XPathNamespaceBinding binding) return false;
        var bindings = _namespaceAxis;
        if (bindings is null || _namespaceScope != scope)
        {
            bindings = _session.Namespaces(binding.OwnerElement, scope);
            var index = -1;
            for (var i = 0; i < bindings.Length; i++)
            {
                _session.Work(1 + bindings[i].Prefix.Length, XPathWorkStage.NamespaceScan);
                if (bindings[i].Prefix == binding.Prefix) { index = i; break; }
            }

            _session.Check();
            _namespaceIndex = index;
            _namespaceAxis = bindings;
            _namespaceScope = scope;
        }

        if (_namespaceIndex < 0 || _namespaceIndex + 1 >= bindings.Length) return false;
        _namespaceIndex++;
        _position = bindings[_namespaceIndex];
        _session.Check();
        return true;
    }

    public override bool MoveToId(string id)
    {
        _session.Check();
        ArgumentNullException.ThrowIfNull(id);
        var found = _session.FindId(id);
        if (found is null)
        {
            _session.Check();
            return false;
        }

        Set(found);
        return true;
    }

    public override bool MoveTo(XPathNavigator other)
    {
        _session.Check();
        if (other is not NativeXPathNavigator native) return false;
        native._session.Check();
        if (!ReferenceEquals(_session.RootIdentity, native._session.RootIdentity)) return false;
        if (native._position is XPathNamespaceBinding binding)
        {
            var rebound = _session.BindingFor(binding.OwnerElement, binding.Prefix, binding.NamespaceUri);
            if (rebound is null) return false;
            Set(rebound);
        }
        else
        {
            Set(native._position);
        }

        _attributeIndex = native._attributeIndex;
        _namespaceScope = native._namespaceScope;
        _namespaceIndex = native._namespaceIndex;
        if (_position is XPathNamespaceBinding current)
        {
            _namespaceAxis = _session.Namespaces(current.OwnerElement, _namespaceScope);
        }

        _session.Check();
        return true;
    }

    public override bool IsSamePosition(XPathNavigator other)
    {
        _session.Check();
        if (other is not NativeXPathNavigator native) return false;
        native._session.Check();
        if (!ReferenceEquals(_session.RootIdentity, native._session.RootIdentity)) return false;
        if (_position is XPathNamespaceBinding a && native._position is XPathNamespaceBinding b)
        {
            return ReferenceEquals(a.OwnerElement, b.OwnerElement) && a.Prefix == b.Prefix && a.NamespaceUri == b.NamespaceUri;
        }

        return ReferenceEquals(_position, native._position);
    }

    public override XmlNodeOrder ComparePosition(XPathNavigator? other)
    {
        _session.Check();
        if (other is not NativeXPathNavigator native) return XmlNodeOrder.Unknown;
        native._session.Check();
        if (!ReferenceEquals(_session.RootIdentity, native._session.RootIdentity)) return XmlNodeOrder.Unknown;
        if (IsSamePosition(other)) return XmlNodeOrder.Same;
        var left = Key(_position);
        var right = Key(native._position);
        var comparison = left.Node.CompareTo(right.Node);
        if (comparison == 0) comparison = left.Phase.CompareTo(right.Phase);
        if (comparison == 0) comparison = left.Index.CompareTo(right.Index);
        _session.Check();
        return comparison < 0 ? XmlNodeOrder.Before : XmlNodeOrder.After;
    }

    private (int Node, int Phase, int Index) Key(object position)
    {
        switch (position)
        {
            case XPathNamespaceBinding binding:
                return (_session.OrderOf(binding.OwnerElement), 1, _session.NamespaceOrder(binding));
            case Attr attribute:
                return (_session.OrderOf(attribute.OwnerElement!), 2, _session.AttributeOrder(attribute));
            default:
                return (_session.OrderOf((Node) position), 0, 0);
        }
    }

    public override string? LookupNamespace(string prefix)
    {
        _session.Check();
        var owner = ContextElement();
        if (owner is null)
        {
            return prefix switch
            {
                "xml" => _session.Atom(Namespaces.Xml),
                "xmlns" => _session.Atom(Namespaces.Xmlns),
                "" => _session.Atom(""),
                _ => null
            };
        }

        if (prefix == "xmlns") return _session.Atom(Namespaces.Xmlns);
        if (_session.ScopeOf(owner).Map.TryGetValue(prefix, out var uri) && uri.Length != 0)
        {
            return _session.Atom(uri);
        }

        return prefix.Length == 0 ? _session.Atom("") : null;
    }

    public override string? LookupPrefix(string namespaceURI)
    {
        _session.Check();
        if (namespaceURI == Namespaces.Xmlns) return _session.Atom("xmlns");
        var owner = ContextElement();
        if (namespaceURI.Length == 0)
        {
            if (owner is null || !_session.ScopeOf(owner).Map.TryGetValue("", out var defaultUri) || defaultUri.Length == 0)
            {
                return _session.Atom("");
            }

            return null;
        }

        if (owner is null) return namespaceURI == Namespaces.Xml ? _session.Atom("xml") : null;
        foreach (var binding in _session.Namespaces(owner, XPathNamespaceScope.All))
        {
            _session.Work(binding.Prefix.Length + binding.NamespaceUri.Length + 1);
            if (binding.NamespaceUri == namespaceURI) return _session.Atom(binding.Prefix);
        }

        _session.Check();
        return null;
    }

    public override IDictionary<string, string> GetNamespacesInScope(XmlNamespaceScope scope)
    {
        _session.Check();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var owner = ContextElement();
        if (owner is not null)
        {
            foreach (var binding in _session.Namespaces(owner, scope == XmlNamespaceScope.Local ? XPathNamespaceScope.Local :
                         scope == XmlNamespaceScope.ExcludeXml ? XPathNamespaceScope.ExcludeXml : XPathNamespaceScope.All))
            {
                _session.Work(binding.Prefix.Length + binding.NamespaceUri.Length + 1);
                result.Add(_session.Atom(binding.Prefix), _session.Atom(binding.NamespaceUri));
            }
        }
        else if (scope == XmlNamespaceScope.All)
        {
            result.Add(_session.Atom("xml"), _session.Atom(Namespaces.Xml));
        }

        _session.Check();
        return result;
    }

    private Element? ContextElement() => _position switch
    {
        Element element => element,
        Attr attribute => attribute.OwnerElement,
        XPathNamespaceBinding binding => binding.OwnerElement,
        Node node => FindAncestorElement(node.ParentNode),
        _ => null
    };

    private Element? FindAncestorElement(Node? node)
    {
        while (node is not null)
        {
            _session.Work();
            if (node is Element element) return element;
            node = node.ParentNode;
        }

        return null;
    }
}
