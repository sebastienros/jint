using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Runtime.Parsing;

internal sealed partial class ParserDriver
{
    private readonly ConditionalWeakTable<Node, ResourceWatch> _resourceWatches = new();
    private readonly List<WeakReference<ResourceWatch>> _resourceWatchReferences = [];
    private readonly HashSet<Element> _pendingStyleCompletions = [];
    private readonly ConditionalWeakTable<Element, ResourceSource> _resourceSources = new();
    private readonly Queue<(ResourceWatch Watch, MutationRecord Record, bool ImageDelegated)> _resourceRecords = new();
    private readonly List<Element> _candidateShadowHosts = [];
    private readonly HashSet<Element> _inlineStyles = [];
    private readonly HashSet<Element> _changedScripts = [];
    private readonly List<WeakReference<MutationSubscription>> _scriptSubscriptions = [];
    private bool _scriptChangesPosted;
    private int _scriptAttachmentsUntilSweep = 64;
    private int _resourceAttachmentsUntilSweep = 64;

    private sealed class ResourceWatch(Node root, Document document, MutationSubscription subscription)
    {
        internal Node Root { get; } = root;
        internal Document Document { get; } = document;
        internal MutationSubscription Subscription { get; } = subscription;
        internal bool Active = true;
        internal bool Posted;
        internal bool Deferred;
    }

    private sealed class ResourceSource
    {
        internal string? Signature;
        internal object? StyleRequest;
        internal Document? StyleRequestDocument;
        internal Node? StyleRequestRoot;
        internal bool StyleLoaded;
        internal bool ModuleStarted;
        internal MutationSubscription? ScriptSubscription;
    }

    private ResourceWatch WatchDocument(Document document) => WatchResourceRoot(document);

    private ResourceWatch WatchResourceRoot(Node root)
    {
        var document = root as Document ?? root.OwnerDocument!;
        if (_resourceWatches.TryGetValue(root, out var existing))
        {
            if (ReferenceEquals(existing.Document, document)) return existing;
            RetireResourceWatch(existing);
        }
        var imageSource = root is Element { NamespaceUri: Namespaces.Html, LocalName: "img" or "input" };
        var subscription = document.ObserveMutations(root, new MutationObserverOptions
        {
            ChildList = !imageSource,
            CharacterData = !imageSource,
            Attributes = true,
            Subtree = !imageSource,
            AttributeFilter = imageSource ? ["src", "srcset", "sizes", "type"]
                : ["src", "srcdoc", "srcset", "sizes", "href", "rel", "type", "media"]
        });
        var watch = new ResourceWatch(root, document, subscription);
        var weak = new WeakReference<ParserDriver>(this);
        subscription.PendingRecord = pending =>
        {
            // Freeze cross-root order at arrival. No tree reads, fetch, CSS or script here.
            if (!weak.TryGetTarget(out var driver)) { watch.Subscription.Dispose(); return; }
            if (driver._disposed || !watch.Active) return;
            foreach (var record in pending.TakeRecords()) driver.CaptureResourceRecord(watch, record);
            driver.QueueResourceDrain(watch);
        };
        _resourceWatches.Add(root, watch);
        _resourceWatchReferences.Add(new(watch));
        if (--_resourceAttachmentsUntilSweep == 0) CompactResourceWatches();
        return watch;
    }

    private void CaptureResourceRecord(ResourceWatch watch, MutationRecord record)
    {
        // Freeze delegation on arrival: a later mutator can install an image watch before
        // delivery, but that new subscription did not observe this earlier document record.
        var delegated = watch.Root is not Element && record.Kind == MutationRecordKind.Attributes &&
            record.Target is Element element && _resourceWatches.TryGetValue(element, out var imageWatch) &&
            imageWatch.Active && ReferenceEquals(imageWatch.Document, watch.Document);
        _resourceRecords.Enqueue((watch, record, delegated));
    }

    private void RetireResourceWatch(ResourceWatch watch)
    {
        watch.Active = false;
        watch.Subscription.Dispose();
        if (_resourceWatches.TryGetValue(watch.Root, out var current) && ReferenceEquals(current, watch))
            _resourceWatches.Remove(watch.Root);
    }

    private void RetireDocumentWatches(Document document) => CompactResourceWatches(document);

