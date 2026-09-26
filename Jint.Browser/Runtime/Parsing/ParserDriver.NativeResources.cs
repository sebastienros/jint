using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Browser.Runtime.Parsing;

internal sealed partial class ParserDriver
{
    private readonly Dictionary<Document, ResourceWatch> _resourceWatches = new();
    private readonly ConditionalWeakTable<Element, ResourceSource> _resourceSources = new();
    private readonly HashSet<Element> _inlineStyles = [];
    private readonly HashSet<Element> _changedScripts = [];
    private readonly List<WeakReference<MutationSubscription>> _scriptSubscriptions = [];
    private bool _scriptChangesPosted;
    private int _scriptAttachmentsUntilSweep = 64;

    private sealed class ResourceWatch(Document document, MutationSubscription subscription)
    {
        internal Document Document { get; } = document;
        internal MutationSubscription Subscription { get; } = subscription;
        internal bool Posted;
        internal bool Deferred;
    }

    private sealed class ResourceSource
    {
        internal string? Signature;
        internal bool ModuleStarted;
        internal MutationSubscription? ScriptSubscription;
    }

    private ResourceWatch WatchDocument(Document document)
    {
        if (_resourceWatches.TryGetValue(document, out var existing)) return existing;
        var subscription = document.ObserveMutations(document, new MutationObserverOptions
        {
            ChildList = true,
            Attributes = true,
            Subtree = true,
            AttributeFilter = ["src", "srcdoc", "srcset", "sizes", "href", "rel", "type", "media"]
        });
        var watch = new ResourceWatch(document, subscription);
        var weak = new WeakReference<ParserDriver>(this);
        subscription.PendingRecord = _ =>
        {
            // Native mutation only signals work. No script, fetch, tree read or style calculation.
            if (!weak.TryGetTarget(out var driver)) { watch.Subscription.Dispose(); return; }
            if (!driver._disposed) driver.QueueResourceDrain(watch);
        };
        _resourceWatches.Add(document, watch);
        return watch;
    }

    private void QueueResourceDrain(ResourceWatch watch)
    {
        if (watch.Posted) return;
        watch.Posted = true;
        _runtime.Engine.Tasks.Post(() =>
        {
            watch.Posted = false;
            if (_disposed) return;
            if (_nativeParses.ContainsKey(watch.Document)) { watch.Deferred = true; return; }
            DrainResourceRecords(watch);
            InstallInlineStyles(watch.Document);
        });
    }

    internal void CompleteNativeMutation(Node node)
    {
        var document = node as Document ?? node.OwnerDocument;
        if (!_disposed && document is not null && _resourceWatches.TryGetValue(document, out var watch))
        {
            DrainResourceRecords(watch);
            InstallInlineStyles(document);
            DrainScriptChanges(document);
        }
    }

    private void ProcessResourceRecords(NativeParse parse) => DrainResourceRecords(WatchDocument(parse.Document));

    private void DrainResourceRecords(ResourceWatch watch)
    {
        // TakeRecords detaches this batch. A nested page script may drain a new batch
        // synchronously, including one belonging to a recursively parsed child document.
        var seen = new HashSet<Node>(ReferenceEqualityComparer.Instance);
        foreach (var record in watch.Subscription.TakeRecordsForDelivery())
        {
            _runtime.Engine.Constraints.Check();
            if (record.Kind == MutationRecordKind.ChildList)
            {
                if (!record.TargetWasConnected) continue;
                if (record.Target is Element { NamespaceUri: Namespaces.Html, LocalName: "script" } script)
                    ProcessResourceElement(script);
                foreach (var added in record.AddedNodes) ProcessResourceSubtree(added, seen);
            }
            else if (record.Kind == MutationRecordKind.Attributes && record.Target is Element element &&
                record.AttributeNamespace is null)
            {
                if (element is { NamespaceUri: Namespaces.Html, LocalName: "script" }
                    && (record.AttributeName != "src" || Attribute(element, "src") is null)) continue;
                ProcessResourceElement(element);
            }
        }
    }

    private void ProcessResources(Document document)
    {
        ProcessResourceSubtree(document, new HashSet<Node>(ReferenceEqualityComparer.Instance));
        InstallInlineStyles(document);
    }

