using System.Text;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>DOM §4.4's Node members over the one native identity, including Attr's Node contract.</summary>
internal static class DomNodeMembers
{
    internal static Node Root(Node node)
    {
        while (node.ParentNode is { } parent)
        {
            node = parent;
        }
        return node;
    }

    internal static JsValue GetRootNode(DomRealm realm, Node target, JsValue[] arguments)
    {
        var composed = TypeConverter.ToBoolean(DomConvert.DictionaryMember(arguments, 0, "composed"));
        var root = Root(target);
        while (composed && root is ShadowRoot shadow)
        {
            root = Root(shadow.Host);
        }
        return realm.WrapNode(root);
    }

    internal static JsValue GetRootNode(DomNodeObject self, JsValue[] arguments)
        => self.Node is { } node ? GetRootNode(self.DomRealm, node, arguments) : self;

    internal static JsValue ChildNodes(DomNodeObject self)
        => self.DomRealm.Wrap(DomChildNodeList.Of(self.DomTarget), DomInterfaces.NodeList);

    internal static string Name(DomNodeObject self) => Name(self.DomTarget);

    internal static string Name(object target) => target switch
    {
        Attr attribute => attribute.Name,
        Document => "#document",
        DocumentFragment => "#document-fragment",
        DocumentType doctype => doctype.Name,
        Text => "#text",
        CDataSection => "#cdata-section",
        Comment => "#comment",
        ProcessingInstruction instruction => instruction.Target,
        Element element when element.NamespaceUri == Namespaces.Html && element.OwnerDocument!.Kind == DocumentKind.Html
            => DomHostHooks.AsciiUppercase(element.TagName),
        Element element => element.TagName,
        _ => throw new InvalidOperationException("Unknown native Node kind."),
    };

    internal static string? Value(DomNodeObject self) => Value(self.DomTarget);

    internal static string? Value(object target) => target switch
    {
        Attr attribute => attribute.Value,
        Text text => text.Data,
        CDataSection cdata => cdata.Data,
        Comment comment => comment.Data,
        ProcessingInstruction instruction => instruction.Data,
        _ => null,
    };

    internal static JsValue SetValue(DomNodeObject self, string value)
    {
        switch (self.DomTarget)
        {
            case Attr attribute: attribute.Value = value; break;
            case Text text: text.Data = value; break;
            case CDataSection cdata: cdata.Data = value; break;
            case Comment comment: comment.Data = value; break;
            case ProcessingInstruction instruction:
                instruction.Data = value;
                DomProcessingInstructionAttributes.DataChanged(instruction);
                break;
        }
        return JsValue.Undefined;
    }

    internal static string? TextContent(DomNodeObject self)
    {
        if (self.DomTarget is not Element and not DocumentFragment)
        {
            return Value(self);
        }
        var text = new StringBuilder();
        var root = self.Node!;
        var current = root.FirstChild;
        while (current is not null)
        {
            if (current is Text data) text.Append(data.Data);
            else if (current is CDataSection cdata) text.Append(cdata.Data);
            if (current.FirstChild is { } child)
            {
                current = child;
                continue;
            }
            while (current.NextSibling is null && !ReferenceEquals(current.ParentNode, root))
                current = current.ParentNode!;
            current = current.NextSibling;
        }
        return text.ToString();
    }

    internal static JsValue SetTextContent(DomNodeObject self, string value)
    {
        if (self.DomTarget is Element or DocumentFragment)
        {
            self.Node!.ReplaceChildren(value.Length == 0 ? null : self.Node.OwnerDocument!.CreateTextNode(value));
            return JsValue.Undefined;
        }
        return SetValue(self, value);
    }

    internal static JsValue CloneNode(DomNodeObject self, JsValue[] arguments)
    {
        return self.Attribute is { } attribute
            ? self.DomRealm.WrapNodeValue(attribute.Clone())
            : CustomElements.CustomElementCreation.CloneNode(self.DomRealm, self.Node!, arguments);
    }

    internal static JsValue Contains(DomNodeObject self, JsValue[] arguments)
    {
        var other = DomBindings.NodeArgument(arguments, 0, "Node.contains");
        if (ReferenceEquals(self.DomTarget, other.DomTarget)) return JsBoolean.True;
        if (self.Node is null || other.Node is null) return JsBoolean.False;
        for (var node = other.Node.ParentNode; node is not null; node = node.ParentNode)
        {
            if (ReferenceEquals(node, self.Node)) return JsBoolean.True;
        }
        return JsBoolean.False;
    }