    private void CompactResourceWatches(Document? retiringDocument = null)
    {
        var consumed = 0;
        var survivors = 0;
        try
        {
            for (; consumed < _resourceWatchReferences.Count; consumed++)
            {
                _runtime.Engine.Constraints.Check();
                var reference = _resourceWatchReferences[consumed];
                if (!reference.TryGetTarget(out var watch) || !watch.Active) continue;
                if (retiringDocument is not null && ReferenceEquals(watch.Document, retiringDocument))
                {
                    RetireResourceWatch(watch);
                    continue;
                }
                _resourceWatchReferences[survivors++] = reference;
            }
        }
        finally
        {
            // Preserve the untouched suffix when a checkpoint throws during this forward pass.
            _resourceWatchReferences.RemoveRange(survivors, consumed - survivors);
            _resourceAttachmentsUntilSweep = Math.Max(64, _resourceWatchReferences.Count);
        }
    }

    internal void WatchShadowRoot(ShadowRoot root)
    {
        if (_disposed) return;
        var known = _resourceWatches.TryGetValue(root, out var watch) && ReferenceEquals(watch.Document, root.OwnerDocument);
        WatchResourceRoot(root);
        if (!known) ProcessResourceSubtree(root, new HashSet<Node>(ReferenceEqualityComparer.Instance));
    }

    internal void EnsureWatchingNode(Node node)
    {
        if (_disposed) return;
        // HTML's image data updates also apply to detached images in an active document.
        // Install before the write so Attr/NamedNodeMap and reflected setters share one lane.
        if (node is Element { NamespaceUri: Namespaces.Html, LocalName: "img" or "input" } image &&
            PageRuntime.FindBrowsingContext(_runtime.Engine, image.OwnerDocument!) is not null)
            WatchResourceRoot(image);
        var root = ShadowTree.GetRoot(node, composed: false, _runtime.Dom.NativeReadCheckpoint, _cancellationToken);
        if (root is ShadowRoot shadow && IsResourceConnected(shadow.Host)) WatchShadowRoot(shadow);
    }

    private void RecordCandidateShadowHost(Element host) => _candidateShadowHosts.Add(host);

    private void DiscoverCandidateShadowRoots()
    {
        foreach (var host in _candidateShadowHosts)
        {
            _runtime.Engine.Constraints.Check();
            if (host.AttachedShadowRoot is { } root) WatchShadowRoot(root);
        }
        _candidateShadowHosts.Clear();
    }

    private void QueueResourceDrain(ResourceWatch watch)
    {
        if (watch.Posted) return;
        watch.Posted = true;
        _runtime.Engine.Tasks.Post(() =>
        {
            watch.Posted = false;
            if (_disposed || !watch.Active) return;
            if (_nativeParses.ContainsKey(watch.Document)) { watch.Deferred = true; return; }
            DrainResourceRecords();
            InstallInlineStyles(watch.Document);
        });
    }

    internal void CompleteNativeMutation(Node node)
    {
        var document = node as Document ?? node.OwnerDocument;
        if (!_disposed && document is not null)
        {
            EnsureWatchingNode(node);
            DrainResourceRecords();
            InstallInlineStyles(document);
            DrainScriptChanges(document);
        }
    }

    private void ProcessResourceRecords(NativeParse parse)
    {
        WatchDocument(parse.Document);
        DrainResourceRecords();
    }

