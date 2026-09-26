using System.Net.Http;
using Acornima;
using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.Fetch;
using Jint.Runtime.Modules;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>One native parse, its script handoffs, subresource loads and load lifecycle.</summary>
/// <remarks>
/// HTML §13.2: tokenizer and script run on the page loop. Cooperative parser quotas check
/// the page budget without pumping unrelated tasks. Parser-blocking resource waits use
/// <see cref="ParserBaton"/>'s existing budgeted resource pump.
/// </remarks>
internal sealed partial class ParserDriver : IDisposable
{
    private readonly PageRuntime _runtime;
    private readonly PageNetwork _network;
    private readonly PageNetworkRecorder _requests;
    private readonly HttpClient _client;
    private readonly ParserBaton _baton;
    private readonly string _url;
    private readonly long _maxBytes;
    private readonly int _maxRedirects;
    private readonly TimeSpan _timeout;
    private readonly CancellationToken _cancellationToken;

    private bool _importMapRead;
    private Element? _importMapElement;
    private DomBrowsingContext? _context;
    private int _frameDocuments;
    private bool _tokenizing;
    private int _pendingResourceEvents;
    private TaskCompletionSource? _resourceEventsCompleted;
    private Queue<(Element Element, string Type, bool AfterParse)>? _deferredResourceEvents;

    private ParserDriver(PageRuntime runtime, string url, CancellationToken cancellationToken)
    {
        _runtime = runtime;
        _network = runtime.Network;
        _requests = runtime.Requests;
        _client = runtime.Network.ClientFor(runtime.Engine);
        _url = url;
        _maxBytes = runtime.Options.MaxSubresourceBytes;
        _maxRedirects = runtime.Options.MaxRedirects;
        _timeout = runtime.Options.SubresourceTimeout;
        _cancellationToken = cancellationToken;
        _baton = new ParserBaton(runtime.Engine, runtime.Options.PumpIdle, OnPumpError, cancellationToken);
    }

    /// <summary>Parses <paramref name="markup"/> as <paramref name="url"/> and runs the document's scripts.</summary>
    /// <remarks>
    /// Called on the page loop, and returns to it with the whole load finished.
    /// <paramref name="contentType"/> is the response's, which is what decides between HTML's
    /// <a href="https://html.spec.whatwg.org/multipage/document-lifecycle.html#read-html">read HTML</a> and
    /// <a href="https://html.spec.whatwg.org/multipage/document-lifecycle.html#read-xml">read XML</a>;
    /// everything a page synthesizes for itself — <c>about:blank</c>, <c>setContent</c>, a text document's
    /// <c>&lt;pre&gt;</c> — is already the markup HTML asked for and states <c>text/html</c>.
    /// </remarks>
    internal static PageLoad Load(
        PageRuntime runtime,
        string markup,
        string url,
        string contentType,
        Action<NavigationPhase>? onPhase)
    {
        using var construction = runtime.Layout.BeginMutation();
        var driver = new ParserDriver(runtime, url, runtime.Cancellation?.Token ?? CancellationToken.None);
        runtime.Parser = driver;
        runtime.Engine.Disposed += (_, _) => driver.Dispose();
        try { return driver.Run(markup, contentType, onPhase); }
        catch { driver.Dispose(); runtime.Parser = null; throw; }
    }