    internal static JsValue IsEqualNode(DomNodeObject self, JsValue[] arguments)
    {
        if (arguments.Length == 0 || arguments[0].IsNullOrUndefined()) return JsBoolean.False;
        var other = DomBindings.NodeArgument(arguments, 0, "Node.isEqualNode");
        return JsBoolean.Create(DomNodeEquality.AreEqual(self.DomTarget, other.DomTarget));
    }

    internal static JsValue IsSameNode(DomNodeObject self, JsValue[] arguments)
    {
        if (arguments.Length == 0 || arguments[0].IsNullOrUndefined()) return JsBoolean.False;
        return JsBoolean.Create(ReferenceEquals(self.DomTarget,
            DomBindings.NodeArgument(arguments, 0, "Node.isSameNode").DomTarget));
    }

    internal static JsValue IsConnected(DomNodeObject self)
    {
        var root = self.Node is { } node ? Root(node) : null;
        while (root is ShadowRoot shadow) root = Root(shadow.Host);
        return JsBoolean.Create(root is Document);
    }

    internal static JsValue Normalize(DomNodeObject self)
    {
        if (self.Node is { } node) NativeCharacterData.Normalize(node);
        return JsValue.Undefined;
    }

    internal static JsValue AppendChild(DomNodeObject self, JsValue[] arguments)
        => InsertBefore(self, arguments, append: true);

    internal static JsValue InsertBefore(DomNodeObject self, JsValue[] arguments, bool append = false)
    {
        var member = append ? "Node.appendChild" : "Node.insertBefore";
        var child = DomBindings.NodeArgument(arguments, 0, member);
        var reference = append || arguments.Length < 2 || arguments[1].IsNullOrUndefined()
            ? null : DomBindings.NodeArgument(arguments, 1, member);
        if (self.Node is null || child.Node is null || reference is { Node: null }) throw DomException.Hierarchy();
        CaptureBeforeAdoption(self, child.Node);
        self.Node.InsertBefore(child.Node, reference?.Node);
        return child;
    }

    internal static JsValue RemoveChild(DomNodeObject self, JsValue[] arguments)
    {
        var child = DomBindings.NodeArgument(arguments, 0, "Node.removeChild");
        if (self.Node is null || child.Node is null) throw DomException.NotFound();
        self.Node.RemoveChild(child.Node);
        return child;
    }

    internal static JsValue ReplaceChild(DomNodeObject self, JsValue[] arguments)
    {
        var child = DomBindings.NodeArgument(arguments, 0, "Node.replaceChild");
        var old = DomBindings.NodeArgument(arguments, 1, "Node.replaceChild");
        if (self.Node is null || child.Node is null) throw DomException.Hierarchy();
        if (old.Node is null) throw DomException.NotFound();
        CaptureBeforeAdoption(self, child.Node);
        self.Node.ReplaceChild(child.Node, old.Node);
        return old;
    }

    private static void CaptureBeforeAdoption(DomNodeObject destination, Node source)
    {
        var document = destination.Node as Document ?? destination.Node!.OwnerDocument;
        if (!ReferenceEquals(source.OwnerDocument, document))
        {
            destination.DomRealm.RecordSubtree(source);
        }
    }

    internal static JsValue BaseUri(DomNodeObject self)
    {
        var document = self.Attribute?.OwnerDocument ?? self.Node as Document ?? self.Node!.OwnerDocument!;
        return JsString.Create(DomDocumentState.BaseUri(document));
    }

