namespace Jint.HtmlParser;

internal enum ShadowRootMode { Open, Closed }
internal enum SlotAssignmentMode { Named, Manual }

internal readonly record struct ShadowRootInit(
    ShadowRootMode Mode,
    bool DelegatesFocus = false,
    bool Serializable = false,
    SlotAssignmentMode SlotAssignment = SlotAssignmentMode.Named,
    bool Clonable = false);

// An identity only. Browser owns definitions, reactions and identity-to-registry mapping.
internal sealed class CustomElementRegistryIdentity(bool isScoped)
{
    internal bool IsScoped { get; } = isScoped;
}

internal readonly record struct ShadowAttachmentContext(
    CustomElementRegistryIdentity? Registry,
    bool DisableShadow,
    bool HostIsCustomOrPrecustomized);

internal sealed class ShadowRoot : DocumentFragment
{
    internal SlotTreeState? SlotState;
    internal ShadowRoot(Element host, ShadowRootInit init, ShadowAttachmentContext context)
        : base(host.OwnerDocument!, host)
    {
        Host = host;
        Mode = init.Mode;
        DelegatesFocus = init.DelegatesFocus;
        Serializable = init.Serializable;
        SlotAssignment = init.SlotAssignment;
        Clonable = init.Clonable;
        AvailableToElementInternals = context.HostIsCustomOrPrecustomized;
        CustomElementRegistry = context.Registry;
    }

    internal new Element Host { get; }
    internal ShadowRootMode Mode { get; }
    internal bool DelegatesFocus { get; }
    internal bool Serializable { get; }
    internal SlotAssignmentMode SlotAssignment { get; }
    internal bool Clonable { get; }
    internal bool Declarative { get; private set; }
    internal bool AvailableToElementInternals { get; private set; }
    internal CustomElementRegistryIdentity? CustomElementRegistry { get; private set; }
    internal bool KeepCustomElementRegistryNull { get; private set; }

    internal void SetCustomElementRegistry(CustomElementRegistryIdentity? registry)
    {
        if (ReferenceEquals(CustomElementRegistry, registry)) return;
        CustomElementRegistry = registry;
        OwnerDocument!.MarkMutation();
    }
    internal void SetDeclarative(bool value) => Declarative = value;
    internal void SetKeepCustomElementRegistryNull(bool value) => KeepCustomElementRegistryNull = value;
    internal void SetAvailableToElementInternals(bool value) => AvailableToElementInternals = value;
}

// DOM Standard §4.2.2 and §4.9: shadow roots are hosted, not ordinary children.
internal static class ShadowTree
{
    internal static ShadowRoot Attach(Element host, ShadowRootInit init, ShadowAttachmentContext context)
        => Attach(host, init, context, null, default);

    internal static ShadowRoot Attach(Element host, ShadowRootInit init, ShadowAttachmentContext context,
        Action<int>? checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (init.Mode is not ShadowRootMode.Open and not ShadowRootMode.Closed)
        {
            throw new ArgumentOutOfRangeException(nameof(init));
        }

        if (init.SlotAssignment is not SlotAssignmentMode.Named and not SlotAssignmentMode.Manual)
        {
            throw new ArgumentOutOfRangeException(nameof(init));
        }

        var work = new ShadowAttachmentWork(checkpoint, token);
        work.Check();
        if (host.NamespaceUri != Namespaces.Html || !IsValidShadowHostName(host.LocalName, ref work) || context.DisableShadow)
        {
            throw DomException.NotSupported();
        }

        if (host.AttachedShadowRoot is { } existing)
        {
            if (!existing.Declarative || existing.Mode != init.Mode)
            {
                throw DomException.NotSupported();
            }

            while (existing.FirstChild is { } child)
            {
                work.Check();
                existing.RemoveChild(child);
                work.Step();
                work.Check();
            }

            work.Check();
            existing.SetDeclarative(false);
            host.OwnerDocument!.MarkMutation();
            work.Check();
            return existing;
        }

        var root = new ShadowRoot(host, init, context);
        work.Check();
        host.SetAttachedShadowRoot(root);
        host.OwnerDocument!.MarkMutation();
        work.Check();
        return root;
    }

