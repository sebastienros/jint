namespace Jint.HtmlParser;

/// <summary>Copies native tree state without routing through document factories.</summary>
internal static class NodeCloner
{
    // DOM Standard §4.4: a clonable shadow tree is copied even when light-tree
    // subtree is false. Frames keep deep chains off the CLR call stack.
    internal static Node Clone(Node source, Document document, bool deep,
        CustomElementRegistryIdentity? fallbackRegistry = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selectWork = new HtmlSelectWork(document.SelectWorkProbe, cancellationToken);
        if (source is ShadowRoot)
        {
            throw DomException.NotSupported();
        }

        var root = CopySingle(source, document, fallbackRegistry, cancellationToken);
        var pending = new Stack<Frame>();
        pending.Push(new Frame(source, root, deep, fallbackRegistry));
        while (pending.TryPop(out var frame))
        {
            selectWork.Step();
            if (frame.NextChild is { } child)
            {
                frame.NextChild = child.NextSibling;
                pending.Push(frame);
                var owner = frame.Copy as Document ?? frame.Copy.OwnerDocument!;
                var copy = CopySingle(child, owner, frame.FallbackRegistry, cancellationToken);
                frame.Copy.AppendClonedChild(copy);
                pending.Push(new Frame(child, copy, true, frame.FallbackRegistry));
                continue;
            }

            if (frame.Stage == 0)
            {
                frame.Stage = 1;
                pending.Push(frame);
                if (frame.Deep && frame.Source is Element { TemplateContent: { } sourceContent } &&
                    frame.Copy is Element { TemplateContent: { } copyContent })
                {
                    pending.Push(new Frame(sourceContent, copyContent, true, frame.FallbackRegistry));
                }

                continue;
            }

            if (frame.Stage == 1)
            {
                frame.Stage = 2;
                pending.Push(frame);
                if (frame.Source is Element { AttachedShadowRoot: { Clonable: true } sourceShadow } &&
                    frame.Copy is Element copyHost)
                {
                    var shadowRegistry = sourceShadow.CustomElementRegistry;
                    if (shadowRegistry is { IsScoped: false })
                    {
                        shadowRegistry = EffectiveGlobalRegistry(copyHost.OwnerDocument!);
                    }

                    var copyShadow = ShadowTree.AttachClone(copyHost,
                        new ShadowRootInit(sourceShadow.Mode, sourceShadow.DelegatesFocus,
                            sourceShadow.Serializable, sourceShadow.SlotAssignment, true),
                        shadowRegistry);
                    copyShadow.SetDeclarative(sourceShadow.Declarative);
                    copyShadow.SetKeepCustomElementRegistryNull(sourceShadow.KeepCustomElementRegistryNull);
                    pending.Push(new Frame(sourceShadow, copyShadow, true, null));
                }

                continue;
            }
        }

        selectWork.Check();
        return root;
    }

    internal static Attr CloneAttribute(Attr source, Document document)
        => new(document, source.NamespaceUri, source.LocalName, source.Prefix, source.Value, source.IsDtdId);

    private static Node CopySingle(Node source, Document document,
        CustomElementRegistryIdentity? fallbackRegistry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (source)
        {
            case Document original:
                var clonedDocument = new Document(original.Kind, original.ContentType,
                    original.CreationDefaultCustomElementRegistry);
                clonedDocument.SetParserMode(original.Mode);
                clonedDocument.CopySkippedXmlEntitiesFrom(original);
                clonedDocument.CopyXmlNotationsFrom(original);
                if (original.CustomElementRegistry is { IsScoped: true } scoped)
                {
                    clonedDocument.InitializeCustomElementRegistry(scoped);
                }

                return clonedDocument;
            case Element original:
                var element = new Element(document, original.NamespaceUri, original.LocalName, original.Prefix,
                    original.IsValue);
                element.CopyAttributesFrom(original, document, cancellationToken);
                HtmlCheckednessAlgorithms.CopyCheckedness(original, element);
                if (original is { NamespaceUri: Namespaces.Html, LocalName: "option" })
                    element.GetHtmlState()!.GetOptionState(cancellationToken)!.CopyFrom(original.GetHtmlState()!.GetOptionState(cancellationToken)!);
                if (original is { NamespaceUri: Namespaces.Html, LocalName: "select" })
                    element.GetHtmlState()!.GetSelectState(cancellationToken);
                if (original is { NamespaceUri: Namespaces.Html, LocalName: "input" })
                {
                    // Charge the cold state boundary independently of the preceding
                    // attribute copy; its metadata and sanitizer poll this same token.
                    var stateWork = new HtmlSelectWork(document.SelectWorkProbe, cancellationToken);
                    stateWork.Step();
                    element.GetHtmlState()!.GetInputValueState(cancellationToken)!
                        .CopyFrom(original.GetHtmlState()!.GetInputValueState(cancellationToken)!);
                }
                if (original is { NamespaceUri: Namespaces.Html, LocalName: "textarea" })
                {
                    element.GetHtmlState()!.TextArea!.CopyFrom(original.GetHtmlState()!.TextArea!);
                }
                if (original is { NamespaceUri: Namespaces.Html, LocalName: "script" })
                {
                    element.GetHtmlState()!.Script!.AlreadyStarted = original.GetHtmlState()!.Script!.AlreadyStarted;
                }
                var registry = original.CustomElementRegistry ?? fallbackRegistry;
                element.InitializeCustomElementRegistry(registry is { IsScoped: false }
                    ? EffectiveGlobalRegistry(document)
                    : registry);
                return element;
            case Text original:
                return new Text(document, original.Data);
            case Comment original:
                return new Comment(document, original.Data);
            case CDataSection original:
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

    private static CustomElementRegistryIdentity? EffectiveGlobalRegistry(Document document)
        => document.CustomElementRegistry is { IsScoped: false } registry ? registry : null;

    private struct Frame
    {
        internal Frame(Node source, Node copy, bool deep, CustomElementRegistryIdentity? fallbackRegistry)
        {
            Source = source;
            Copy = copy;
            Deep = deep;
            FallbackRegistry = fallbackRegistry;
            NextChild = deep ? source.FirstChild : null;
        }

        internal Node Source { get; }
        internal Node Copy { get; }
        internal bool Deep { get; }
        internal CustomElementRegistryIdentity? FallbackRegistry { get; }
        internal Node? NextChild { get; set; }
        internal int Stage { get; set; }
    }
}