    // DOM §4.4 compareDocumentPosition: attributes take their owner's position only for
    // this algorithm; neither the native links nor event paths acquire an attribute parent.
    internal static JsValue CompareDocumentPosition(DomNodeObject self, JsValue[] arguments)
    {
        var other = DomBindings.NodeArgument(arguments, 0, "Node.compareDocumentPosition");
        if (ReferenceEquals(self.DomTarget, other.DomTarget)) return JsNumber.Create(0);
        var firstAttribute = other.Attribute;
        var secondAttribute = self.Attribute;
        var first = firstAttribute?.OwnerElement ?? other.Node;
        var second = secondAttribute?.OwnerElement ?? self.Node;
        if (firstAttribute is not null && secondAttribute is not null && first is not null && ReferenceEquals(first, second))
        {
            foreach (var attribute in ((Element) first).Attributes)
            {
                if (ReferenceEquals(attribute, secondAttribute)) return JsNumber.Create(32 | 4);
                if (ReferenceEquals(attribute, firstAttribute)) return JsNumber.Create(32 | 2);
            }
        }
        if (first is null || second is null || !ReferenceEquals(Root(first), Root(second)))
            return JsNumber.Create(1 | 32 | (other.PositionOrder < self.PositionOrder ? 2 : 4));
        if (IsAncestor(first, second) && firstAttribute is null || ReferenceEquals(first, second) && secondAttribute is not null)
            return JsNumber.Create(8 | 2);
        if (IsAncestor(second, first) && secondAttribute is null || ReferenceEquals(first, second) && firstAttribute is not null)
            return JsNumber.Create(16 | 4);
        return JsNumber.Create(IsBefore(first, second) ? 2 : 4);
    }

    private static bool IsAncestor(Node ancestor, Node node)
    {
        for (var current = node.ParentNode; current is not null; current = current.ParentNode)
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    private static bool IsBefore(Node first, Node second)
    {
        var path = new HashSet<Node>();
        for (Node? current = first; current is not null; current = current.ParentNode) path.Add(current);
        var branch = second;
        while (branch.ParentNode is { } parent && !path.Contains(parent)) branch = parent;
        var common = branch.ParentNode;
        if (common is null) return false; // first is below second, so follows its ancestor.
        if (ReferenceEquals(first, common)) return true;
        var firstBranch = first;
        while (!ReferenceEquals(firstBranch.ParentNode, common)) firstBranch = firstBranch.ParentNode!;
        for (var child = common.FirstChild; child is not null; child = child.NextSibling)
        {
            if (ReferenceEquals(child, firstBranch)) return true;
            if (ReferenceEquals(child, branch)) return false;
        }
        throw new InvalidOperationException("Nodes with the same root have no common branch.");
    }

    // DOM §4.4's locate-a-namespace/prefix algorithms use an Attr's owner element for
    // namespace lookup, while getRootNode/contains above never turn that into a tree parent.
    private static Element? NamespaceElement(DomNodeObject self) => self.DomTarget switch
    {
        Attr attribute => attribute.OwnerElement,
        Element element => element,
        Document document => document.DocumentElement,
        _ => self.Node?.ParentNode as Element,
    };

    private static string? LookupNamespace(Element? element, string? prefix)
    {
        prefix = string.IsNullOrEmpty(prefix) ? null : prefix;
        while (element is not null)
        {
            if (element.NamespaceUri is not null && element.Prefix == prefix) return element.NamespaceUri;
            var declared = prefix is null
                ? element.GetAttributeNS(Namespaces.Xmlns, "xmlns")
                : element.GetAttributeNS(Namespaces.Xmlns, prefix);
            if (declared is not null) return declared.Length == 0 ? null : declared;
            element = element.ParentNode as Element;
        }
        return null;
    }

    internal static JsValue LookupNamespaceUri(DomNodeObject self, JsValue[] arguments)
        => DomConvert.NullableText(LookupNamespace(NamespaceElement(self),
            DomConvert.RequiredText(arguments, 0, "Node.lookupNamespaceURI")));

    internal static JsValue IsDefaultNamespace(DomNodeObject self, JsValue[] arguments)
        => JsBoolean.Create(LookupNamespace(NamespaceElement(self), null) ==
            DomConvert.RequiredText(arguments, 0, "Node.isDefaultNamespace"));

    internal static JsValue LookupPrefix(DomNodeObject self, JsValue[] arguments)
    {
        var namespaceUri = DomConvert.NullableText(arguments, 0);
        if (string.IsNullOrEmpty(namespaceUri)) return JsValue.Null;
        var original = NamespaceElement(self);
        for (var element = original; element is not null; element = element.ParentNode as Element)
        {
            if (element.NamespaceUri == namespaceUri && element.Prefix is { } prefix &&
                LookupNamespace(original, prefix) == namespaceUri) return JsString.Create(prefix);
            foreach (var attribute in element.Attributes)
            {
                if (attribute.NamespaceUri == Namespaces.Xmlns && attribute.Prefix == "xmlns" &&
                    attribute.Value == namespaceUri && LookupNamespace(original, attribute.LocalName) == namespaceUri)
                    return JsString.Create(attribute.LocalName);
            }
        }
        return JsValue.Null;
    }
}