    // The source tree already passed attachment validation. Cloning assembles an
    // unpublished tree, so it does not invalidate an unrelated import destination.
    internal static ShadowRoot AttachClone(Element host, ShadowRootInit init,
        CustomElementRegistryIdentity? registry)
    {
        if (host.AttachedShadowRoot is not null)
        {
            throw new InvalidOperationException("A clone cannot acquire a second shadow root.");
        }

        var root = new ShadowRoot(host, init, new ShadowAttachmentContext(registry, false, false));
        host.SetAttachedShadowRoot(root);
        return root;
    }

    internal static Node GetRoot(Node node, bool composed, CancellationToken cancellationToken)
        => GetRoot(node, composed, null, cancellationToken);

    // Per-invocation checkpoint for deterministic cancellation tests; no callback
    // is kept by a node or document.
    internal static Node GetRoot(Node node, bool composed, Action<int>? workCheckpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();
        var current = node;
        var steps = 0;
        while (true)
        {
            var parent = current.ParentNode ?? (composed && current is ShadowRoot root ? root.Host : null);
            if (parent is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return current;
            }

            current = parent;
            if ((++steps & 255) == 0)
            {
                workCheckpoint?.Invoke(steps);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    internal static bool IsConnected(Node node, CancellationToken cancellationToken)
        => GetRoot(node, composed: true, cancellationToken) is Document;

    // Trusted HTML tree-builder seam, before this template can be published.
    internal static void SetDeclarativeTemplateContent(Element template, ShadowRoot root,
        bool keepCustomElementRegistryNull)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(root);
        if (template.NamespaceUri != Namespaces.Html || template.LocalName != "template" ||
            template.ParentNode is not null || template.PreviousSibling is not null ||
            template.NextSibling is not null || template.ChildCount != 0 ||
            template.MutationRegistrations is not null ||
            template.TemplateContent is not { ChildCount: 0, ParentNode: null } original ||
            original is ShadowRoot || original.MutationRegistrations is not null ||
            !ReferenceEquals(original.Host, template) ||
            !ReferenceEquals(original.OwnerDocument, template.OwnerDocument!.GetTemplateContentsOwnerDocument()) ||
            !ReferenceEquals(root.Host.AttachedShadowRoot, root) || root.Declarative ||
            root.ChildCount != 0 || root.MutationRegistrations is not null ||
            !ReferenceEquals(template.OwnerDocument, root.OwnerDocument))
        {
            throw new InvalidOperationException("Declarative content requires a fresh, unpublished template and attached root.");
        }

        template.SetTemplateContent(root);
        root.SetDeclarative(true);
        root.SetAvailableToElementInternals(true);
        root.SetKeepCustomElementRegistryNull(keepCustomElementRegistryNull);
        template.OwnerDocument!.MarkMutation();
    }

    private static bool IsValidShadowHostName(string name, ref ShadowAttachmentWork work)
        => name is "article" or "aside" or "blockquote" or "body" or "div" or "footer" or
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "header" or "main" or
            "nav" or "p" or "section" or "span" || IsValidCustomElementName(name, ref work);

    // HTML §4.13.2, valid custom element name, using DOM's current valid local name.
    private static bool IsValidCustomElementName(string name, ref ShadowAttachmentWork work)
    {
        if (name.Length < 2 || name[0] is not (>= 'a' and <= 'z') ||
            name is "annotation-xml" or "color-profile" or "font-face" or "font-face-src" or
                "font-face-uri" or "font-face-format" or "font-face-name" or "missing-glyph")
        {
            return false;
        }

        var hasHyphen = false;
        for (var i = 0; i < name.Length; i++)
        {
            work.Step();
            var ch = name[i];
            hasHyphen |= ch == '-';
            if (ch is >= 'A' and <= 'Z' or '\0' or '\t' or '\n' or '\f' or '\r' or ' ' or '/' or '>')
            {
                return false;
            }
        }

        work.Check();
        return hasHyphen;
    }

    private struct ShadowAttachmentWork(Action<int>? checkpoint, CancellationToken token)
    {
        private int _units;
        internal void Step()
        {
            if ((++_units & 255) == 0) Check();
        }
        internal readonly void Check()
        {
            checkpoint?.Invoke(_units);
            token.ThrowIfCancellationRequested();
        }
    }
}