    /// <summary>Releases the baton, once the parse it served has finished.</summary>
    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var watch in _resourceWatches.Values) watch.Subscription.Dispose();
        _resourceWatches.Clear();
        _baton.Dispose();
    }

    private PageLoad Run(string markup, string contentType, Action<NavigationPhase>? onPhase)
    {
        var document = new Document(DomContentType.IsXml(contentType) ? DocumentKind.Xml : DocumentKind.Html, contentType, new CustomElementRegistryIdentity(isScoped: false));
        var context = new DomBrowsingContext(document);
        _context = context;
        _runtime.Dom.AssociateContext(context);
        _runtime.Document = document;
        try
        {
            Parse(document, markup);
            _runtime.Dom.RecordSubtree(document);
            if (_runtime.Options.MaxDomNodes is var maxNodes and > 0 && Exceeds(document, maxNodes))
            {
                throw new NavigationFailedException(_url, "The document has more than the "
                    + maxNodes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " nodes BrowserOptions.MaxDomNodes allows.");
            }
            onPhase?.Invoke(NavigationPhase.Committed);
            FinishLoad(document, onPhase);
            return new PageLoad(document, context, ScriptsRun);
        }
        catch
        {
            _runtime.Document = null;
            context.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Whether the tree rooted at <paramref name="node"/> holds more than <paramref name="limit"/> nodes.
    /// </summary>
    /// <remarks>
    /// An explicit stack rather than recursion, because the depth is the document's and a document is
    /// something a stranger wrote; and it stops at the first node past the limit rather than counting a tree
    /// whose whole point is to be too large.
    /// </remarks>
    private bool Exceeds(Node node, int limit)
    {
        var pending = new Stack<Node>();
        pending.Push(node);
        var seen = 0;

        var work = new DomReadWork(_runtime.Dom.NativeReadCheckpoint, _cancellationToken);
        work.Check();
        while (pending.Count > 0)
        {
            work.Step();
            var current = pending.Pop();
            if (++seen > limit)
            {
                work.Check();
                return true;
            }

            for (var child = current.FirstChild; child is not null; child = child.NextSibling)
            {
                work.Step();
                pending.Push(child);
            }
        }

        work.Check();
        return false;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/semantics.html#link-type-stylesheet — queue the resource
    /// event after processing the stylesheet, not when its bytes arrive.
    /// </summary>
    internal void StyleSheetProcessed(Element link, Exception? error = null)
    {
        Serve<object?>(() =>
        {
            if (error is null)
            {
                QueueResourceEvent(link, "load", afterParse: false);
            }
            else if (!_cancellationToken.IsCancellationRequested)
            {
                FailSubresource(link, Attribute(link, "href") ?? _url, "The stylesheet could not be processed: " + error.Message);
            }

            return null;
        });
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#queue-an-element-task — a resource event is
    /// queued at the element rather than fired where the bytes landed, and the document's <c>load</c> waits
    /// for every one of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two kinds of element come through here and both need the delay for the same reason.</b> A style
    /// sheet's listener has to see <c>link.sheet</c>, which the CSS processor assigns after the fetch
    /// returns; an image's has to see <c>complete</c>, <c>naturalWidth</c> and <c>currentSrc</c>, which the
    /// image lane assigns at the same point. Queuing at the fetch instead would let a parser-time network
    /// pump deliver before either exists.
    /// </para>
    /// <para>
    /// <b>The count is what delays the window's <c>load</c></b>, which is
    /// https://html.spec.whatwg.org/multipage/parsing.html#the-end step 6: spin until nothing delays the load
    /// event. An image request in flight is one of the things that does.
    /// </para>
    /// </remarks>
    /// <param name="element">The element the event is fired at.</param>
    /// <param name="type">Either <c>load</c> or <c>error</c>.</param>
    /// <param name="afterParse">
    /// Whether the event has to wait for the tokenizer to finish, which an image's does and a style sheet's
    /// does not. It is the one place this browser's synchronous subresource fetch is visible: a browser
    /// yields to its event loop only for a parser-blocking script, so a queued task reaches a page mid-parse
    /// only where a browser would also have yielded — and this browser yields while it fetches an image,
    /// where a browser would have carried on tokenizing. Delivering there would make an
    /// <c>&lt;img src&gt;</c> followed by a <c>&lt;script&gt;</c> that installs <c>onload</c> — the
    /// commonest image pattern there is — miss the event, which no browser does, because a real network is
    /// slower than the next fifty bytes of markup. A style sheet is deliberately not deferred: the parser
    /// really does wait for one, and <c>AStyleSheetLoadDuringAParserNetworkWaitSeesTheInstalledSheet</c>
    /// pins that its <c>load</c> arrives while it does.
    /// </param>
    private void QueueResourceEvent(Element element, string type, bool afterParse)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_pendingResourceEvents++ == 0)
        {
            _resourceEventsCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        // The processor assigns link.Sheet after the styling service returns, before the next hand-off.
        _runtime.Engine.Tasks.Post(() => DeliverResourceEvent(element, type, afterParse));
    }

    private void DeliverResourceEvent(Element element, string type, bool afterParse)
    {
        // Engine.Execute drains tasks even for a script inserted by another script. Resource events must
        // wait for the outermost script element to return and restore document.currentScript first — and an
        // image's for the tokenizer as well, for the reason QueueResourceEvent gives.
        if (_runtime.CurrentScript is not null || (afterParse && _tokenizing))
        {
            (_deferredResourceEvents ??= new()).Enqueue((element, type, afterParse));
            return;
        }

        try
        {
            if (!_cancellationToken.IsCancellationRequested)
            {
                if (type == "load" && IsHtml(element, "iframe") && element is { } frame)
                {
                    FinishFrame(frame);
                }
                else
                {
                    FireAt(element, type);
                }
            }
        }
        finally
        {
            if (--_pendingResourceEvents == 0)
            {
                _resourceEventsCompleted!.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Re-posts every resource event that was waiting for a script to return or for the parse to end.
    /// </summary>
    /// <remarks>
    /// <b>It has to run before <see cref="FinishLoad"/>'s drain and not only after a script</b>: the drain
    /// waits for the pending count to reach zero, and an entry parked in this queue with no task posted for
    /// it would never be counted down.
    /// </remarks>
    private void FlushDeferredResourceEvents()
    {
        if (_deferredResourceEvents is not { } deferred)
        {
            return;
        }

        // One pass over what is there, keeping in the queue what is still waiting for the tokenizer: posting
        // an image's event back only for it to park again would be one task per script per image.
        for (var pending = deferred.Count; pending > 0; pending--)
        {
            var entry = deferred.Dequeue();

            if (entry.AfterParse && _tokenizing)
            {
                deferred.Enqueue(entry);
                continue;
            }

            _runtime.Engine.Tasks.Post(() => DeliverResourceEvent(entry.Element, entry.Type, entry.AfterParse));
        }
    }

    /// <summary>Records a script a frame's document asked for and this browser will not run.</summary>
    private void RefuseFrameScript(string url)
        => _requests.RecordNotFetched(
            url,
            RequestInitiator.Subresource,
            PageRequestKind.Script,
            "child-frame scripting requires a same-origin, unsandboxed document");

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/images.html#update-the-image-data — the image an
    /// <c>&lt;img&gt;</c> or an <c>&lt;input type=image&gt;</c> named, fetched over the page's own network
    /// position and reduced to the two numbers a pixel-free browser can honestly hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>AngleSharp asks and this answers; the state is the binding's.</b> The request itself is
    /// AngleSharp's <c>ImageRequestProcessor</c>, which is what turns a parsed <c>src</c>, a
    /// <c>img.src = …</c> and a <c>srcset</c> rewrite into exactly one fetch and what makes the document
    /// delay its <c>load</c> event while one is outstanding. What it cannot produce is HTML's current-request
    /// state or an intrinsic size, so <see cref="Media.PageImages"/> holds both — see that class for what a
    /// browser with no pixels can and cannot say. <b>The URL is the binding's too</b>: AngleSharp's source
    /// set reads no descriptor and no <c>media</c>, so which candidate of a <c>srcset</c> or a
    /// <c>&lt;picture&gt;</c> is actually fetched is <see cref="Media.ImageSourceSet"/>'s answer over the
    /// page's own viewport, and only the request around it stays AngleSharp's.
    /// </para>
    /// <para>
    /// <b>The three endings are the standard's three.</b> Bytes whose container
    /// <see cref="Media.ImageHeader"/> recognises are <i>completely available</i> and the element hears
    /// <c>load</c>; a fetch that failed and a container it does not recognise are both the <i>broken</i>
    /// state and both hear <c>error</c>, which is step 25's "not in a supported file format" arm. Neither
    /// event is fired here: both are queued as element tasks through
    /// <see cref="QueueResourceEvent"/>, so a listener added after <c>img.src = …</c> in the same script
    /// still hears them and every one of them delays the window's <c>load</c>.
    /// </para>
    /// <para>
    /// <b><c>loading=lazy</c> loads eagerly, and it has to.</b> HTML defers a lazy image until it is within
    /// the lazy load root's scrolling area, which is a question about a layout this browser does not have.
    /// Never loading one would leave every image of an infinite-scroll page <c>complete === false</c>
    /// forever, which is the state those libraries block on; so the attribute is parsed, reflected and
    /// otherwise ignored, exactly as <see cref="Observers"/>' <c>IntersectionObserver</c> reports every
    /// target as intersecting for the same reason.
    /// </para>
    /// <para>
    /// <b>The ceiling is <see cref="BrowserOptions.MaxImageRequests"/></b>, counted over the document rather
    /// than per element. Zero means images are not fetched at all, which is exactly what this browser did
    /// before there was a model: the reference is recorded in <see cref="Page.Requests"/> with a
    /// <see cref="PageRequest.NotFetchedReason"/>, no socket is opened, and no event is fired — because
    /// nothing was attempted and an <c>error</c> would say something was.
    /// </para>
    /// </remarks>
    private FetchedBody? FetchImage(Element image, string requested)
    {

        return Serve(() =>
        {
            var images = _runtime.Images;

            // https://html.spec.whatwg.org/multipage/images.html#update-the-source-set — the candidate
            // AngleSharp put in the request is the *first* one of the first srcset it found, whatever the
            // descriptors and the media say, so the URL that is actually fetched is decided here instead.
            // See Media/ImageSourceSet, and Dom/divergences.md for what AngleSharp's own answer misses.
            var url = Media.ImageSourceSet.Select(_runtime, image);

            if (url is null)
            {
                // The selection produced nothing — an empty `srcset`, or a `<picture>` whose every
                // `<source>` was ruled out and whose `<img>` has no `src`. HTML fires `error` here and this
                // does not: see Dom/divergences.md, which records why AngleSharp gives no notification to
                // hang one on for the case it never asks the loader about at all.
                _requests.RecordNotFetched(
                    requested,
                    RequestInitiator.Subresource,
                    PageRequestKind.Image,
                    "no image source was selected: every candidate was ruled out by its media, its type or "
                        + "its descriptor");
                return null;
            }

            // https://html.spec.whatwg.org/multipage/images.html#update-the-image-data step 7.3: an image
            // already in the list of available images under this key is taken from it, with no request and
            // — the previous URL being this one — no event.
            if (images.IsAlreadyAvailable(image, url))
            {
                return null;
            }

            var request = images.Begin(image, url);
            var ceiling = _runtime.Options.MaxImageRequests;

            if (!images.TryStart(ceiling))
            {
                _requests.RecordNotFetched(
                    url,
                    RequestInitiator.Subresource,
                    PageRequestKind.Image,
                    ceiling <= 0
                        ? "images are not fetched: BrowserOptions.MaxImageRequests is zero"
                        : "an image is not fetched: this document has already reached the "
                            + ceiling.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " image requests BrowserOptions.MaxImageRequests allows");
                return null;
            }

            // From here the element has asked for this URL, so `currentSrc` names it whatever the fetch
            // does next: HTML sets the current request's current URL from the selected source in both the
            // success and the failure arm. A ceiling refusal above deliberately does not get here.
            request.Requested = true;

            var fetched = FetchBytes(url, image, "image", PageRequestKind.Image, mayPump: false);

            if (fetched is null)
            {
                // FailSubresource has already recorded the failure and queued the element's `error`, unless
                // the document is being abandoned — in which case there is nobody left to tell.
                Media.PageImages.Break(request);
                return null;
            }

            if (!Media.ImageHeader.TryRead(fetched.Value.Bytes, out var width, out var height))
            {
                Media.PageImages.Break(request);
                FailSubresource(
                    image,
                    url,
                    "The image '" + url + "' is not in a container format this browser can read a size out of.");
                return null;
            }

            Media.PageImages.Complete(request, width, height);
            QueueResourceEvent(image, "load", afterParse: true);

            return fetched;
        });
    }

    /// <summary>
    /// Whether <paramref name="document"/> belongs to a child frame rather than to the page itself.
    /// </summary>
    /// <remarks>
    /// A frame's document is opened into the child browsing context AngleSharp made for the element, and a
    /// child context copies its parent's services — so the page's own <c>IScriptingService</c> and
    /// <c>IResourceLoader</c> are what a frame's document asks. The context is therefore the only thing that
    /// separates the two, and it is what says which document a script belongs to.
    /// </remarks>
    private bool IsFrameDocument(Document document)
        => !ReferenceEquals(DomBrowsingContext.Of(document), _context);

    private bool CanRunFrame(Document document) => FrameWindows.CanRunScripts(_runtime, document);

    // Every native parser call is already on the page loop.
    private static T Serve<T>(Func<T> work) => work();

    /// <summary>
    /// The bytes of one subresource, before anything has been made of them.
    /// </summary>
    /// <remarks>
    /// <see cref="Fetch"/> wraps them into the response AngleSharp reads; the image lane needs the bytes
    /// themselves, because <see cref="Media.ImageHeader"/> is what decides between the completely-available
    /// and the broken state and it has to decide before the element hears anything.
    /// </remarks>
    private FetchedBody? FetchBytes(
        string url,
        Element source,
        string what,
        PageRequestKind kind,
        bool mayPump)
    {
        var target = UrlParser.Parse(url);

        if (target is null)
        {
            FailSubresource(source, url, "'" + url + "' is not a URL a page can load.");
            return null;
        }

        if (DataUrl.Is(target))
        {
            return FetchDataUrl(target, source, url, what);
        }

        if (!PageUrl.IsNetworkScheme(target))
        {
            FailSubresource(source, url, "'" + url + "' is not a URL a page can load.");
            return null;
        }

        // The same record twice, deliberately: as the referrer it is the URL a `Referer` header carries, and
        // as the origin it is kept for its origin alone — the transport compares `SerializeOrigin()` and
        // derives the `Origin` header from it, never the path. `DocumentFetch` and `fetch()` pass the same
        // shape for the same reason.
        var documentUrl = UrlParser.Parse(_runtime.DocumentUrl);
        var request = new SubresourceRequest(
            target,
            documentUrl,
            documentUrl,
            _maxBytes,
            _maxRedirects,
            RequestInitiator.Subresource,
            _runtime.Emulation.EffectiveUserAgent,
            kind);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        timeout.CancelAfter(_timeout);

        var fetch = SubresourceFetch.LoadAsync(_network, _client, request, _requests, timeout.Token);

        try
        {
            var fetched = mayPump ? _baton.PumpUntil(fetch) : fetch.GetAwaiter().GetResult();
            return new FetchedBody(
                fetched.Bytes,
                ResponseUrl(fetched.Url, fetched.Fragment),
                fetched.ContentType);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            // The document is being left or the page is closing; there is nobody left to tell.
            return null;
        }
        catch (OperationCanceledException)
        {
            FailSubresource(source, url, "The " + what + " '" + url + "' did not answer within "
                + _timeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " seconds.");
            return null;
        }
        catch (Exception exception)
        {
            FailSubresource(source, url, "The " + what + " '" + url + "' could not be loaded: " + exception.Message);
            return null;
        }
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#scheme-fetch's <c>data</c> arm: a URL that carries its own bytes,
    /// answered without a socket.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the one subresource scheme besides <c>http</c> and <c>https</c>, and it is not a network
    /// position at all.</b> There is nothing here for the context's <c>UrlFilter</c>, the cookie jar, a
    /// redirect budget or the request log to decide — the bytes were in the document that named them — which
    /// is the same reason <c>fetch</c>'s <c>blob</c> arm runs before every one of those checks. Nothing is
    /// recorded in <see cref="Page.Requests"/> for the same reason <c>about:blank</c> records nothing: a
    /// page that asked for nothing made no request.
    /// </para>
    /// <para>
    /// <b>The one bound that does apply is the size one.</b> A <c>data:</c> URL is as large as the markup
    /// that carried it, so <see cref="BrowserOptions.MaxSubresourceBytes"/> is checked here exactly as
    /// <see cref="SubresourceFetch"/> checks it over the wire; a page may not escape it by inlining.
    /// </para>
    /// </remarks>
    private FetchedBody? FetchDataUrl(UrlRecord target, Element source, string url, string what)
    {
        if (!DataUrl.TryProcess(target, out var content))
        {
            FailSubresource(source, url, "The " + what + " '" + url + "' is not a valid data: URL.");
            return null;
        }

        if (content.Body.LongLength > _maxBytes)
        {
            FailSubresource(
                source,
                url,
                "The " + what + " '" + url + "' carries more than the "
                    + _maxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " bytes a page may load.");
            return null;
        }

        // https://fetch.spec.whatwg.org/#concept-response-url: the response URL is the request's, so the
        // fragment a data: URL was written with survives into what an error report names.
        return new FetchedBody(content.Body, target.Serialize(), content.MimeType.Serialize());
    }

    /// <summary>One subresource's bytes, the URL they were answered under, and what the server called them.</summary>
    private readonly record struct FetchedBody(byte[] Bytes, string Url, string? ContentType);

    /// <summary>The URL a fetched subresource is answered under.</summary>
    /// <remarks>
    /// <para>
    /// Fetch does not send a fragment to the server, and <see cref="SubresourceFetch"/> therefore serializes
    /// its public response URL without one. Its separate fragment preserves the final request URL's three
    /// states across redirects: absent, explicitly empty, or non-empty.
    /// </para>
    /// <para>
    /// <b>Every response carries it back, not only a nested document's.</b>
    /// https://fetch.spec.whatwg.org/#concept-response-url is the request's URL, fragment and all — the
    /// fragment is left out of the request-target and of nothing else — and
    /// https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception names the script's own
    /// URL, so <c>&lt;script src="a.js#"&gt;</c> has to report the <c>#</c> that
    /// https://url.spec.whatwg.org/#concept-url-serializer keeps for a non-null empty fragment. Answering
    /// the transport URL instead made <c>onerror</c> disagree with the <c>src</c> the same element
    /// reflects. AngleSharp reads it for a document's <c>location</c> and Selectors' <c>:target</c>, and for
    /// a script and a style sheet it is the base URL, which a fragment plays no part in resolving against.
    /// </para>
    /// </remarks>
    private static string ResponseUrl(string responseUrl, string? fragment)
    {
        if (fragment is null || UrlParser.Parse(responseUrl) is not { } documentUrl)
        {
            return responseUrl;
        }

        documentUrl.Fragment = fragment;
        return documentUrl.Serialize();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html — a resource that failed to load fires
    /// <c>error</c> at the element that asked for it, and the page carries on loading.
    /// </summary>
    private void FailSubresource(Element source, string url, string message)
    {
        Report(PageErrorKind.ReportedError, message, url);
        if (IsHtml(source, "link"))
        {
            QueueResourceEvent(source, "error", afterParse: false);
        }
        else if (IsHtml(source, "img") || IsHtml(source, "input"))
        {
            QueueResourceEvent(source, "error", afterParse: true);
        }
        else
        {
            FireAt(source, "error");
        }
    }

    /// <summary>Dispatches a simple event at an element through Jint's dispatcher.</summary>
    /// <remarks>
    /// AngleSharp fires its own <c>load</c> and <c>error</c> into its own listener lists, which hold nothing
    /// a script registered — so the one a page can hear is this one. Neither bubbles, which is HTML's rule
    /// for a resource event.
    /// </remarks>
    private void FireAt(Node node, string type)
    {
        if (_runtime.Dom.WrapNode(node) is { } wrapper)
        {
            PageEvents.Fire(_runtime, wrapper, type);
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // Script execution.
    // ---------------------------------------------------------------------------------------------------

    private void Execute(FetchedBody? response, Element element)
    {
        var external = response is not null;

        if (element.NamespaceUri == Namespaces.Html && Attribute(element, "nomodule") is not null)
        {
            return;
        }

        string text;
        string source;
        string location;
        var line = 1;

        if (external)
        {
            text = Read(response!.Value, element);
            source = response!.Value.Url;
            location = source;
        }
        else
        {
            // https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception: an inline script's
            // *filename* is the document's URL, so that is what the engine is given as the source name, and
            // the line the script starts on is a parsing offset rather than part of the name. The page's own
            // error recorder still gets the `url:line` string it always did, which is the one a host reads.
            text = TextOf(element);
            source = DomDocumentState.Of(element.OwnerDocument!).Url;
            line = LineOf(element, text);
            location = source + ":" + line;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            if (external)
            {
                FireAt(element, "load");
            }

            return;
        }

        // https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-content-attributes: a handler
        // content attribute becomes the handler when the attribute is *set*, which for `<body onerror>` is
        // when the body element is parsed — before this script and before any listener it could add. The
        // body's handlers are the window's, so nothing else would build that wrapper; doing it here is what
        // lets `<body onerror>` hear an exception from a script that follows it in the document. After the
        // first script it is one lookup in the wrapper cache.
        if (element.OwnerDocument is { } owner)
        {
            Events.EventHandlerContentAttributes.InstallBodyHandlers(_runtime.Dom, owner);
        }

        var previous = _runtime.CurrentScript;
        var scriptDom = _runtime.Dom.RealmOfDocument(element.OwnerDocument!);
        var previousInDocument = scriptDom.CurrentScript;
        scriptDom.CurrentScript = element;

        // https://html.spec.whatwg.org/multipage/dom.html#dom-document-currentscript: a classic script only,
        // which is exactly why it is set here and not around a module.
        _runtime.CurrentScript = element;
        ScriptsRun++;

        try
        {
            // A turn of its own, nested inside the mailbox request that is parsing the document: the deadline
            // is re-armed for this script and the enclosing turn gets a full budget back on the way out, so
            // each script is bounded and a document is not failed for containing many. See PageBudget.
            using (_runtime.Budget.BeginTurn())
            {
                _runtime.Engine.Execute(text, source, ParsingFrom(line));
            }
        }
        catch (JavaScriptException exception)
        {
            // HTML's "report the exception" step, both halves: the `error` event at the global scope, which
            // is what `window.onerror` and `<body onerror>` hear, and then the page's own recorder. The
            // script ends and the parse goes on either way.
            ReportException(exception, location);
        }
        catch (Exception exception) when (PageBudget.IsBudgetFailure(exception))
        {
            // The same step for a budget rather than a throw: this script ends, the parse goes on with a
            // budget of its own, and the document still loads. A page whose first script is a loop is still
            // a page a host can read.
            Report(PageErrorKind.BudgetExceeded, exception.Message, location);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Everything else a script can end with, and the ones that matter are the bounds: a per-turn
            // deadline or a memory budget throws out of Execute, and it arrives here on the *loop* thread
            // inside a baton hand-off. Letting it out is worse than it looks — AngleSharp's script processor
            // wraps EvaluateScriptAsync in `catch (Exception) { TrackError }`, so a constraint abort raised
            // by an inline or deferred script would vanish into AngleSharp's own error list, while one
            // raised where the resource loader is running would fault the parse and fail the navigation.
            // Neither is the contract, which is HTML's: recorded, and the page survives its scripts.
            // `Execute` and `RunModule` are the only two places a script runs, so they are the only two
            // that owe this.
            Report(PageErrorKind.ScriptError, exception.Message, location);
        }
        finally
        {
            _runtime.CurrentScript = previous;
            scriptDom.CurrentScript = previousInDocument;
            if (previous is null)
            {
                FlushDeferredResourceEvents();
            }
        }

        // https://html.spec.whatwg.org/multipage/scripting.html#execute-the-script-element: load fires at an
        // element that had a src to fetch, and at nothing else.
        if (external)
        {
            FireAt(element, "load");
        }
    }

    /// <summary>How many scripts this parse executed.</summary>
    internal int ScriptsRun { get; private set; }

    /// <summary>
    /// The script's source text, decoded with the response's charset, then the element's, then the
    /// document's.
    /// </summary>
    private static string Read(FetchedBody response, Element element)
    {
        var fallback = element.GetAttribute("charset") is { Length: > 0 } charset
            ? charset : DomDocumentState.Of(element.OwnerDocument!).CharacterSet;
        return new FetchedSubresource(response.Bytes, response.ContentType, response.Url,
            UrlParser.Parse(response.Url)?.Fragment, 200).Text(fallback);
    }

    // ---------------------------------------------------------------------------------------------------
    // After the parse: modules, then the lifecycle.
    // ---------------------------------------------------------------------------------------------------

    private void FinishLoad(Document document, Action<NavigationPhase>? onPhase)
    {
        if (_runtime.Document is { } nativeDocument) DomDocumentState.SelectNavigationTarget(_runtime.Dom, nativeDocument);

        // The handler content attributes on <body> that HTML redirects to the window — onload above all —
        // belong to a target the body's own wrapper is what registers them on. Every other element's arrive
        // with its wrapper; see EventHandlerContentAttributes.InstallBodyHandlers for why this one cannot.
        // First, so a markup handler is ahead of any listener a deferred or module script adds. Not at all
        // when scripting is disabled, which is where every other handler content attribute stops too.
        if (_runtime.ScriptingEnabled)
        {
            Events.EventHandlerContentAttributes.InstallBodyHandlers(_runtime.Dom, document);
        }

        // The last parse boundary: everything the tokenizer wrote after the final inline script becomes
        // custom before DOMContentLoaded, which is where a page looks for it.
        _runtime.CustomElementsIfCreated?.UpgradeParsedElements();

        // https://html.spec.whatwg.org/multipage/parsing.html#the-end step 2. AngleSharp advances its own
        // readiness during the parse and its setter is not reachable from outside its assembly
        // (AngleSharp#1309), so what a page reads is the runtime's shadow, moved here.
        SetReadyState("interactive");

        // The module half of "scripting is disabled": AngleSharp never sees a module script, so refusing one
        // is this driver's own business rather than a service it can decline to register.
        if (_runtime.ScriptingEnabled)
        {
            RunModules(document);
        }

        var window = _runtime.Engine._webApi?.GlobalEventTarget;
        var wrapper = _runtime.DocumentWrapper;

        if (wrapper is not null)
        {
            PageEvents.Dispatch(
                _runtime,
                wrapper,
                _runtime.Engine._mainRealm.Intrinsics.Event.CreateTrustedEvent(
                    JsString.Create("DOMContentLoaded"),
                    new EventInit(Bubbles: true, Cancelable: false, Composed: false)));
        }

        onPhase?.Invoke(NavigationPhase.DomContentLoaded);

        // https://html.spec.whatwg.org/multipage/interaction.html#the-autofocus-attribute — the autofocus
        // candidate is flushed once the document has parsed, before `load`, and it fires the focus events.
        Events.FocusController.FlushAutofocus(_runtime.Dom, document);

        // Step 6 of "the end": spin until nothing delays the load event. An <iframe> delays it
        // (https://html.spec.whatwg.org/multipage/iframe-embed-object.html#the-iframe-element), so its own
        // load lands here — after DOMContentLoaded, before readyState becomes "complete" and before the
        // window's load.
        FireFrameLoads(document);

        // The tokenizer has finished, so every image event that was waiting for it is posted now — after
        // DOMContentLoaded, which is where a browser's images land too, and before the window's load, which
        // an image request delays.
        FlushDeferredResourceEvents();

        // A style sheet's or an image's completion handler can insert further subresources. Keep their
        // load-event delay until the entire chain has delivered, each event on its own budgeted task.
        while (_pendingResourceEvents > 0 && !_cancellationToken.IsCancellationRequested)
        {
            _baton.PumpUntil(_resourceEventsCompleted!.Task);
        }

        // Step 9: readiness becomes "complete" and only then does load fire, which is why a load listener
        // reads "complete" rather than "interactive".
        SetReadyState("complete");

        if (window is not null)
        {
            PageEvents.Fire(_runtime, window, "load");

            // https://html.spec.whatwg.org/multipage/browsing-the-web.html#history-traversal: pageshow follows
            // load, and its persisted flag is false because nothing here restores a document from a cache.
            var pageShow = PageEvents.Create(_runtime, "pageshow");
            PageEvents.Member(pageShow, "persisted", JsBoolean.False);
            PageEvents.Dispatch(_runtime, window, pageShow);
        }

        onPhase?.Invoke(NavigationPhase.Loaded);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/iframe-embed-object.html#iframe-load-event-steps — <c>load</c>
    /// at every frame element whose document arrived, innermost first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Innermost first, because that is the order the documents finished in.</b> A frame's document is
    /// opened while its parent's parse is still running, so a frame nested two deep completed before the one
    /// that holds it; walking the tree the other way round would tell a page the outer frame loaded first.
    /// </para>
    /// <para>
    /// <b>Only a frame that has a document fires.</b> One whose fetch failed already heard <c>error</c> from
    /// <see cref="FailSubresource"/>, one over <see cref="BrowserOptions.MaxFrameDocuments"/> hears nothing
    /// because nothing was attempted, and one with no <c>src</c> and no <c>srcdoc</c> has no document at all
    /// — where a browser would give it <c>about:blank</c> and fire. That last one is the divergence this
    /// leaves: a frame is given a document by what it points at, and an empty frame points at nothing.
    /// </para>
    /// <para>
    /// AngleSharp fires its own <c>load</c> into its own listener list, which holds nothing a script
    /// registered; <see cref="FireAt"/> is the one a page can hear. A <c>&lt;frame&gt;</c> is not here
    /// because it never gets a document — see <see cref="IsLegacyFrame"/>.
    /// </para>
    /// </remarks>
    private void FireFrameLoads(Document document)
    {
        foreach (var element in NativeElements(document))
        {
            if (IsHtml(element, "iframe") && DomBrowsingContext.OfFrame(element)?.Active is not null)
                FinishFrame(element);
        }
    }

    private void FinishFrame(Element frame)
    {
        if (!ShadowTree.IsConnected(frame, _cancellationToken) || DomBrowsingContext.OfFrame(frame)?.Active is not { } document) return;
        var dom = FrameWindows.DocumentRealm(_runtime, document);
        if (dom.LoadCompleted) return;
        dom.LoadCompleted = true;
        if (CanRunFrame(document))
        {
            FrameWindows.ForDocument(_runtime, document);
            using var scope = new RealmScope(_runtime.Engine, dom.OwningRealm);
            if (_runtime.ScriptingEnabled) Events.EventHandlerContentAttributes.InstallBodyHandlers(dom, document);
            SetFrameReadyState(dom, "interactive");
            PageEvents.Fire(_runtime, dom.WrapNode(document), "DOMContentLoaded", bubbles: true);
            FireFrameLoads(document);
            SetFrameReadyState(dom, "complete");
            PageEvents.Fire(_runtime, dom.WindowTarget!, "load");
            var shown = dom.OwningRealm.Intrinsics.Event.CreateTrustedEvent(JsString.Create("pageshow"), default);
            PageEvents.Member(shown, "persisted", JsBoolean.False);
            PageEvents.Dispatch(_runtime, dom.WindowTarget!, shown);
        }
        else FireFrameLoads(document);
        FireAt(frame, "load");
    }

    private void SetFrameReadyState(DomRealm dom, string state)
    {
        if (dom.ReadyState == state)
        {
            return;
        }
        dom.ReadyState = state;
        PageEvents.Fire(_runtime, dom.WrapNode(dom.Document!), "readystatechange");
    }

    /// <summary>Moves the page's <c>document.readyState</c> and fires <c>readystatechange</c> at the document.</summary>
    internal void SetReadyState(string state)
    {
        if (string.Equals(_runtime.ReadyState, state, StringComparison.Ordinal))
        {
            return;
        }

        _runtime.ReadyState = state;

        if (_runtime.DocumentWrapper is { } wrapper)
        {
            PageEvents.Fire(_runtime, wrapper, "readystatechange");
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#integration-with-the-javascript-module-system —
    /// the document's import map, then every module script in document order.
    /// </summary>
    private void RunModules(Document document)
    {
        var loader = _runtime.Modules;
        if (loader is null)
        {
            return;
        }

        loader.BaseUrl = BaseUrlOf(document);

        var modules = new List<Element>();
        var mapSeen = _importMapRead;

        foreach (var element in NativeElements(document))
        {
            if (!IsHtml(element, "script"))
            {
                continue;
            }

            var script = element;
            var flags = script.GetHtmlState()!.Script!;
            if (!ReferenceEquals(flags.PreparationTimeDocument, document)) continue;
            var type = ScriptType(script);

            if (type == NativeScriptType.ImportMap)
            {
                if (mapSeen)
                {
                    if (!ReferenceEquals(script, _importMapElement))
                    {
                        Report(
                            PageErrorKind.ReportedError,
                            "A document may declare only one import map; this one was ignored.",
                            _url);
                    }

                    continue;
                }

                mapSeen = true;
                _importMapRead = true;
                _importMapElement = script;
                loader.Map = ReadImportMap(script, loader.BaseUrl);
                continue;
            }

            if (type == NativeScriptType.Module)
            {
                modules.Add(script);
            }
        }

        foreach (var script in modules)
        {
            RunModule(loader, script);
        }
    }

    /// <summary>
    /// Reads the document's import map mid-parse, so that a bare specifier a classic script hands to
    /// <c>import()</c> resolves through it.
    /// </summary>
    /// <remarks>
    /// <b>The one place this diverges from HTML on import maps.</b> The standard requires a map to precede
    /// the first module script and makes a later one an error; here the first map found anywhere in the
    /// document applies to every module, because the modules all run after the parse and there is no moment
    /// at which one of them could have resolved without it.
    /// </remarks>
    private void ReadImportMapEarly(Document document)
    {
        if (_importMapRead || _runtime.Modules is not { } loader)
        {
            return;
        }

        var script = NativeElements(document).FirstOrDefault(element => IsHtml(element, "script") && ScriptType(element) == NativeScriptType.ImportMap && ReferenceEquals(element.GetHtmlState()!.Script!.PreparationTimeDocument, document));
        if (script is null)
        {
            return;
        }

        _importMapRead = true;
        _importMapElement = script;
        loader.Map = ReadImportMap(script, loader.BaseUrl);
    }

    private ImportMap? ReadImportMap(Element script, string baseUrl)
    {
        if (!string.IsNullOrEmpty(Attribute(script, "src")))
        {
            // https://html.spec.whatwg.org/multipage/webappapis.html#import-map-processing-model: an import
            // map is inline text, and an external one is a parse error rather than a fetch.
            Report(PageErrorKind.ReportedError, "An import map cannot have a src attribute; this one was ignored.", _url);
            return null;
        }

        var problems = new List<string>();
        var map = ImportMap.Parse(TextOf(script), baseUrl, problems);

        foreach (var problem in problems)
        {
            Report(PageErrorKind.ReportedError, problem, _url);
        }

        return map;
    }

    private void RunModule(PageModuleScriptLoader loader, Element script)
    {
        var state = _resourceSources.GetValue(script, static _ => new ResourceSource());
        if (state.ModuleStarted) return;
        state.ModuleStarted = true;
        string specifier;

        if (Attribute(script, "src") is { Length: > 0 } source)
        {
            var resolved = PageUrl.Resolve(source, loader.BaseUrl);

            if (resolved is null)
            {
                FailSubresource(script, source, "The module script '" + source + "' is not a URL a page can load.");
                return;
            }

            specifier = resolved;
        }
        else
        {
            var text = TextOf(script);

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            specifier = loader.AddInline(text);
        }

        ScriptsRun++;

        ModuleImportOperation operation;

        try
        {
            // Starting an import parses and links on this thread, so it is script running and takes a turn of
            // its own. The graph's *load* is not inside it: the pump below brackets each of its own drains,
            // which is where a module's evaluation actually happens.
            using (_runtime.Budget.BeginTurn())
            {
                operation = _runtime.Engine.Modules.StartImport(specifier);
            }
        }
        catch (Exception exception) when (PageBudget.IsBudgetFailure(exception))
        {
            Report(PageErrorKind.BudgetExceeded, exception.Message, specifier);
            FireAt(script, "error");
            return;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Report(PageErrorKind.ScriptError, "The module script '" + specifier + "' failed: " + exception.Message, specifier);
            FireAt(script, "error");
            return;
        }

        // The graph loads over the network and settles into the engine's own job queue, so the loop has to
        // give it turns — the same pump a parser-blocking fetch runs, and the reason a page's timers keep
        // firing while its modules load.
        if (!_baton.PumpUntil(() => operation.IsCompleted, _timeout))
        {
            Report(
                PageErrorKind.ScriptError,
                "The module script '" + specifier + "' did not finish loading within "
                + _timeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " seconds.",
                specifier);
            FireAt(script, "error");
            return;
        }

        if (operation.IsFaulted)
        {
            var message = operation.Error is { } error ? SafeDescribe(error) : "the module failed to load";
            Report(PageErrorKind.ScriptError, "The module script '" + specifier + "' failed: " + message, specifier);
            FireAt(script, "error");
            return;
        }

        FireAt(script, "load");
    }

    private static string SafeDescribe(JsValue error)
    {
        try
        {
            return PageRecorder.Diagnostics.Describe(error, null);
        }
        catch (JavaScriptException)
        {
            return "the module failed to load";
        }
    }

    private string BaseUrlOf(Document document)
        => DomDocumentState.BaseUri(document, _runtime.Engine.Constraints.Check, _cancellationToken);

    /// <summary>
    /// The parsing options an inline script gets: the engine's own, plus the position in the document its
    /// text begins at, so a syntax error and a stack frame both name a line of the <i>document</i>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>window-onerror-parse-error.html</c> is what asks for this by name — it asserts line 34, which is
    /// the line of the document its unparsable <c>&lt;script&gt;</c> sits on. The column is deliberately not
    /// offset: AngleSharp exposes the parser's index just past the closing tag and not the index the text
    /// began at, so the only honest column is the one within the script's own line.
    /// </para>
    /// <para>
    /// A script on line 1 takes the engine's cached default parser instead, because an offset of nothing is
    /// what <c>Execute(text, source)</c> already does and building a parser per script is not free.
    /// </para>
    /// </remarks>
    private ScriptParsingOptions? ParsingFrom(int line)
    {
        if (line <= 1)
        {
            return null;
        }

        var defaults = _runtime.Engine.Options.RetainFunctionSourceText
            ? ScriptParsingOptions.RetainingDefault
            : ScriptParsingOptions.Default;

        return defaults with { SourceOffset = Position.From(line, 0) };
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception, for an exception that
    /// escaped a script this driver ran.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Step 5 before step 6.</b> The <c>error</c> event fires at the global scope first — that is what
    /// reaches <c>window.onerror</c>, a global <c>error</c> listener and <c>&lt;body onerror&gt;</c>, and it
    /// is a no-op on a page whose script registered none of them — and only then is the page's recorder told.
    /// The order is observable: a handler that reads <c>page.Errors</c> through a host binding sees the entry
    /// added after it ran, not before.
    /// </para>
    /// <para>
    /// It runs a listener, so it takes a turn of its own for the reason each script does: a page whose
    /// <c>onerror</c> loops is bounded by its own budget and not by what is left of the enclosing document's.
    /// A budget it exhausts becomes a page error rather than ending the load, which is the same promise
    /// <c>Execute</c> makes for the script that failed in the first place.
    /// </para>
    /// </remarks>
    private void ReportException(JavaScriptException exception, string location)
    {
        try
        {
            using (_runtime.Budget.BeginTurn())
            {
                _runtime.Engine._webApi?.FireGlobalErrorEvent(_runtime.Engine.Realm, exception);
            }
        }
        catch (Exception nested) when (nested is not OperationCanceledException)
        {
            Report(
                PageBudget.IsBudgetFailure(nested) ? PageErrorKind.BudgetExceeded : PageErrorKind.ScriptError,
                nested.Message,
                location);
        }

        Report(PageErrorKind.ScriptError, PageRecorder.Diagnostics.Describe(exception.Error, exception), location);
    }

    private void Report(PageErrorKind kind, string message, string source)
        => _runtime.Recorder.Add(kind, message, source);

    /// <summary>
    /// What erupted out of the baton's pump — a job the diagnostics sink does not cover, or a constraint
    /// aborting one.
    /// </summary>
    /// <remarks>
    /// It is the same debt <c>PageLoop.Pump</c> pays to its own <c>onPumpError</c>: the pump must not end
    /// the load, and what it swallows must not vanish. A constraint abort in particular is the whole reason
    /// this exists — a page bounded by a budget that fires into nothing is a page nobody can debug.
    /// </remarks>
    private void OnPumpError(Exception exception)
        => Report(
            PageBudget.IsBudgetFailure(exception) ? PageErrorKind.BudgetExceeded : PageErrorKind.UncaughtCallbackError,
            exception.Message,
            "ParserDriver");

    // Primary parser coordinates are exact. Inserted or mixed text is script-relative.
    private static int LineOf(Element element, string text)
        => element.GetHtmlState()?.Script?.ParserSourceLocation is { Kind: HtmlSourceKind.Primary, Line: > 0 and <= int.MaxValue } source
            ? (int) source.Line : 1;
}
