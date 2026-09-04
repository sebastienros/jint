using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text;
using AngleSharp.Html.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser;

/// <summary>
/// The navigation half of a page: a fetch off the loop, a new engine on it, and the session history that
/// spans both.
/// </summary>
public sealed partial class Page
{
    /// <summary>
    /// One navigation at a time per page. A second one — a script assigning <c>location</c> while a host's
    /// call is in flight — waits rather than racing the engine swap.
    /// </summary>
    private readonly SemaphoreSlim _navigationGate = new(1, 1);

    private readonly List<TaskCompletionSource<bool>> _navigationWaiters = [];
    private readonly System.Threading.Lock _waitersGate = new();

    /// <summary>
    /// Set while <see cref="SubmitFormAsync"/> runs, so that the navigation the submission produces is
    /// handed back to the caller to await instead of being started and forgotten. Loop thread only.
    /// </summary>
    private NavigationRequest? _capturedNavigation;
    private bool _capturingNavigation;

    /// <summary>How many documents this page has begun loading, which is what a loader identifier counts.</summary>
    private int _loaderSerial;

    /// <summary>
    /// Whether the document showing is still the <c>about:blank</c> the page opened on, which the first
    /// navigation replaces rather than pushes past. Loop thread only.
    /// </summary>
    private bool _isInitialAboutBlank = true;

    /// <summary>Loads <paramref name="url"/>, replacing the document and the engine behind it.</summary>
    /// <param name="url">The URL to load: <c>http</c>, <c>https</c>, <c>about:</c> or <c>data:</c>.</param>
    /// <param name="options">How far to wait and how long to allow; the defaults when omitted.</param>
    /// <returns>
    /// The response the document came from, or <see langword="null"/> for a URL that reached no network.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>A status is not a failure.</b> A <c>404</c> or a <c>500</c> navigates and its body becomes the
    /// document, which is what a browser does and what a caller scraping an error page needs.
    /// <see cref="NavigationFailedException"/> means there was nothing to show: a refused URL, a transport
    /// failure, a content type a page cannot render, or the ceiling in
    /// <see cref="NavigationOptions.Timeout"/>.
    /// </para>
    /// <para>
    /// <b>The document fetch happens off the page's thread</b>, so the page goes on pumping its timers and
    /// answering calls while the response is on its way — and closing the page while one is in flight ends
    /// this call with <see cref="OperationCanceledException"/>.
    /// </para>
    /// <para>
    /// <b>The previous document is unloaded first.</b> It receives <c>beforeunload</c>, <c>pagehide</c> and
    /// <c>unload</c>, its cancellation token is cancelled so everything it had in flight is abandoned, and
    /// its engine is disposed on the page's thread. Every value that engine made — every wrapper, every
    /// expando a script left on one — goes with it.
    /// </para>
    /// <para>
    /// <b>Except when nothing but the fragment changes.</b> A URL equal to the current one but for its
    /// fragment, and carrying a fragment of its own, is a fragment navigation: the document, the engine and
    /// everything on them stay, a history entry is added, and <c>hashchange</c> fires. Navigating to the same
    /// URL <i>without</i> a fragment is a reload, which is HTML's own rule and the difference a router
    /// depends on.
    /// </para>
    /// </remarks>
    /// <exception cref="NavigationFailedException">There was no document to show.</exception>
    /// <exception cref="OperationCanceledException">The page was closed while the navigation ran.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<PageResponse?> NavigateAsync(string url, NavigationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        ObjectDisposedException.ThrowIf(_closed, this);

