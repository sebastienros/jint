namespace Jint.HtmlParser;

/// <summary>Copies native tree state without routing through document factories.</summary>
internal static class NodeCloner
{
    // DOM Standard §4.4, cloning and importing nodes. The source is an existing
    // valid tree, but mutable data setters can admit values that creation rejects.
    internal static Node Clone(Node source, Document document, bool deep)
    {
        var root = CopySingle(source, document);
        if (!deep)
        {
            return root;
        }

        var pending = new Stack<(Node Source, Node Copy)>();
        pending.Push((source, root));
        while (pending.TryPop(out var pair))
        {
            var owner = pair.Copy as Document ?? pair.Copy.OwnerDocument!;
            for (var child = pair.Source.FirstChild; child is not null; child = child.NextSibling)
            {
                var copy = CopySingle(child, owner);
                pair.Copy.AppendClonedChild(copy);
                pending.Push((child, copy));
            }

            if (pair.Source is Element { TemplateContent: { } sourceContent } &&
                pair.Copy is Element { TemplateContent: { } copyContent })
            {
                var contentOwner = copyContent.OwnerDocument!;
                for (var child = sourceContent.FirstChild; child is not null; child = child.NextSibling)
                {
                    var copy = CopySingle(child, contentOwner);
                    copyContent.AppendClonedChild(copy);
                    pending.Push((child, copy));
                }
            }
        }

        return root;
    }

    internal static Attr CloneAttribute(Attr source, Document document)
        => new(document, source.NamespaceUri, source.LocalName, source.Prefix, source.Value);

    private static Node CopySingle(Node source, Document document)
    {
        switch (source)
        {
            case Document original:
                var clonedDocument = new Document(original.Kind, original.ContentType);
                clonedDocument.SetParserMode(original.Mode);
                clonedDocument.CopySkippedXmlEntitiesFrom(original);
                return clonedDocument;
            case Element original:
                var element = new Element(document, original.NamespaceUri, original.LocalName, original.Prefix);
                element.CopyAttributesFrom(original, document);
                return element;
            case Text original:
                return new Text(document, original.Data);
            case Comment original:
                return new Comment(document, original.Data);
            case CDataSection original:
                // The public setter permits a terminator after construction.
                return new CDataSection(document, original.Data, clone: true);
            case ProcessingInstruction original:
                return ProcessingInstruction.CopyTo(document, original);
            case DocumentType original:
                return new DocumentType(document, original.Name, original.PublicId, original.SystemId);
            case DocumentFragment:
                return new DocumentFragment(document);
            default:
                throw DomException.NotSupported();
        }
    }
}