    private void DrainResourceRecords()
    {
        // The arrival queue preserves order across ordinary roots and nested page turns.
        var seen = new HashSet<Node>(ReferenceEqualityComparer.Instance);
        var delivered = new HashSet<ResourceWatch>(ReferenceEqualityComparer.Instance);
        while (_resourceRecords.TryDequeue(out var entry))
        {
            var record = entry.Record;
            _runtime.Engine.Constraints.Check();
            if (entry.Watch.Active && delivered.Add(entry.Watch))
            {
                // Capture happens on arrival; transient observation ends at this safe delivery boundary.
                foreach (var remaining in entry.Watch.Subscription.TakeRecordsForDelivery())
                    CaptureResourceRecord(entry.Watch, remaining);
            }
            if (!entry.Watch.Active)
            {
                if (record.Kind == MutationRecordKind.ChildList && record.TargetWasConnected)
                    foreach (var removed in record.RemovedNodes) DisassociateResourceSubtree(removed, entry.Watch.Document, seen);
                continue;
            }
            if (record.Kind == MutationRecordKind.ChildList)
            {
                if (!record.TargetWasConnected) continue;
                if (record.Target is Element { NamespaceUri: Namespaces.Html, LocalName: "script" } script)
                    ProcessResourceElement(script);
                foreach (var removed in record.RemovedNodes) DisassociateResourceSubtree(removed, entry.Watch.Document, seen);
                foreach (var added in record.AddedNodes) ProcessResourceSubtree(added, seen);
                if (record.Target is Element { LocalName: "style", NamespaceUri: Namespaces.Html or Namespaces.Svg } style)
                {
                    NativeCssStyleSheets.DisassociateOwner(_runtime.Dom.RealmOfDocument(entry.Watch.Document), entry.Watch.Document, style);
                    ProcessResourceElement(style);
                }
            }
            else if (record.Kind == MutationRecordKind.CharacterData)
            {
                for (var parent = record.Target.ParentNode; parent is not null; parent = parent.ParentNode)
                {
                    _runtime.Engine.Constraints.Check();
                    if (parent is Element { LocalName: "style", NamespaceUri: Namespaces.Html or Namespaces.Svg } style)
                    {
                        NativeCssStyleSheets.DisassociateOwner(_runtime.Dom.RealmOfDocument(entry.Watch.Document), entry.Watch.Document, style);
                        ProcessResourceElement(style);
                        break;
                    }
                }
            }
            else if (record.Kind == MutationRecordKind.Attributes && record.Target is Element element &&
                record.AttributeNamespace is null)
            {
                if (entry.Watch.Root is Element && !ReferenceEquals(element.OwnerDocument, entry.Watch.Document)) continue;
                // The element-owned image subscription captures this write as well. Its arrival is
                // guaranteed before the native call returns; process it once, including failed requests.
                if (entry.ImageDelegated) continue;
                if (element is { NamespaceUri: Namespaces.Html, LocalName: "script" }
                    && (record.AttributeName != "src" || Attribute(element, "src") is null)) continue;
                if (element is { LocalName: "style", NamespaceUri: Namespaces.Html or Namespaces.Svg })
                {
                    if (record.AttributeName == "type")
                    {
                        NativeCssStyleSheets.DisassociateOwner(_runtime.Dom.RealmOfDocument(entry.Watch.Document), entry.Watch.Document, element);
                        ProcessResourceElement(element);
                    }
                    // Media metadata is read by the producer; it must not replace authored CSSOM rules.
                }
                else ProcessResourceElement(element);
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
            if (node is Element element)
            {
                ProcessResourceElement(element);
                if (element.AttachedShadowRoot is { } shadow)
                {
                    WatchResourceRoot(shadow);
                    pending.Push(shadow);
                }
            }
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                if ((++steps & 255) == 0) _runtime.Engine.Constraints.Check();
                pending.Push(child);
            }
        }
        _runtime.Engine.Constraints.Check();
    }

