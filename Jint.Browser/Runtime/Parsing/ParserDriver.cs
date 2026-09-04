using System.Net.Http;
using Acornima;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using AngleSharp.Scripting;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.Runtime.Modules;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>
/// One document's parse: AngleSharp tokenizing on a thread of its own, every script and every subresource it
/// asks for served back on the page loop, and the load lifecycle fired at the end.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape is the design's baton</b> (<c>docs/design/headless-browser.md</c> §6), and
/// <see cref="ParserBaton"/> carries the argument for it. What this class adds is the script scheduling on
/// top: HTML's <i>prepare a script element</i> as far as AngleSharp does not already implement it, the
/// module half it does not implement at all, and the lifecycle events a page can actually hear.
/// </para>
/// <para>
/// <b>Who runs what.</b> A classic script — inline, external, <c>defer</c> or <c>async</c> — is prepared and
/// ordered by AngleSharp, which is what buys parser-blocking, document order, the deferred queue and the
/// <c>document.write</c> insertion point for nothing. A module script, an import map and everything with an
/// unknown type is invisible to AngleSharp, because <see cref="PageScriptingService.SupportsType"/> answers
/// <see langword="false"/> for them, and this class runs the modules itself after the parse — which is where
/// HTML puts them anyway, modules being deferred by definition.
/// </para>
/// <para>
/// <b>The divergences this shape costs</b>, each of them a scheduling one rather than an ordering one:
/// a <c>defer</c> or <c>async</c> script's <i>download</i> is not overlapped with the parse (see
/// <see cref="PageResourceLoader"/>); an <c>async</c> script executes in document order at the end of the
/// parse rather than the instant its fetch lands, because AngleSharp queues both kinds together; and a
/// deferred classic script runs before a module script that precedes it in the document, because they are
/// two queues rather than HTML's one.
/// </para>
/// </remarks>
internal sealed class ParserDriver : IDisposable
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
    private IHtmlScriptElement? _importMapElement;
    private IBrowsingContext? _context;
    private int _frameDocuments;

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
        _baton = new ParserBaton(runtime.Engine, runtime.Budget, runtime.Options.PumpIdle, OnPumpError, cancellationToken);
    }

    /// <summary>Parses <paramref name="html"/> as <paramref name="url"/> and runs the document's scripts.</summary>
    /// <remarks>Called on the page loop, and returns to it with the whole load finished.</remarks>
    internal static PageLoad Load(PageRuntime runtime, string html, string url, Action<NavigationPhase>? onPhase)
    {
        using var driver = new ParserDriver(runtime, url, runtime.Cancellation?.Token ?? CancellationToken.None);
        return driver.Run(html, onPhase);
    }

    /// <summary>Releases the baton, once the parse it served has finished.</summary>
    public void Dispose() => _baton.Dispose();

    private PageLoad Run(string html, Action<NavigationPhase>? onPhase)
    {
        // WithCss registers the declaration factory `element.style`, the computed-style cascade and the
        // styling service <link rel=stylesheet> needs; the resource loader is what makes AngleSharp ask for
        // anything at all, and is therefore what makes the baton necessary. There is deliberately no
        // WithDefaultLoader: every byte a document pulls goes through the page's own network position.
        // The render device is what the cascade resolves a relative length and an `@media` rule against;
        // without one AngleSharp.Css raises rather than answering for `width: 100%` (see PageRenderDevice).
        // The attribute observer is the last of them, and it is what a custom element's
        // `attributeChangedCallback` is answered from: it is called for every element of this document,
        // attached or not, where a mutation record needs the element to be under the observed document
        // and `el.setAttribute` before insertion is the commonest thing a component does. `.With` adds a
        // service rather than replacing one, so AngleSharp's own observer keeps working.
        var configuration = Configuration.Default
            .WithCss()
            // https://drafts.csswg.org/selectors-4/#the-lang-pseudo — a document's language is the
            // document's. AngleSharp resolves an element with no inherited language through
            // `IBrowsingContext.GetCulture()`, which without this is whatever `CultureInfo.CurrentCulture`
            // the host thread happens to carry: `:lang(en)` then matches an element that declared no
            // language at all on an English machine and matches nothing on an invariant one, so a page's
            // selectors answer differently on two machines running the same document. The engine's own
            // culture is the page's — `Options.Culture`, which a host sets through `ConfigureEngine` and
            // which itself defaults to the current culture, so nothing moves for an embedder who sets
            // none — and it is what a document is parsed and matched against here.
            .WithCulture(_runtime.Engine.Options.Culture)
            .With(new PageResourceLoader(this))
            .With<AngleSharp.Css.IRenderDevice>(_ => new PageRenderDevice(_runtime))
            .With<AngleSharp.Dom.IAttributeObserver>(_ => new CustomElements.CustomElementAttributeObserver(_runtime));

        // https://chromedevtools.github.io/devtools-protocol/tot/Emulation/#method-setScriptExecutionDisabled
        // — the scripting service is simply not registered, which is how AngleSharp is told a document has
        // scripting disabled: no <script> is prepared, and <noscript> parses as the markup it is rather than
        // as text. Refusing each script instead would leave the document believing it could run one.
        if (_runtime.ScriptingEnabled)
        {
            configuration = configuration.With(new PageScriptingService(this));
        }

        var context = BrowsingContext.New(configuration);
        _context = context;
        IDocument document;

        try
        {
            document = Parse(context, html);
        }
        catch
        {
            // The context is this method's until a PageLoad owns it, so a parse that never produced one takes
            // its context with it rather than leaving it to the collector.
            (context as IDisposable)?.Dispose();
            throw;
        }

        _runtime.Document ??= document;

        if (_runtime.Options.MaxDomNodes is var maxNodes and > 0 && Exceeds(document, maxNodes))
        {
            // Before the lifecycle events, so a document over the limit never gets DOMContentLoaded or load:
            // there is nothing to show and the navigation says so. The parse itself cannot be stopped part
            // way — AngleSharp owns it, and its scripts have already run — so this is the first moment the
            // size is known.
            _runtime.Document = null;
            document.Dispose();
            (context as IDisposable)?.Dispose();

            throw new NavigationFailedException(
                _url,
                "The document has more than the "
                + maxNodes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " nodes BrowserOptions.MaxDomNodes allows.");
        }

        if (_baton.ParserHopped)
        {
            Report(
                PageErrorKind.ScriptError,
                "AngleSharp resumed the parse on another thread. The baton kept the DOM to one holder, but a "
                + "step of the parse suspended where this driver expected none: see Jint.Browser/Runtime/AGENTS.md, "
                + "'the parser driver'.",
                _url);
        }

        onPhase?.Invoke(NavigationPhase.Committed);
        FinishLoad(document, onPhase);
        return new PageLoad(document, context, ScriptsRun);
    }

    /// <summary>
    /// Whether the tree rooted at <paramref name="node"/> holds more than <paramref name="limit"/> nodes.
    /// </summary>
    /// <remarks>
    /// An explicit stack rather than recursion, because the depth is the document's and a document is
    /// something a stranger wrote; and it stops at the first node past the limit rather than counting a tree
    /// whose whole point is to be too large.
    /// </remarks>
    private static bool Exceeds(INode node, int limit)
    {
        var pending = new Stack<INode>();
        pending.Push(node);
        var seen = 0;

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (++seen > limit)
            {
                return true;
            }

            foreach (var child in current.ChildNodes)
            {
                pending.Push(child);
            }
        }

        return false;
    }

    /// <summary>
    /// Runs AngleSharp's parse on a thread of its own and serves it from the loop until it is finished.
    /// </summary>
    private IDocument Parse(IBrowsingContext context, string html)
    {
        var url = _url;
        IDocument? document = null;
        Exception? failure = null;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                // The content type is stated rather than left to AngleSharp, which otherwise guesses one from
                // the address: a document fetched from `/notes.txt` would be given AngleSharp's plain-text
                // document factory and the markup below — already the plain-text wrapper the navigate rules
                // asked for — would end up inside a second <pre>.
                document = context
                    .OpenAsync(response => response
                        .Content(html)
                        .Address(url)
                        .Header(HeaderNames.ContentType, "text/html; charset=utf-8"))
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                completion.TrySetResult();
            }
        })
        {
            IsBackground = true,
            Name = "Jint.Browser page parser",
        };

        thread.Start();

        if (!_baton.Serve(completion.Task))
        {
            // The page is closing. The parser thread is a background one and its next hand-off fails, so
            // there is nothing left to wait for and nothing to show.
            throw new OperationCanceledException("The page was closed while its document was being parsed.");
        }

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return document!;
    }

    // ---------------------------------------------------------------------------------------------------
    // What the parser asks for. Every one of these is called on the parser thread (or, for a script a
    // running script inserted, on the loop itself) and answers only once the work is finished.
    // ---------------------------------------------------------------------------------------------------

    /// <summary>Fetches an external classic script's source, or answers <see langword="null"/> if it must not run.</summary>
    internal IResponse? FetchScript(IHtmlScriptElement script, string url)
    {
        var handedOver = HandsOver;

        return Serve(() =>
        {
            if (script.Owner is { } owner && IsFrameDocument(owner))
            {
                RefuseFrameScript(url);
                return null;
            }

            _runtime.Document ??= script.Owner;

            // https://html.spec.whatwg.org/multipage/scripting.html#prepare-the-script-element step 12: a
            // classic script carrying `nomodule` is not run — and not fetched — by anything that supports
            // module scripts, which this does.
            if (script.HasAttribute("nomodule"))
            {
                return null;
            }

            return Fetch(url, script, "script", PageRequestKind.Script, handedOver);
        });
    }

    /// <summary>Fetches an external style sheet so that AngleSharp.Css can parse it into the document.</summary>
    /// <remarks>
    /// A frame's style sheet is fetched like the page's own: the cascade is what
    /// <c>getComputedStyle</c> and the box model read, and neither needs a realm. Only a script does.
    /// </remarks>
    internal IResponse? FetchStyleSheet(IHtmlLinkElement link, string url)
    {
        var handedOver = HandsOver;

        return Serve(() =>
        {
            if (link.Owner is { } owner && !IsFrameDocument(owner))
            {
                _runtime.Document ??= owner;
            }

            return Fetch(url, link, "stylesheet", PageRequestKind.Stylesheet, handedOver);
        });
    }

    /// <summary>Records a script a frame's document asked for and this browser will not run.</summary>
    private void RefuseFrameScript(string url)
        => _requests.RecordNotFetched(
            url,
            RequestInitiator.Subresource,
            PageRequestKind.Script,
            "a script in a child frame's document is not run: a frame has a document here and no realm of its own");

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/iframe-embed-object.html#process-the-iframe-attributes — the
    /// document a child frame's <c>src</c> names, fetched so that AngleSharp can open it into the nested
    /// browsing context it has already made for the element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The nested context is AngleSharp's, not this driver's.</b> <c>HtmlFrameElementBase.SetupElement</c>
    /// creates a child context for every frame element it builds and asks the resource loader for the
    /// document to put in it; until this method existed that request was refused, so <c>ContentDocument</c>
    /// stayed <see langword="null"/> and <c>load</c> never arrived
    /// (<a href="https://github.com/sebastienros/jint/issues/3771">#3771</a>). Answering it is the whole of
    /// what gives a frame a document.
    /// </para>
    /// <para>
    /// <b>The child document runs no script</b> — see <see cref="IsFrameDocument"/>. The child context
    /// inherits this page's services, so refusing there rather than here is what keeps a frame's
    /// <c>&lt;script&gt;</c> out of the page's own realm, which is the one realm there is.
    /// </para>
    /// <para>
    /// <b>The ceiling is <see cref="BrowserOptions.MaxFrameDocuments"/></b>, counted over the whole load
    /// rather than per document, because a frame's document may hold frames of its own: a page pointing a
    /// frame at itself would otherwise recurse until the parser thread's stack ran out. Over the ceiling a
    /// frame is recorded as not fetched, exactly as every frame was before.
    /// </para>
    /// <para>
    /// <b><c>about:blank</c> is answered here rather than fetched.</b> It is the commonest frame source
    /// there is and no network position can answer it: HTML says it is an empty HTML document, so that is
    /// what is handed back, without a socket and without a row in <see cref="Page.Requests"/> — a page that
    /// asked for nothing made no request.
    /// </para>
    /// </remarks>
    internal IResponse? FetchFrame(IHtmlInlineFrameElement frame, string url)
    {
        var handedOver = HandsOver;

        return Serve(() =>
        {
            var ceiling = _runtime.Options.MaxFrameDocuments;

            if (ceiling <= 0 || _frameDocuments >= ceiling)
            {
                _requests.RecordNotFetched(
                    url,
                    RequestInitiator.Subresource,
                    PageRequestKind.Frame,
                    ceiling <= 0
                        ? "a frame's document is not fetched: BrowserOptions.MaxFrameDocuments is zero"
                        : "a frame's document is not fetched: this page has already reached the "
                            + ceiling.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " frame documents BrowserOptions.MaxFrameDocuments allows");
                return null;
            }

            _frameDocuments++;

            // https://html.spec.whatwg.org/multipage/urls-and-fetching.html#about:blank — "a resource whose
            // representation is the empty byte sequence, parsed as HTML".
            if (string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return PageResourceLoader.Answer(url, [], "text/html; charset=utf-8");
            }

            return Fetch(url, frame, "frame document", PageRequestKind.Frame, handedOver);
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
    private bool IsFrameDocument(IDocument document)
        => _context is not null && !ReferenceEquals(document.Context, _context);

    /// <summary>
    /// Whether a call arriving now would cross from the parser thread to the loop — as opposed to already
    /// being on the loop, which is where a script that inserted a script element is.
    /// </summary>
    private bool HandsOver => Environment.CurrentManagedThreadId != _baton.LoopThreadId;

    /// <summary>
    /// Records a reference the page will not follow, and answers the empty response that says so.
    /// </summary>
    internal IResponse? RefuseSubresource(IElement source, string url)
    {
        _requests.RecordNotFetched(url, RequestInitiator.Subresource, KindNotFetched(source), ReasonNotFetched(source));
        return null;
    }

    /// <summary>Runs one classic script, on the page loop, and fires what HTML says it fires.</summary>
    internal void RunClassicScript(IResponse response, ScriptOptions options)
    {
        if (options.Element is not { } element)
        {
            return;
        }

        Serve<object?>(() =>
        {
            // https://html.spec.whatwg.org/multipage/webappapis.html#concept-environment-noscript — a frame's
            // document has no realm of its own here, so its scripts do not run at all rather than running in
            // the page's. Both halves of a script arrive: an external one is refused at the fetch above, so
            // that the reference it names is in the request log, and an inline one here, because AngleSharp
            // prepares an inline script with no download and there is no reference to record.
            if (IsFrameDocument(options.Document))
            {
                return null;
            }

            // The document exists from the first token, but this is the earliest AngleSharp hands it over,
            // and a script running during the parse needs `document` to answer before the parse has finished.
            _runtime.Document ??= options.Document;

            // AngleSharp advances its own readiness before it runs the deferred queue, which is the one
            // moment this driver cannot observe from outside the parse — so it is read here, on the way in.
            ObserveReadiness(options.Document);

            // A script boundary is one of the two moments a parser-created custom element can become
            // custom (the other is the end of the parse): AngleSharp creates a parser element with no
            // notification to hook, so an element written in the markup before this script is upgraded
            // here, which is where a browser would already have constructed it. It costs nothing for a
            // document that has defined nothing.
            _runtime.CustomElementsIfCreated?.UpgradeParsedElements();

            // And the import map, for the same reason: a classic script's dynamic import() runs during the
            // parse, so the map has to be in force by then rather than only when the modules run.
            ReadImportMapEarly(options.Document);
            Execute(response, element);
            return null;
        });
    }

    /// <summary>
    /// Hands <paramref name="work"/> to the loop, or runs it here when this <i>is</i> the loop — which it is
    /// for a script a running script inserted, whose whole chain happens inside a job the loop is running.
    /// </summary>
    private T Serve<T>(Func<T> work) => HandsOver ? _baton.RunOnLoop(work) : work();

    /// <summary>
    /// The fetch itself, on the loop. Pumping while it is in flight is what makes a page's timers fire
    /// during a parser-blocking load; a fetch a <i>script</i> triggered blocks instead, because pumping from
    /// inside a running script would run the page's jobs in the middle of one.
    /// </summary>
    private IResponse? Fetch(string url, IElement source, string what, PageRequestKind kind, bool mayPump)
    {
        var target = UrlParser.Parse(url);

        if (target is null || !PageUrl.IsNetworkScheme(target))
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
            return PageResourceLoader.Answer(fetched.Url, fetched.Bytes, fetched.ContentType);
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
    /// https://html.spec.whatwg.org/multipage/webappapis.html — a resource that failed to load fires
    /// <c>error</c> at the element that asked for it, and the page carries on loading.
    /// </summary>
    private void FailSubresource(IElement source, string url, string message)
    {
        Report(PageErrorKind.ReportedError, message, url);
        FireAt(source, "error");
    }

    /// <summary>Dispatches a simple event at an element through Jint's dispatcher.</summary>
    /// <remarks>
    /// AngleSharp fires its own <c>load</c> and <c>error</c> into its own listener lists, which hold nothing
    /// a script registered — so the one a page can hear is this one. Neither bubbles, which is HTML's rule
    /// for a resource event.
    /// </remarks>
    private void FireAt(INode node, string type)
    {
        if (_runtime.Dom.WrapNode(node) is { } wrapper)
        {
            PageEvents.Fire(_runtime, wrapper, type);
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // Script execution.
    // ---------------------------------------------------------------------------------------------------

    private void Execute(IResponse response, IHtmlScriptElement element)
    {
        var external = !string.IsNullOrEmpty(element.Source);

        if (element.HasAttribute("nomodule"))
        {
            return;
        }

        string text;
        string source;
        string location;
        var line = 1;

        if (external)
        {
            text = Read(response, element);
            source = response.Address?.Href ?? element.Source!;
            location = source;
        }
        else
        {
            // https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception: an inline script's
            // *filename* is the document's URL, so that is what the engine is given as the source name, and
            // the line the script starts on is a parsing offset rather than part of the name. The page's own
            // error recorder still gets the `url:line` string it always did, which is the one a host reads.
            text = element.Text ?? "";
            source = _url;
            line = LineOf(element, text);
            location = _url + ":" + line;
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
        if (element.Owner is { } owner)
        {
            Events.EventHandlerContentAttributes.InstallBodyHandlers(_runtime.Dom, owner);
        }

        var previous = _runtime.CurrentScript;

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
    private static string Read(IResponse response, IHtmlScriptElement element)
    {
        var bytes = ReadAll(response.Content);
        var contentType = response.Headers.TryGetValue(HeaderNames.ContentType, out var declared) ? declared : null;
        var fallback = element.CharacterSet is { Length: > 0 } charset ? charset : element.Owner?.CharacterSet;
        return new FetchedSubresource(bytes, contentType, response.Address?.Href ?? "", 200).Text(fallback);
    }

    private static byte[] ReadAll(Stream? stream)
    {
        if (stream is null)
        {
            return [];
        }

        if (stream is MemoryStream memory)
        {
            return memory.ToArray();
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    // ---------------------------------------------------------------------------------------------------
    // After the parse: modules, then the lifecycle.
    // ---------------------------------------------------------------------------------------------------

    private void FinishLoad(IDocument document, Action<NavigationPhase>? onPhase)
    {
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
    private void FireFrameLoads(IDocument document)
    {
        foreach (var element in document.QuerySelectorAll("iframe, frame"))
        {
            if (element is not IHtmlInlineFrameElement { ContentDocument: { } nested })
            {
                continue;
            }

            FireFrameLoads(nested);
            FireAt(element, "load");
        }
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
    /// Called from the scripting service before a script AngleSharp queued runs, so that a deferred script
    /// sees the readiness HTML says it sees.
    /// </summary>
    internal void ObserveReadiness(IDocument document)
    {
        if (document.ReadyState != DocumentReadyState.Loading)
        {
            SetReadyState("interactive");
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#integration-with-the-javascript-module-system —
    /// the document's import map, then every module script in document order.
    /// </summary>
    private void RunModules(IDocument document)
    {
        var loader = _runtime.Modules;
        if (loader is null)
        {
            return;
        }

        loader.BaseUrl = BaseUrlOf(document);

        var modules = new List<IHtmlScriptElement>();
        var mapSeen = _importMapRead;

        foreach (var element in document.QuerySelectorAll("script"))
        {
            if (element is not IHtmlScriptElement script)
            {
                continue;
            }

            var type = script.Type ?? "";

            if (string.Equals(type, "importmap", StringComparison.OrdinalIgnoreCase))
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

            if (string.Equals(type, "module", StringComparison.OrdinalIgnoreCase))
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
    private void ReadImportMapEarly(IDocument document)
    {
        if (_importMapRead || _runtime.Modules is not { } loader)
        {
            return;
        }

        if (document.QuerySelector("script[type='importmap']") is not IHtmlScriptElement script)
        {
            return;
        }

        _importMapRead = true;
        _importMapElement = script;
        loader.Map = ReadImportMap(script, loader.BaseUrl);
    }

    private ImportMap? ReadImportMap(IHtmlScriptElement script, string baseUrl)
    {
        if (!string.IsNullOrEmpty(script.Source))
        {
            // https://html.spec.whatwg.org/multipage/webappapis.html#import-map-processing-model: an import
            // map is inline text, and an external one is a parse error rather than a fetch.
            Report(PageErrorKind.ReportedError, "An import map cannot have a src attribute; this one was ignored.", _url);
            return null;
        }

        var problems = new List<string>();
        var map = ImportMap.Parse(script.Text ?? "", baseUrl, problems);

        foreach (var problem in problems)
        {
            Report(PageErrorKind.ReportedError, problem, _url);
        }

        return map;
    }

    private void RunModule(PageModuleScriptLoader loader, IHtmlScriptElement script)
    {
        string specifier;

        if (script.Source is { Length: > 0 } source)
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
            var text = script.Text ?? "";

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

    private static string BaseUrlOf(IDocument document)
        => document.BaseUri ?? document.Url;

    /// <summary>What kind of resource a reference the page will not follow was, for the request log.</summary>
    /// <remarks>
    /// A frame is deliberately not here any more: <see cref="PageResourceLoader"/> routes one to
    /// <see cref="FetchFrame"/>, which records its own refusal with the reason it has and this one has not.
    /// </remarks>
    private static PageRequestKind KindNotFetched(IElement source) => source switch
    {
        IHtmlImageElement => PageRequestKind.Image,
        _ when IsLegacyFrame(source) => PageRequestKind.Frame,
        _ => PageRequestKind.Other,
    };

    private static string ReasonNotFetched(IElement source) => source switch
    {
        IHtmlImageElement => "images are not fetched: there is no rendering to need them",
        IHtmlLinkElement link => "a <link rel=\"" + (link.Relation ?? "") + "\"> is not fetched: only a stylesheet is",
        _ when IsLegacyFrame(source) => "a <frame>'s document is not fetched: AngleSharp has no HTMLFrameElement "
            + "interface, so nothing script can reach would answer it",
        _ => source.LocalName + " resources are not fetched: there is no rendering to need them",
    };

    /// <summary>Whether the element is a <c>&lt;frame&gt;</c> inside a <c>&lt;frameset&gt;</c>.</summary>
    /// <remarks>
    /// It asks AngleSharp for its document exactly as an <c>&lt;iframe&gt;</c> does, and it is refused where
    /// an <c>&lt;iframe&gt;</c> is answered: AngleSharp declares no <c>IHtmlFrameElement</c>, so the binding
    /// projects no <c>HTMLFrameElement</c> and there is no <c>contentDocument</c> for a page to read the
    /// document through. Fetching it would be traffic whose result nothing could reach. The local name is
    /// what names it, for the same reason <c>WindowNamedProperties.IsNamedAccessKind</c> uses one.
    /// </remarks>
    private static bool IsLegacyFrame(IElement source)
        => source is IHtmlElement && string.Equals(source.LocalName, "frame", StringComparison.Ordinal);

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
                _runtime.Engine._webApi?.FireGlobalErrorEvent(exception);
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

    /// <summary>The one-based line an inline script's first character is on.</summary>
    /// <remarks>
    /// AngleSharp records a source position only when the parser is asked to keep source references, which
    /// the default parser is not; what it does expose is the parser's index into the document source, and
    /// this call happens with that index just past the closing tag. Counting back over the script's own
    /// newlines from there is exact for a document nothing has written into, and approximate afterwards —
    /// <c>document.write</c> moves every later index by what it inserted.
    /// </remarks>
    private static int LineOf(IHtmlScriptElement element, string text)
    {
        var source = element.Owner?.Source;
        var index = source?.Index ?? 0;

        if (index <= 0)
        {
            return 1;
        }

        var full = source!.Text;
        if (index > full.Length)
        {
            return 1;
        }

        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (full[i] == '\n')
            {
                line++;
            }
        }

        foreach (var c in text)
        {
            if (c == '\n')
            {
                line--;
            }
        }

        return line < 1 ? 1 : line;
    }
}
