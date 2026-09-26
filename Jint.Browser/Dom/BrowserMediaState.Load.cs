using System.Runtime.ExceptionServices;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.Browser.Runtime.Parsing;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

internal sealed partial class BrowserMediaState
{
    private LoadOperation? _loadOperation;
    private int _networkState;
    private string _currentSrc = "";
    private BrowserMediaError? _error;

    /// <summary>An explicit request through the active document's existing resource owner.</summary>
    internal void Load(DomRealm realm)
    {
        CheckLoad(realm);
        var source = SelectSource(realm);
        var owner = _element.OwnerDocument!;
        var runtime = PageRuntime.FindBrowsingContext(realm.Engine, owner);
        var parser = runtime?.Parser;
        if (source.Length > 0 && parser is null)
        {
            DomFailures.Refuse(realm, "HTMLMediaElement.load", "NotSupportedError", "This document has no media resource owner.");
        }

        CheckLoad(realm);
        var operation = source.Length == 0 ? null : new LoadOperation();
        var resolved = "";
        Task<MediaResourceResponse>? request;
        try
        {
            // Request setup and all URL/DOM/constraint reads occur on the page loop. Do not
            // widen this catch into a constraint-swallowing media error translation.
            request = operation is null ? null : parser!.RequestMediaAsync(_element, source, out resolved, operation.Token);
        }
        catch
        {
            operation?.DisposeToken();
            throw;
        }

        var previousNetworkState = NetworkState;
        _loadOperation?.Cancel();
        _loadOperation = operation;
        CurrentTime = 0;
        if (_playbackRate != _defaultPlaybackRate)
        {
            _playbackRate = _defaultPlaybackRate;
            QueueEvent(realm, "ratechange");
        }
        _error = null;
        _networkState = operation is null ? 0 : 2;
        _currentSrc = resolved;
        if (previousNetworkState == 2) QueueEvent(realm, "abort");
        if (previousNetworkState != 0) QueueEvent(realm, "emptied");
        if (operation is null) return;

        // This observer is allocated only for an actual explicit load. Native mutation
        // callbacks cancel transport; they never run script, fetch, or inspect a DOM tree.
        var subscription = owner.ObserveMutations(_element, new MutationObserverOptions
        {
            Attributes = true, AttributeFilter = ["src"], ChildList = true, Subtree = true
        });
        var weakOperation = new WeakReference<LoadOperation>(operation);
        subscription.PendingRecord = pending =>
        {
            pending.TakeRecords();
            if (weakOperation.TryGetTarget(out var active)) active.Cancel();
        };
        var engine = realm.Engine;
        EventHandler? disposed = null;
        void Release(bool cancel = false)
        {
            subscription.Dispose();
            if (cancel) operation.Cancel();
            operation.DisposeToken();
            engine.Disposed -= disposed;
        }
        disposed = (_, _) => Release(cancel: true);
        engine.Disposed += disposed;
        QueueEvent(realm, "loadstart");

        // Completion code is captured here, then invoked only by the retained driver seam
        // on the loop. The asynchronous wait reads CLR transport state only.
        _ = AwaitResource(request!, parser!, operation, error =>
        {
            Release();
            if (error is not null && error is not (SubresourceFetchException or OperationCanceledException))
            {
                // Host failures remain failures of the owning page task, never a fake media result.
                ExceptionDispatchInfo.Capture(error).Throw();
            }
            if (!ReferenceEquals(_loadOperation, operation)) return;
            _loadOperation = null;
            if (!ReferenceEquals(owner, _element.OwnerDocument) || operation.WasCanceled
                || source != SelectSource(realm)
                || resolved != PageUrl.Resolve(source, DomDocumentState.BaseUri(owner, realm.Engine.Constraints.Check, realm.CancellationToken)))
            {
                _networkState = 0;
                _currentSrc = "";
                return;
            }
            // Fetching bytes is not decoding them. Report the actual failure, without
            // inventing metadata, buffers, tracks, dimensions or successful playback.
            _networkState = 3;
            _error = error is not null
                ? new BrowserMediaError(2, "The media resource request failed.")
                : new BrowserMediaError(4, "No media decoder is available.");
            QueueEvent(realm, "error");
        });
    }

    private static async Task AwaitResource(Task<MediaResourceResponse> request, ParserDriver parser,
        LoadOperation operation, Action<Exception?> complete)
    {
        Exception? error = null;
        try
        {
            _ = await request.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Marshal unexpected failures too, so they are observed on the owning loop.
            error = exception;
        }
        finally
        {
            operation.DisposeToken();
        }
        parser.TryPostResourceCompletion(() => complete(error));
    }

    private string SelectSource(DomRealm realm)
    {
        if (_element.GetAttributeNS(null, "src") is { } source) return source;
        var work = 0;
        for (var child = _element.FirstChild; child is not null; child = child.NextSibling)
        {
            if ((++work & 255) == 0) CheckLoad(realm);
            if (child is Element candidate && EventDom.IsHtml(candidate, "source")
                && candidate.GetAttributeNS(null, "src") is { Length: > 0 } value) return value;
        }
        return "";
    }

    private static void CheckLoad(DomRealm realm)
    {
        realm.CancellationToken.ThrowIfCancellationRequested();
        realm.Engine.Constraints.Check();
    }

    private sealed class LoadOperation
    {
        private readonly object _gate = new();
        private CancellationTokenSource? _cancellation = new();
        private volatile bool _wasCanceled;
        internal CancellationToken Token => _cancellation!.Token;
        internal bool WasCanceled => _wasCanceled;
        internal bool IsCanceled => _wasCanceled;
        internal void Cancel()
        {
            lock (_gate)
            {
                _wasCanceled = true;
                _cancellation?.Cancel();
            }
        }
        internal void DisposeToken()
        {
            lock (_gate)
            {
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }
    }
}

internal sealed class BrowserMediaError(int code, string message)
{
    internal int Code { get; } = code;
    internal string Message { get; } = message;
}