    private void DisassociateResourceSubtree(Node root, Document document, HashSet<Node> seen)
    {
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            _runtime.Engine.Constraints.Check();
            seen.Remove(node);
            if (node is ShadowRoot shadowRoot && _resourceWatches.TryGetValue(node, out var watch) &&
                ReferenceEquals(watch.Document, document) &&
                !(ReferenceEquals(shadowRoot.OwnerDocument, document) &&
                  ReferenceEquals(shadowRoot.Host.AttachedShadowRoot, shadowRoot) && IsResourceConnected(shadowRoot.Host)))
                RetireResourceWatch(watch);
            if (node is Element element)
            {
                if (element.LocalName == "style" && element.NamespaceUri is Namespaces.Html or Namespaces.Svg ||
                    element is { LocalName: "link", NamespaceUri: Namespaces.Html })
                {
                    var preserve = element.LocalName == "link" &&
                        _resourceSources.TryGetValue(element, out var current) && current.StyleLoaded &&
                        ReferenceEquals(current.StyleRequestDocument, document) && current.StyleRequest is { } request &&
                        CurrentStyleSheetSource(element, document, current, request, current.Signature);
                    // A move completed before delivery can retain its sheet and edited CSSOM rules.
                    // A separately delivered detached state, changed root, URL or owner still invalidates.
                    if (!preserve)
                    {
                        NativeCssStyleSheets.DisassociateOwner(_runtime.Dom.RealmOfDocument(document), document, element);
                        if (_resourceSources.TryGetValue(element, out var source) &&
                            ReferenceEquals(source.StyleRequestDocument, document)) InvalidateStyleRequest(source);
                    }
                }
                if (element.AttachedShadowRoot is { } shadow) pending.Push(shadow);
            }
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                _runtime.Engine.Constraints.Check();
                pending.Push(child);
            }
        }
    }

    private void ProcessResourceElement(Element element)
    {
        if (element.LocalName == "style" && element.NamespaceUri is Namespaces.Html or Namespaces.Svg)
        {
            NativeCssStyleSheets.PrepareOwner(_runtime.Dom.RealmOfDocument(element.OwnerDocument!), element);
            _inlineStyles.Add(element);
            InstallInlineStyle(element);
            return;
        }
        if (element.NamespaceUri != Namespaces.Html) return;
        switch (element.LocalName)
        {
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

    private bool StyleIsPending(Element style) => _pendingStyleCompletions.Contains(style) ||
        style.OwnerDocument is { } document && _nativeParses.TryGetValue(document, out var parse) && parse.Session.IsStyleOpen(style);

    private void InstallInlineStyle(Element style)
    {
        if (StyleIsPending(style)) return;
        _inlineStyles.Remove(style);
        if (style.OwnerDocument is { } document && IsResourceConnected(style))
        {
            var work = new CssValueWork(_cancellationToken, _runtime.Engine.Constraints.Check);
            if (NativeCssStyleSheets.EligibleOwner(style, new DomReadWork(work.Charge, work.Token), work))
                NativeCssStyleSheets.Install(_runtime.Dom.RealmOfDocument(document), style,
                    TextOf(style), DomDocumentState.Of(document).Url);
        }
    }

    private void InstallInlineStyles(Document document)
    {
        foreach (var style in _inlineStyles.ToArray())
        {
            if (ReferenceEquals(style.OwnerDocument, document)) InstallInlineStyle(style);
        }
    }

    private void LoadStyleSheet(Element link)
    {
        var document = link.OwnerDocument!;
        var realm = _runtime.Dom.RealmOfDocument(document);
        NativeCssStyleSheets.PrepareOwner(realm, link);
        var source = _resourceSources.GetValue(link, static _ => new ResourceSource());
        void Disassociate()
        {
            NativeCssStyleSheets.DisassociateOwner(realm, document, link);
            InvalidateStyleRequest(source);
        }
        if (!IsResourceConnected(link)) { Disassociate(); return; }
        var href = Attribute(link, "href");
        if (string.IsNullOrEmpty(href)) { Disassociate(); return; }
        var relations = Attribute(link, "rel") ?? "";
        var work = new CssValueWork(_cancellationToken, _runtime.Engine.Constraints.Check);
        if (!NativeCssStyleSheets.EligibleOwner(link, new DomReadWork(work.Charge, work.Token), work))
        {
            Disassociate();
            var refused = PageUrl.Resolve(href, BaseUrlOf(link.OwnerDocument!)) ?? href;
            _requests.RecordNotFetched(refused, RequestInitiator.Subresource, PageRequestKind.Other,
                "a <link rel=\"" + relations + "\"> is not fetched: only a stylesheet is");
            return;
        }
        var url = PageUrl.Resolve(href, BaseUrlOf(document));
        var root = ShadowTree.GetRoot(link, composed: false, _runtime.Dom.NativeReadCheckpoint, _cancellationToken);
        if (source.Signature == url && source.StyleRequest is not null &&
            ReferenceEquals(source.StyleRequestDocument, document) && ReferenceEquals(source.StyleRequestRoot, root)) return;
        // A changed request removes the old sheet before the new fetch can pump a page turn.
        NativeCssStyleSheets.DisassociateOwner(realm, document, link);
        var request = new object();
        source.StyleRequest = request;
        source.StyleRequestDocument = document;
        source.StyleRequestRoot = root;
        source.StyleLoaded = false;
        source.Signature = url;
        bool Current() => CurrentStyleSheetSource(link, document, source, request, url);
        if (url is null)
        {
            FailSubresource(link, href, "The stylesheet URL is invalid.", Current);
            return;
        }
        void Failed(string message)
        {
            if (!Current()) return;
            // A failed current request can be retried. Keep its identity until a replacement
            // starts so its queued error still belongs to this request.
            source.Signature = null;
            FailSubresource(link, url, message, Current);
        }
        var fetched = FetchBytes(url, link, "stylesheet", PageRequestKind.Stylesheet,
            mayPump: !_runtime.Engine.IsEvaluationInProgress, onFailure: Failed);
        if (fetched is not { } body || !Current())
        {
            if (ReferenceEquals(source.StyleRequest, request)) source.Signature = null;
            return;
        }
        var text = new FetchedSubresource(body.Bytes, body.ContentType, body.Url, null, 200)
            .Text(DomDocumentState.Of(document).CharacterSet);
        NativeCssStyleSheets.Install(_runtime.Dom.RealmOfDocument(document), link, text, body.Url);
        if (Current())
        {
            source.StyleLoaded = true;
            QueueResourceEvent(link, "load", afterParse: false, Current);
        }
    }

    private static void InvalidateStyleRequest(ResourceSource source)
    {
        source.StyleRequest = null;
        source.StyleRequestDocument = null;
        source.StyleRequestRoot = null;
        source.StyleLoaded = false;
        source.Signature = null;
    }

    private bool CurrentStyleSheetSource(Element link, Document document, ResourceSource source, object request, string? url)
    {
        var work = new CssValueWork(_cancellationToken, _runtime.Engine.Constraints.Check);
        var reads = new DomReadWork(work.Charge, _cancellationToken);
        while (true)
        {
            var stamp = document.MutationStamp;
            if (stamp == ulong.MaxValue) throw new InvalidOperationException(NativeCssQuery.Invalidated);
            if (!ReferenceEquals(source.StyleRequest, request) || !ReferenceEquals(link.OwnerDocument, document)) return false;
            var connected = IsResourceConnected(link);
            var root = ShadowTree.GetRoot(link, composed: false, _runtime.Dom.NativeReadCheckpoint, _cancellationToken);
            var eligible = NativeCssStyleSheets.EligibleOwner(link, reads, work);
            var currentUrl = PageUrl.Resolve(reads.Attribute(link, "href") ?? "", BaseUrlOf(document));
            work.CheckCancellation();
            if (stamp != document.MutationStamp) continue;
            return ReferenceEquals(link.OwnerDocument, document) && connected && eligible &&
                ReferenceEquals(source.StyleRequest, request) && ReferenceEquals(source.StyleRequestRoot, root) && currentUrl == url;
        }
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
        var sandboxedOrigin = DomDocumentState.Of(owner).HasSandboxedOrigin || HasSandboxedOrigin(frame);
        var scriptsBlockedBySandbox = DomDocumentState.Of(owner).ScriptsBlockedBySandbox
            || Attribute(frame, "sandbox") is not null;
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
        string? defaultStyle = null;
        if (srcdoc is not null || DomDocumentOrigin.MatchesAboutBlank(url))
        {
            markup = srcdoc ?? "";
            contentType = DomContentType.Html;
        }
        else
        {
            if (FetchBytes(url, frame, "frame document", PageRequestKind.Frame,
                    mayPump: !_runtime.Engine.IsEvaluationInProgress) is not { } body) return;
            lastModified = body.LastModified;
            defaultStyle = body.DefaultStyle;
            (markup, contentType) = DocumentFetch.Decode(body.Bytes, body.ContentType, body.Url);
            url = body.Url;
        }
        var document = new Document(DomContentType.IsXml(contentType) ? DocumentKind.Xml : DocumentKind.Html,
            contentType, new CustomElementRegistryIdentity(isScoped: false));
        DomDocumentMetadata.Initialize(document, sandboxedOrigin ? DomDocumentOrigin.Opaque()
            : DomDocumentOrigin.InheritsCreator(url) ? creatorOrigin : DomDocumentOrigin.FromUrl(url), lastModified);
        ApplyDefaultStyle(document, defaultStyle);
        var metadata = DomDocumentState.Of(document);
        metadata.Url = url;
        metadata.Referrer = creatorUrl;
        metadata.ReadyState = "loading";
        metadata.HasSandboxedOrigin = sandboxedOrigin;
        metadata.ScriptsBlockedBySandbox = scriptsBlockedBySandbox;
        if (DomDocumentOrigin.InheritsCreator(url)) metadata.AboutBaseUrl = creatorBaseUrl;
        if (DomBrowsingContext.OfFrame(frame) is { } context)
        {
            if (context.Active is { } previous) RetireDocumentWatches(previous);
            context.Activate(document);
        }
        else context = new DomBrowsingContext(document, creatorContext, frame);
        var dom = FrameWindows.DocumentRealm(_runtime, document);
        dom.AssociateContext(context);
        Parse(document, markup, isSrcdoc: srcdoc is not null);
        // HTML's fragment navigation selects an indicated element from this completed
        // child document, just as FinishLoad does for the principal document.
        DomDocumentState.SelectNavigationTarget(dom, document);
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