        var settings = options ?? NavigationOptions.Default;
        return NavigateCoreAsync(new NavigationRequest(
            url,
            settings,
            HistoryMode.Push,
            TraversalIndex: -1,
            Body: null,
            ContentType: null,
            Reload: false,
            Referrer: settings.Referrer));
    }

    /// <summary>
    /// Submits the form <paramref name="selector"/> names, running HTML's form submission algorithm.
    /// </summary>
    /// <param name="selector">A CSS selector; the first matching <c>&lt;form&gt;</c> is submitted.</param>
    /// <param name="options">How far to wait for the navigation it produces.</param>
    /// <returns>
    /// The response the submission navigated to, or <see langword="null"/> when it produced no navigation —
    /// because no form matched, because a <c>submit</c> listener cancelled it, or because the form failed
    /// validation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The whole algorithm runs: the <c>submit</c> event a listener may cancel, the entry list with its
    /// disabled, unchecked and multiple-select rules, the <c>formdata</c> event a listener may amend, then a
    /// <c>GET</c> with a query string or a <c>POST</c> in the form's <c>enctype</c>.
    /// </para>
    /// <para>
    /// It takes a selector rather than an element because an AngleSharp node belongs to the page's own
    /// thread: no member of this type hands one out, and one that took a node would be an invitation to
    /// touch the DOM from the caller's thread. A click on a submit button is the input model's business
    /// (campaign item R2), which reaches the same algorithm from inside the loop.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task<PageResponse?> SubmitFormAsync(string selector, NavigationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ObjectDisposedException.ThrowIf(_closed, this);

        var captured = await _loop.PostAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine);
            if (runtime?.Document?.QuerySelector(selector) is not IHtmlFormElement form)
            {
                return null;
            }

            _capturingNavigation = true;
            _capturedNavigation = null;

            try
            {
                Events.FormSubmission.Submit(runtime.Dom, form, submitter: null);
                return _capturedNavigation;
            }
            finally
            {
                _capturingNavigation = false;
                _capturedNavigation = null;
            }
        }).ConfigureAwait(false);

        if (captured is null)
        {
            return null;
        }

        return await NavigateCoreAsync(captured with { Options = options ?? NavigationOptions.Default }).ConfigureAwait(false);
    }

    /// <summary>Waits for the page's next navigation to commit.</summary>
    /// <param name="timeout">The ceiling on the wait.</param>
    /// <returns>
    /// <see langword="true"/> when a navigation committed, <see langword="false"/> when the timeout won or
    /// the page closed first.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is what waits for a navigation a <i>script</i> started — a form's <c>submit()</c>, an assignment
    /// to <c>location</c>, a <c>history.back()</c> that crosses documents — because nothing returned that
    /// navigation a task to await. It counts a same-document commit too: a <c>pushState</c> and a fragment
    /// navigation both satisfy it, which is what makes it usable against a client-side router.
    /// </para>
    /// <para>
    /// <b>Start the wait before the thing that triggers it.</b> It waits for the <i>next</i> commit, and a
    /// navigation a script starts runs off the page's own thread — so one begun after the trigger can miss a
    /// commit that has already happened and time out instead:
    /// </para>
    /// <code>
    /// var navigated = page.WaitForNavigationAsync(TimeSpan.FromSeconds(10));
    /// await page.EvaluateAsync("document.forms[0].submit()");
    /// await navigated;
    /// </code>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task<bool> WaitForNavigationAsync(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_closed, this);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_waitersGate)
        {
            _navigationWaiters.Add(completion);
        }

        using var cancellation = new CancellationTokenSource(timeout);
        using var registration = cancellation.Token.Register(static state => ((TaskCompletionSource<bool>) state!).TrySetResult(false), completion);

        var committed = await completion.Task.ConfigureAwait(false);

        lock (_waitersGate)
        {
            _navigationWaiters.Remove(completion);
        }

        return committed;
    }

    /// <summary>
    /// The navigation seam a page's own <c>location</c> assignment, link or form reaches.
    /// </summary>
    /// <remarks>
    /// Called on the page loop, from inside the script that asked. The navigation itself is deliberately not
    /// awaited — the document that script is running in is the one being replaced — and a failure becomes a
    /// page error rather than an unobserved faulted task, because there is nobody to hand it to.
    /// </remarks>
    internal void RequestNavigation(
        string url,
        bool replace,
        bool reload = false,
        Engine? engine = null,
        PageNavigationReason? reason = PageNavigationReason.ScriptInitiated)
    {
        if (engine is not null && !reload && TryFragmentNavigation(engine, url, replace))
        {
            return;
        }

        Start(new NavigationRequest(
            url,
            NavigationOptions.Default,
            replace ? HistoryMode.Replace : HistoryMode.Push,
            TraversalIndex: -1,
            Body: null,
            ContentType: null,
            reload,
            Referrer: null),
            reload && reason is not null ? PageNavigationReason.Reload : reason);
    }

    /// <summary>
    /// A same-document fragment navigation, done <b>on the loop</b> rather than through <see cref="Start"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// HTML's <i>navigate</i> queues a task and its fragment arm neither fetches, replaces the engine nor can
    /// fail, so there is nothing for the off-loop half of a navigation to do — and going there anyway is
    /// observable: a page that clicks an <c>&lt;a href="#x"&gt;</c> and then spins zero-delay timers waiting
    /// for <c>hashchange</c> waits out the whole chain, because the thread hop and the navigation gate put
    /// the commit behind every timer already due.
    /// <c>dom/events/Event-dispatch-single-activation-behavior.html</c> is written exactly that way and gives
    /// it two turns.
    /// </para>
    /// <para>
    /// <b>The one question asked is whose document is moving</b>, and it is answered by the engine: HTML's
    /// <i>navigate to a fragment</i> is a task on the event loop of the document whose URL it is changing, so
    /// the request is a same-document move exactly when it came from the document the page is showing.
    /// Neither the page's own load having returned nor the navigation gate being free is that question —
    /// <see cref="_load"/> is null for the whole of a document's parse and the gate is held by the very
    /// navigation that produced it, so a script running during its own document's parse was refused the
    /// fragment arm and had a whole navigation queued behind the gate instead. It landed after the parse, out
    /// of order with everything queued after it, and the corpus file above never saw the <c>hashchange</c>.
    /// </para>
    /// <para>
    /// A commit already on its way is ordered by the queue rather than by a refusal: a job posted here runs
    /// before it if it was posted first, and dies with the engine it was posted to if the commit wins —
    /// which is a document that went away, exactly as a browser drops a fragment move onto a replaced
    /// document.
    /// </para>
    /// </remarks>
    private bool TryFragmentNavigation(Engine engine, string url, bool replace)
    {
        if (_closed || !ReferenceEquals(engine, _loop.CurrentEngine))
        {
            return false;
        }

        var target = PageUrl.Parse(url, _url);
        if (target?.Fragment is null)
        {
            return false;
        }

        var href = target.Serialize();
        if (!PageUrl.IsSameDocument(_url, href))
        {
            return false;
        }

        var history = replace ? HistoryMode.Replace : HistoryMode.Push;
        engine.Tasks.Post(() => FragmentNavigate(engine, href, history));
        return true;
    }

    /// <summary>The same, for a form submission that ends in a <c>POST</c>.</summary>
    internal void RequestFormPost(string url, byte[] body, string contentType)
        => Start(new NavigationRequest(
            url,
            NavigationOptions.Default,
            HistoryMode.Push,
            TraversalIndex: -1,
            Body: body,
            ContentType: contentType,
            Reload: true,
            Referrer: null),
            PageNavigationReason.FormSubmissionPost);

    /// <summary>
    /// <c>history.back()</c>, <c>forward()</c> and <c>go()</c>: queue a traversal, on the page loop.
    /// </summary>
    /// <remarks>
    /// A traversal is always asynchronous, which is HTML's own model — a script reading <c>location.href</c>
    /// on the line after <c>history.back()</c> sees the old URL. A same-document traversal is queued as a job
    /// on the engine's own loop; one that crosses documents is a navigation like any other.
    /// </remarks>
    internal void RequestTraversal(int delta, bool rendererInitiated = false)
    {
        if (_closed)
        {
            return;
        }

        if (delta == 0)
        {
            // https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-history-go: go(0) reloads.
            RequestNavigation(
                _url,
                replace: true,
                reload: true,
                reason: rendererInitiated ? PageNavigationReason.Reload : null);
            return;
        }

        if (_history.Peek(delta, out var index) is not { } entry)
        {
            // Nothing that far back or forward; HTML says to do nothing at all.
            return;
        }

        if (entry.DocumentId == _history.CurrentDocumentId)
        {
            var runtime = PageRuntime.Find(_loop.CurrentEngine!);
            if (runtime is null)
            {
                return;
            }

            runtime.Engine.Tasks.Post(() => TraverseSameDocument(runtime, index));
            return;
        }

        Start(new NavigationRequest(
            entry.Url,
            NavigationOptions.Default,
            HistoryMode.Traverse,
            index,
            Body: null,
            ContentType: null,
            Reload: true,
            Referrer: null),
            rendererInitiated ? PageNavigationReason.ScriptInitiated : null);
    }

    /// <summary>
    /// Moves the page's URL without touching the document — what <c>pushState</c> and <c>replaceState</c>
    /// do once the history entry is in place. On the loop.
    /// </summary>
    internal void CommitSameDocumentUrl(PageRuntime runtime, string url)
    {
        _url = url;
        runtime.DocumentUrl = url;
        SignalNavigation();
        _observer?.SameDocumentNavigated(url, _loaderId);
    }

    /// <summary>Starts a navigation nobody is waiting for, and turns its failure into a page error.</summary>
    private void Start(NavigationRequest request, PageNavigationReason? reason)
    {
        if (_closed)
        {
            return;
        }

        if (reason is { } requestedReason)
        {
            var target = PageUrl.Parse(request.Target, _url);
            if (target is null)
            {
                RejectBeforeStart(
                    request,
                    new NavigationFailedException(
                        request.Target,
                        "'" + request.Target + "' cannot be parsed as a URL."));
                return;
            }

            // Freeze the URL at request time: this navigation may wait behind one that changes the base URL.
            var initiatorUrl = _url;
            var href = target.Serialize();
            request = request with
            {
                Target = href,
                Referrer = request.Referrer ?? ReferrerFor(initiatorUrl),
                InitiatorUrl = initiatorUrl,
            };

            var allowed = true;
            if (PageUrl.IsNetworkScheme(target))
            {
                var uri = PageUrl.ToUri(target);
                try
                {
                    allowed = uri is not null && _network.UrlFilter(uri);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
                {
                    RejectBeforeStart(request, exception);
                    return;
                }

                request = request with { FirstHopAllowed = allowed };
            }
            else
            {
                try
                {
                    request = request with { InlineContent = ContentOf(href) };
                }
                catch (NavigationFailedException failure)
                {
                    RejectBeforeStart(request, failure);
                    return;
                }
            }

            if (allowed)
            {
                _observer?.NavigationRequested(href, requestedReason);
            }
        }

        if (_capturingNavigation)
        {
            // SubmitFormAsync is running the algorithm and wants the navigation back rather than started.
            _capturedNavigation = request;
            return;
        }

        // Deliberately off the loop: the caller is a script the current document is running, and the document
        // it asked for replaces the engine that script is in.
        _ = Task.Run(async () =>
        {
            try
            {
                await NavigateCoreAsync(request).ConfigureAwait(false);
            }
            catch (NavigationFailedException failure)
            {
                _recorder.Add(PageErrorKind.ReportedError, failure.Message, "Navigation");
            }
            catch (OperationCanceledException)
            {
                // The page closed while the navigation was in flight; there is nobody left to tell.
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                _recorder.Add(PageErrorKind.ReportedError, exception.Message, "Navigation");
            }
        });
    }

    private void RejectBeforeStart(NavigationRequest request, Exception failure)
    {
        if (_capturingNavigation)
        {
            _capturedNavigation = request with { PreflightFailure = failure };
            return;
        }

        _recorder.Add(new PageError(PageErrorKind.ReportedError, failure.Message, "Navigation"));
    }

    /// <summary>The commit half of <see cref="SetContentAsync"/>, under the navigation gate.</summary>
    private async Task SetContentCoreAsync(string url, string html)
    {
        await _navigationGate.WaitAsync(_loop.Closing).ConfigureAwait(false);

        try
        {
            var loaderId = NextLoaderId();
            _observer?.NavigationStarted(url, loaderId);

            if (_load is not null)
            {
                // beforeunload fires and is never honoured: a host replacing the content means it, and there
                // is no NavigationOptions here to say otherwise.
                await _loop.PostAsync(FireBeforeUnload).ConfigureAwait(false);
            }

            await _loop.PostAsync(engine => Commit(
                engine,
                new CommitRequest(url, html, Response: null, HistoryMode.Push, TraversalIndex: -1, Referrer: ReferrerFor(_url), OnPhase: null, LoaderId: loaderId))).ConfigureAwait(false);
        }
        finally
        {
            _navigationGate.Release();
        }
    }

    private async Task<PageResponse?> NavigateCoreAsync(NavigationRequest request)
    {
        if (request.PreflightFailure is { } failure)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        using var timeout = new CancellationTokenSource(request.Options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, _loop.Closing);

        try
        {
            await _navigationGate.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new NavigationFailedException(request.Target, "Navigation to '" + request.Target + "' timed out waiting for the previous one.");
        }

        try
        {
            return await RunAsync(request, timeout, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _navigationGate.Release();
        }
    }

    private async Task<PageResponse?> RunAsync(NavigationRequest request, CancellationTokenSource timeout, CancellationToken cancellationToken)
    {
        var current = _url;
        var initiatorUrl = request.InitiatorUrl ?? current;
        var target = PageUrl.Parse(request.Target, current);

        if (target is null)
        {
            throw new NavigationFailedException(request.Target, "'" + request.Target + "' cannot be parsed as a URL.");
        }

        var href = target.Serialize();

        // https://html.spec.whatwg.org/multipage/browsing-the-web.html#navigate step 3: a URL equal to the
        // current one with fragments excluded, and whose own fragment is non-null, keeps the document, the
        // engine and everything on it. The fragment being non-null is what makes navigating to the same URL
        // without one a reload rather than a no-op — and `location.reload()` says so outright, because the
        // URL it re-navigates to may well carry a fragment already.
        if (!request.Reload && _load is not null && target.Fragment is not null && PageUrl.IsSameDocument(current, href))
        {
            await _loop.PostAsync(engine => FragmentNavigate(engine, href, request.History)).ConfigureAwait(false);
            return _response;
        }

        // Minted here rather than at the commit, because a client is told a navigation started before
        // anything has been fetched and every signal of the document it produces has to carry the same value.
        var loaderId = NextLoaderId();
        _observer?.NavigationStarted(href, loaderId);

        if (_load is not null)
        {
            var stay = await _loop.PostAsync(FireBeforeUnload).ConfigureAwait(false);
            if (stay && request.Options.AllowCancel)
            {
                throw new NavigationFailedException(href, "Navigation to '" + href + "' was cancelled by the page's beforeunload handler.");
            }
        }

        string html;
        string finalUrl;
        PageResponse? response = null;

        if (PageUrl.IsNetworkScheme(target))
        {
            var fetched = await FetchDocumentAsync(target, request, initiatorUrl, loaderId, timeout, cancellationToken).ConfigureAwait(false);
            html = fetched.Html;

            // https://html.spec.whatwg.org/multipage/browsing-the-web.html#create-navigation-params-by-fetching:
            // the document's URL is the response's, and its fragment is the request's when the response
            // carried none — which is what makes navigating to `page#section` leave `location.hash` set.
            finalUrl = WithFragmentOf(fetched.Url, target);
            response = fetched.Response;
        }
        else
        {
            html = request.InlineContent ?? ContentOf(href);
            finalUrl = href;
        }

        var signals = new NavigationSignals();
        var referrer = request.Referrer ?? ReferrerFor(initiatorUrl);

        var commit = _loop.PostAsync(engine => Commit(
            engine,
            new CommitRequest(finalUrl, html, response, request.History, request.TraversalIndex, referrer, signals.Reached, loaderId)));

        // The signal for the requested phase, so that WaitUntil.Commit really does answer before the load
        // events have run. A commit that fails before its phase arrives wins the race and throws.
        var wanted = signals.For(request.Options.WaitUntil);
        var finished = await Task.WhenAny(commit, wanted).ConfigureAwait(false);

        if (ReferenceEquals(finished, commit))
        {
            await commit.ConfigureAwait(false);
        }
        else
        {
            // The rest of the load runs on; its failure would otherwise be an unobserved faulted task.
            _ = commit.ContinueWith(
                static (task, state) =>
                {
                    if (task.Exception is { } exception)
                    {
                        ((Page) state!)._recorder.Add(PageErrorKind.ReportedError, exception.GetBaseException().Message, "Navigation");
                    }
                },
                this,
                TaskScheduler.Default);
        }

        return response;
    }

    private async Task<FetchedDocument> FetchDocumentAsync(
        UrlRecord target,
        NavigationRequest request,
        string initiatorUrl,
        string loaderId,
        CancellationTokenSource timeout,
        CancellationToken cancellationToken)
    {
        // The first hop's filter is run here rather than in the transport, which deliberately does not run it
        // twice: a host filter being called once per request is observable to the host.
        var uri = PageUrl.ToUri(target);
        if (uri is null || request.FirstHopAllowed == false || (request.FirstHopAllowed is null && !_network.UrlFilter(uri)))
        {
            throw new NavigationFailedException(target.Serialize(), "Navigation to '" + target.Serialize() + "' was refused by the browser context's URL filter.");
        }

        var referrerUrl = request.Referrer ?? ReferrerFor(initiatorUrl);
        var fetchOptions = _options;

        var documentRequest = new DocumentRequest(
            target,
            request.Body is null ? "GET" : "POST",
            request.Body,
            request.ContentType,
            referrerUrl.Length == 0 ? null : UrlParser.Parse(referrerUrl),
            PageUrl.HasOrigin(initiatorUrl) ? UrlParser.Parse(initiatorUrl) : null,
            fetchOptions.MaxDocumentBytes,
            fetchOptions.MaxRedirects,
            Emulation.EffectiveUserAgent);

        // Resolved on the page's own thread, because that is where a host's client factory is documented to
        // be called and where the engine it is handed belongs. The engine it sees is the one the outgoing
        // document ran in — the engine that will show the new one does not exist yet — which is the same
        // engine every subresource of that document already went through.
        var client = await _loop.PostAsync(_network.ClientFor).ConfigureAwait(false);

        try
        {
            return await DocumentFetch
                .LoadAsync(_network, client, documentRequest, _requests, loaderId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new NavigationFailedException(target.Serialize(), "Navigation to '" + target.Serialize() + "' timed out.");
        }
    }

    /// <summary>
    /// The commit, on the page loop: unload the old document, swap the engine, parse the new one.
    /// </summary>
    private object? Commit(Engine current, CommitRequest request)
    {
        Unload(current);

        var documentId = _history.NextDocumentId();
        var history = request.History;

        // https://html.spec.whatwg.org/multipage/browsing-the-web.html#navigate step 20: navigating away from
        // the initial about:blank replaces its entry rather than pushing one. Without it every page would
        // start one entry deep and `history.length` would be one more than a browser answers for the same
        // sequence — which is exactly what a router's own back button counts.
        if (history == HistoryMode.Push && _isInitialAboutBlank)
        {
            history = HistoryMode.Replace;
        }

        _isInitialAboutBlank = false;

        switch (history)
        {
            case HistoryMode.Replace:
                _history.Replace(request.Url, documentId);
                break;

            case HistoryMode.Traverse:
                // The whole cluster of entries that shared the target's document — its pushState siblings —
                // moves to the document this load produced, so travelling among them afterwards is still a
                // same-document traversal rather than a chain of reloads.
                if (_history.At(request.TraversalIndex) is { } entry)
                {
                    _history.Rebind(entry.DocumentId, documentId);
                }

                _history.MoveTo(request.TraversalIndex);
                break;

            default:
                _history.Push(request.Url, documentId);
                break;
        }

        var engine = _loop.ReplaceEngine(() => BuildEngine(request.Url, request.Referrer));
        LoadInto(engine, request.Url, request.Html, request.Response, request.Referrer, request.OnPhase, request.LoaderId);

        if (history == HistoryMode.Traverse)
        {
            FirePopState(engine);
        }

        return null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/browsing-the-web.html#unloading-documents, minus the prompt a
    /// headless browser has nowhere to show.
    /// </summary>
    private bool FireBeforeUnload(Engine engine)
    {
        if (PageRuntime.Find(engine) is not { } runtime || engine._webApi?.GlobalEventTarget is not { } window)
        {
            return false;
        }

        var ev = PageEvents.BeforeUnload(runtime);
        PageEvents.Dispatch(runtime, window, ev);
        return PageEvents.AskedToStay(ev);
    }

    private void Unload(Engine engine)
    {
        if (PageRuntime.Find(engine) is not { } runtime)
        {
            return;
        }

        if (_load is not null && engine._webApi?.GlobalEventTarget is { } window)
        {
            var pageHide = PageEvents.Create(runtime, "pagehide");
            PageEvents.Member(pageHide, "persisted", JsBoolean.False);
            PageEvents.Dispatch(runtime, window, pageHide);

            PageEvents.Fire(runtime, window, "unload");
        }

        // After the events, because a pagehide listener sending a beacon should reach the network, and before
        // the engine is disposed, because this is what abandons whatever it still had in flight.
        try
        {
            runtime.Cancellation?.Cancel();
        }
        catch (AggregateException)
        {
            // A registration threw on its way out; the document is going either way.
        }
    }

    private object? LoadInto(
        Engine engine,
        string url,
        string html,
        PageResponse? response,
        string referrer,
        Action<NavigationPhase>? onPhase,
        string loaderId)
    {
        // The previous document goes first, and the page describes nothing until the new one exists. The
        // engine that document belonged to has already been replaced, so nothing can reach it; and a parse
        // that throws leaves a page with no document rather than one describing a document that is gone.
        var previous = _load;
        _load = null;
        _url = url;
        _referrer = referrer;
        _response = response;
        _mainFrame = Frame.Detached(this);
        Release(previous);

        var runtime = PageRuntime.Find(engine)!;
        runtime.DocumentUrl = url;
        runtime.Referrer = referrer;
        _loaderId = loaderId;
        CancelNetworkIdle();

        // The engine exists and its window is installed, and nothing of the document has been parsed. This is
        // where a protocol target replaces its engine, re-installs the bindings a client added and runs the
        // scripts it asked to be evaluated on every new document -- all of which have to be in place before
        // the first inline script of the document runs.
        _observer?.DocumentCreated(runtime, loaderId);

        // Before the parse, and the order is load-bearing rather than tidy. Every phase signal a caller of
        // NavigateAsync may be awaiting is raised *inside* the parse, so signalling afterwards would let a
        // navigation the caller has already finished awaiting satisfy a WaitForNavigationAsync armed on the
        // line after it — the wait would answer for the wrong navigation and the page would still be showing
        // the previous document. Waking here means every waiter is woken before any caller can arm one.
        // What a woken waiter then posts queues behind this request, so it still observes the parsed document.
        SignalNavigation();

        var load = PageDocument.Load(runtime, html, url, phase =>
        {
            onPhase?.Invoke(phase);
            Reached(runtime, phase, loaderId);
        });

        _load = load;
        _mainFrame = Frame.Build(this, load.Document, url);
        return null;
    }

    /// <summary>Tells the watcher how far the load got, and arms the quiet period once it is loaded.</summary>
    /// <remarks>
    /// The title is reported at each of the three, because it is what a client shows for the target and the
    /// parse is what settles it: a document's <c>&lt;title&gt;</c> is in place by the commit, and a script
    /// that rewrites it does so before <c>load</c> far more often than after.
    /// </remarks>
    private void Reached(PageRuntime runtime, NavigationPhase phase, string loaderId)
    {
        if (_observer is not { } observer)
        {
            return;
        }

        // Before the phase, because the phase is what a watcher forwards to a client and the tree has to be
        // observable by the time one can act on it. The commit is the one point where the runtime is worth
        // carrying: the other two are reported after a listener of this document could have navigated away.
        if (phase == NavigationPhase.Committed)
        {
            observer.DocumentParsed(runtime, loaderId);
        }

        observer.Phase(phase, loaderId);
        observer.TitleChanged(runtime.Document?.Title ?? "");

        if (phase == NavigationPhase.Loaded)
        {
            ArmNetworkIdle(loaderId);
        }
    }

    /// <summary>Records the entry the first <c>about:blank</c> document sits at, on the loop.</summary>
    private object? RecordFirstHistoryEntry(Engine engine)
    {
        _history.Push(_url, _history.NextDocumentId());
        return null;
    }

    /// <summary>
    /// A fragment navigation: the same document, a new history entry, and <c>hashchange</c>.
    /// </summary>
    private object? FragmentNavigate(Engine engine, string url, HistoryMode history)
    {
        if (PageRuntime.Find(engine) is not { } runtime)
        {
            return null;
        }

        var previous = _url;

        if (history == HistoryMode.Replace)
        {
            _history.ReplaceState(url, state: null);
        }
        else
        {
            _history.PushState(url, state: null);
        }

        _url = url;
        runtime.DocumentUrl = url;
        SignalNavigation();
        _observer?.SameDocumentNavigated(url, _loaderId);

        FireHashChange(runtime, previous, url);
        return null;
    }

    /// <summary>A traversal that stays in the document: <c>popstate</c>, and <c>hashchange</c> if it moved.</summary>
    private void TraverseSameDocument(PageRuntime runtime, int index)
    {
        if (_closed)
        {
            return;
        }

        var previous = _url;
        _history.MoveTo(index);

        var url = _history.Current?.Url ?? previous;
        _url = url;
        runtime.DocumentUrl = url;
        SignalNavigation();
        _observer?.SameDocumentNavigated(url, _loaderId);

        FirePopState(runtime.Engine);
        FireHashChange(runtime, previous, url);
    }

    private void FirePopState(Engine engine)
    {
        if (PageRuntime.Find(engine) is not { } runtime || engine._webApi?.GlobalEventTarget is not { } window)
        {
            return;
        }

        var state = _history.Current?.State is { } record
            ? new Jint.WebApi.StructuredClone.StructuredDeserializer(engine, engine._mainRealm, sharedRecord: true).Deserialize(record)
            : JsValue.Null;

        var ev = PageEvents.Create(runtime, "popstate");
        PageEvents.Member(ev, "state", state);
        PageEvents.Dispatch(runtime, window, ev);
    }

    private static void FireHashChange(PageRuntime runtime, string oldUrl, string newUrl)
    {
        if (string.Equals(PageUrl.FragmentOf(oldUrl), PageUrl.FragmentOf(newUrl), StringComparison.Ordinal))
        {
            return;
        }

        if (runtime.Engine._webApi?.GlobalEventTarget is not { } window)
        {
            return;
        }

        var ev = PageEvents.Create(runtime, "hashchange");
        PageEvents.Member(ev, "oldURL", JsString.Create(oldUrl));
        PageEvents.Member(ev, "newURL", JsString.Create(newUrl));
        PageEvents.Dispatch(runtime, window, ev);
    }

    /// <summary>
    /// The response's URL carrying the request's fragment, when the response answered with none of its own.
    /// </summary>
    private static string WithFragmentOf(string responseUrl, UrlRecord requested)
    {
        if (string.IsNullOrEmpty(requested.Fragment))
        {
            return responseUrl;
        }

        var final = UrlParser.Parse(responseUrl);
        if (final is null || !string.IsNullOrEmpty(final.Fragment))
        {
            return responseUrl;
        }

        final.Fragment = requested.Fragment;
        return final.Serialize();
    }

    /// <summary>What <c>document.referrer</c> and the <c>Referer</c> header report for the next document.</summary>
    /// <remarks>
    /// Only a document with a real origin is a referrer: <c>about:blank</c>, a <c>data:</c> URL and content a
    /// host set have none, and a browser sends no <c>Referer</c> from them either.
    /// </remarks>
    private static string ReferrerFor(string url) => PageUrl.HasOrigin(url) ? url : "";

    private void SignalNavigation()
    {
        TaskCompletionSource<bool>[] waiters;

        lock (_waitersGate)
        {
            if (_navigationWaiters.Count == 0)
            {
                return;
            }

            waiters = _navigationWaiters.ToArray();
            _navigationWaiters.Clear();
        }

        foreach (var waiter in waiters)
        {
            waiter.TrySetResult(true);
        }
    }

    private void FailPendingNavigationWaiters()
    {
        TaskCompletionSource<bool>[] waiters;

        lock (_waitersGate)
        {
            waiters = _navigationWaiters.ToArray();
            _navigationWaiters.Clear();
        }

        foreach (var waiter in waiters)
        {
            waiter.TrySetResult(false);
        }
    }

    /// <summary>
    /// The markup a URL that reaches no network carries: nothing for <c>about:</c>, and the payload of a
    /// <c>data:</c> URL, percent-decoded or base64-decoded.
    /// </summary>
    private static string ContentOf(string url)
    {
        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            throw new NavigationFailedException(
                url,
                "Jint.Browser cannot load '" + url + "': a page loads http, https, about: and data: URLs.");
        }

        var comma = url.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0)
        {
            throw new NavigationFailedException(url, "'" + url + "' is not a valid data URL: it has no comma.");
        }

        var metadata = url[5..comma];
        var payload = url[(comma + 1)..];

        try
        {
            if (metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.UTF8.GetString(System.Convert.FromBase64String(payload));
            }

            return Uri.UnescapeDataString(payload);
        }
        catch (FormatException exception)
        {
            throw new NavigationFailedException(url, "'" + url + "' is not a valid data URL: " + exception.Message, exception);
        }
    }

    /// <summary>Where a navigation puts its result in the session history.</summary>
    private enum HistoryMode
    {
        /// <summary>A new entry, and everything ahead of the current one is dropped.</summary>
        Push,

        /// <summary>The current entry, rewritten — <c>location.replace</c> and a reload.</summary>
        Replace,

        /// <summary>An entry that already exists, moved to — <c>history.back()</c> across documents.</summary>
        Traverse,
    }

    /// <summary>One navigation, from the moment something asked for it.</summary>
    private sealed record NavigationRequest(
        string Target,
        NavigationOptions Options,
        HistoryMode History,
        int TraversalIndex,
        byte[]? Body,
        string? ContentType,
        bool Reload,
        string? Referrer,
        string? InitiatorUrl = null,
        bool? FirstHopAllowed = null,
        string? InlineContent = null,
        Exception? PreflightFailure = null);

    /// <summary>What the loop is handed once the document's bytes are in.</summary>
    private sealed record CommitRequest(
        string Url,
        string Html,
        PageResponse? Response,
        HistoryMode History,
        int TraversalIndex,
        string Referrer,
        Action<NavigationPhase>? OnPhase,
        string LoaderId);

    /// <summary>Mints the identifier the next document carries, unique for the life of the page.</summary>
    private string NextLoaderId()
    {
        var loaderId = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{_loaderIdPrefix}{Interlocked.Increment(ref _loaderSerial)}");

        _pendingLoaderId = loaderId;
        return loaderId;
    }

    /// <summary>
    /// The three points a caller can wait for, as tasks the loop completes on its way through the load.
    /// </summary>
    private sealed class NavigationSignals
    {
        private readonly TaskCompletionSource _committed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _domContentLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Reached(NavigationPhase phase)
        {
            switch (phase)
            {
                case NavigationPhase.Committed:
                    _committed.TrySetResult();
                    break;

                case NavigationPhase.DomContentLoaded:
                    _committed.TrySetResult();
                    _domContentLoaded.TrySetResult();
                    break;

                default:
                    _committed.TrySetResult();
                    _domContentLoaded.TrySetResult();
                    _loaded.TrySetResult();
                    break;
            }
        }

        internal Task For(WaitUntilState state) => state switch
        {
            WaitUntilState.Commit => _committed.Task,
            WaitUntilState.DomContentLoaded => _domContentLoaded.Task,
            _ => _loaded.Task,
        };
    }
}