    private void ProcessResourceSubtree(Node root, HashSet<Node> seen)
    {
        var pending = new Stack<Node>();
        pending.Push(root);
        var steps = 0;
        while (pending.TryPop(out var node))
        {
            if ((++steps & 255) == 0) _runtime.Engine.Constraints.Check();
            // Added-node records overlap within one parser slice. Visit each real node once.
            if (!seen.Add(node)) continue;
            if (node is Element element) ProcessResourceElement(element);
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                if ((++steps & 255) == 0) _runtime.Engine.Constraints.Check();
                pending.Push(child);
            }
        }
        _runtime.Engine.Constraints.Check();
    }

    private void ProcessResourceElement(Element element)
    {
        if (element.NamespaceUri != Namespaces.Html) return;
        switch (element.LocalName)
        {
            case "style":
                _inlineStyles.Add(element);
                return;
            case "link":
                LoadStyleSheet(element);
                return;
            case "iframe":
                LoadFrame(element);
                return;
            case "img":
                FetchImage(element, Attribute(element, "src") ?? "");
                return;
            case "input" when HtmlInputTypes.Parse(Attribute(element, "type")) == HtmlInputType.Image:
                FetchImage(element, Attribute(element, "src") ?? "");
                return;
            case "script":
                // The HTML tokenizer's own request prepares parser-inserted scripts.
                if (_xmlParsingDocuments.Contains(element.OwnerDocument!))
                    element.GetHtmlState()!.Script!.AlreadyStarted = true;
                else if (!element.GetHtmlState()!.Script!.ParserInserted)
                {
                    ObserveUnstartedScript(element);
                    PrepareDynamicScript(element);
                }
                return;
        }
    }

    private bool IsResourceConnected(Element element)
        => ShadowTree.GetRoot(element, composed: true, _runtime.Dom.NativeReadCheckpoint, _cancellationToken) is Document;

    private void InstallInlineStyles(Document document)
    {
        foreach (var style in _inlineStyles.ToArray())
        {
            if (!ReferenceEquals(style.OwnerDocument, document)) continue;
            _inlineStyles.Remove(style);
            if (IsResourceConnected(style))
                global::Jint.Browser.Styling.NativeCssStyleSheets.Install(_runtime.Dom.RealmOfDocument(document), style,
                    TextOf(style), DomDocumentState.Of(document).Url);
        }
    }

    private void LoadStyleSheet(Element link)
    {
        if (!IsResourceConnected(link)) return;
        var href = Attribute(link, "href");
        if (string.IsNullOrEmpty(href)) return;
        var relations = Attribute(link, "rel") ?? "";
        if (!relations.Split([' ', '\t', '\r', '\n', '\f'], StringSplitOptions.RemoveEmptyEntries)
            .Any(value => value.Equals("stylesheet", StringComparison.OrdinalIgnoreCase)))
        {
            var refused = PageUrl.Resolve(href, BaseUrlOf(link.OwnerDocument!)) ?? href;
            _requests.RecordNotFetched(refused, RequestInitiator.Subresource, PageRequestKind.Other,
                "a <link rel=\"" + relations + "\"> is not fetched: only a stylesheet is");
            return;
        }
        var url = PageUrl.Resolve(href, BaseUrlOf(link.OwnerDocument!));
        var source = _resourceSources.GetValue(link, static _ => new ResourceSource());
        if (source.Signature == url) return;
        source.Signature = url;
        if (url is null) { FailSubresource(link, href, "The stylesheet URL is invalid."); return; }
        var fetched = FetchBytes(url, link, "stylesheet", PageRequestKind.Stylesheet,
            mayPump: !_runtime.Engine.IsEvaluationInProgress);
        if (fetched is not { } body) return;
        var text = new FetchedSubresource(body.Bytes, body.ContentType, body.Url, null, 200)
            .Text(DomDocumentState.Of(link.OwnerDocument!).CharacterSet);
        global::Jint.Browser.Styling.NativeCssStyleSheets.Install(_runtime.Dom.RealmOfDocument(link.OwnerDocument!), link, text, body.Url);
        StyleSheetProcessed(link);
    }

    private void LoadFrame(Element frame)
    {
        if (!IsResourceConnected(frame)) return;
        // Freeze origin and sandbox facts before fetching can pump a later page turn.
        var owner = frame.OwnerDocument!;
        var creatorOrigin = DomDocumentState.Of(owner).Origin;
        var creatorUrl = DomDocumentState.Of(owner).Url;
        var creatorBaseUrl = BaseUrlOf(owner);
        var creatorContext = DomBrowsingContext.Of(owner);
        var sandboxedOrigin = HasSandboxedOrigin(frame);
        var srcdoc = Attribute(frame, "srcdoc");
        var src = Attribute(frame, "src");
        var url = srcdoc is not null ? "about:srcdoc"
            : string.IsNullOrEmpty(src) ? "about:blank" : PageUrl.Resolve(src, creatorBaseUrl);
        var signature = srcdoc is not null ? "srcdoc:" + srcdoc : "src:" + url;
        var source = _resourceSources.GetValue(frame, static _ => new ResourceSource());
        if (source.Signature == signature) return;
        source.Signature = signature;
        if (url is null) { FailSubresource(frame, src ?? "", "The frame URL is invalid."); return; }
        var ceiling = _runtime.Options.MaxFrameDocuments;
        if (ceiling <= 0 || _frameDocuments >= ceiling)
        {
            _requests.RecordNotFetched(url, RequestInitiator.Subresource, PageRequestKind.Frame,
                "The frame document limit has been reached.");
            return;
        }
        _frameDocuments++;
        string markup;
        string contentType;
        DateTimeOffset? lastModified = null;
        if (srcdoc is not null || url == "about:blank")
        {
            markup = srcdoc ?? "";
            contentType = DomContentType.Html;
        }
        else
        {
            if (FetchBytes(url, frame, "frame document", PageRequestKind.Frame,
                    mayPump: !_runtime.Engine.IsEvaluationInProgress) is not { } body) return;
            lastModified = body.LastModified;
            (markup, contentType) = DocumentFetch.Decode(body.Bytes, body.ContentType, body.Url);
            url = body.Url;
        }
        var document = new Document(DomContentType.IsXml(contentType) ? DocumentKind.Xml : DocumentKind.Html,
            contentType, new CustomElementRegistryIdentity(isScoped: false));
        DomDocumentMetadata.Initialize(document, sandboxedOrigin ? DomDocumentOrigin.Opaque()
            : url is "about:blank" or "about:srcdoc" ? creatorOrigin : DomDocumentOrigin.FromUrl(url), lastModified);
        var metadata = DomDocumentState.Of(document);
        metadata.Url = url;
        metadata.Referrer = creatorUrl;
        metadata.ReadyState = "loading";
        if (url is "about:blank" or "about:srcdoc") metadata.AboutBaseUrl = creatorBaseUrl;
        if (DomBrowsingContext.OfFrame(frame) is { } context)
        {
            if (context.Active is { } previous && _resourceWatches.Remove(previous, out var watch)) watch.Subscription.Dispose();
            context.Activate(document);
        }
        else context = new DomBrowsingContext(document, creatorContext, frame);
        var dom = FrameWindows.DocumentRealm(_runtime, document);
        dom.AssociateContext(context);
        Parse(document, markup, isSrcdoc: srcdoc is not null);
        dom.RecordSubtree(document);
        QueueResourceEvent(frame, "load", afterParse: true);
    }

    // https://html.spec.whatwg.org/multipage/origin.html#sandboxed-origin-browsing-context-flag
    private bool HasSandboxedOrigin(Element frame)
    {
        var sandbox = Attribute(frame, "sandbox");
        if (sandbox is null) return false;
        var start = 0;
        for (var i = 0; i <= sandbox.Length; i++)
        {
            if ((i & 4095) == 0) _runtime.Engine.Constraints.Check();
            if (i != sandbox.Length && sandbox[i] is not (' ' or '\t' or '\n' or '\r' or '\f')) continue;
            if (sandbox.AsSpan(start, i - start).Equals("allow-same-origin", StringComparison.OrdinalIgnoreCase)) return false;
            start = i + 1;
        }
        return true;
    }

    private void ObserveUnstartedScript(Element script)
    {
        if (script.GetHtmlState()!.Script!.AlreadyStarted) return;
        var state = _resourceSources.GetValue(script, static _ => new ResourceSource());
        if (state.ScriptSubscription is not null) return;
        PruneScriptSubscriptions();
        var subscription = script.OwnerDocument!.ObserveMutations(script,
            new MutationObserverOptions { CharacterData = true, Subtree = true });
        state.ScriptSubscription = subscription;
        _scriptSubscriptions.Add(new WeakReference<MutationSubscription>(subscription));
        var owner = new WeakReference<ParserDriver>(this);
        var target = new WeakReference<Element>(script);
        subscription.PendingRecord = _ =>
        {
            if (!owner.TryGetTarget(out var driver) || !target.TryGetTarget(out var element))
            {
                subscription.Dispose();
                return;
            }
            if (driver._disposed) return;
            // Metadata only: preparation and DOM reads happen after the native mutation returns.
            driver._changedScripts.Add(element);
            if (driver._scriptChangesPosted) return;
            driver._scriptChangesPosted = true;
            driver._resourceTasks.Post(() =>
            {
                driver._scriptChangesPosted = false;
                if (!driver._disposed) driver.DrainScriptChanges(null);
            });
        };
    }

    private void PruneScriptSubscriptions()
    {
        if (--_scriptAttachmentsUntilSweep > 0) return;
        var survivors = 0;
        var consumed = 0;
        try
        {
            for (; consumed < _scriptSubscriptions.Count; consumed++)
            {
                if ((consumed & 255) == 0) _runtime.Engine.Constraints.Check();
                var weak = _scriptSubscriptions[consumed];
                if (weak.TryGetTarget(out _)) _scriptSubscriptions[survivors++] = weak;
            }
        }
        finally
        {
            _scriptSubscriptions.RemoveRange(survivors, consumed - survivors);
            _scriptAttachmentsUntilSweep = Math.Max(64, _scriptSubscriptions.Count);
        }
    }

    private void DrainScriptChanges(Document? document)
    {
        foreach (var script in _changedScripts.ToArray())
        {
            _runtime.Engine.Constraints.Check();
            if (document is not null && !ReferenceEquals(script.OwnerDocument, document)) continue;
            _changedScripts.Remove(script);
            if (_resourceSources.TryGetValue(script, out var state)) state.ScriptSubscription?.TakeRecordsForDelivery();
            PrepareDynamicScript(script);
        }
    }

    private void PrepareDynamicScript(Element script)
    {
        var flags = script.GetHtmlState()!.Script!;
        if (flags.AlreadyStarted || !IsResourceConnected(script)) return;
        var type = ScriptType(script);
        var src = Attribute(script, "src");
        if (type == NativeScriptType.Data || src is null && ScriptTextOf(script).Length == 0) return;
        flags.AlreadyStarted = true;
        if (_resourceSources.TryGetValue(script, out var state) && state.ScriptSubscription is { } subscription)
        {
            subscription.Dispose();
            state.ScriptSubscription = null;
        }
        flags.PreparationTimeDocument = script.OwnerDocument;
        if (!_runtime.ScriptingEnabled || IsFrameDocument(script.OwnerDocument!) && !CanRunFrame(script.OwnerDocument!)) return;
        if (type == NativeScriptType.ImportMap)
        {
            if (!IsFrameDocument(script.OwnerDocument!)) ReadImportMapEarly(script.OwnerDocument!);
            return;
        }
        if (type == NativeScriptType.Module)
        {
            // The existing child-frame capability is classic scripting; the engine's module
            // map is shared and cannot evaluate a child module in the principal realm.
            if (IsFrameDocument(script.OwnerDocument!)) return;
            if (_runtime.Modules is { } modules) RunModule(modules, script);
            return;
        }
        if (Attribute(script, "nomodule") is not null) return;
        if (src is null) RunNativeClassic(script, null);
        else if (src.Length == 0) FireAt(script, "error");
        else if (FetchScriptSource(script, mayPump: false) is { } body) RunNativeClassic(script, body);
    }
}
