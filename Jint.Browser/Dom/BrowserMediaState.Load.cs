using System.Runtime.ExceptionServices;
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
    private long _loadGeneration;

    /// <summary>An explicit request through the active document's existing resource owner.</summary>
    internal void Load(DomRealm realm, DomReadWork? completionWork = null)
    {
        CheckLoad(realm);
        var selection = SelectSource(realm);
        var source = selection.Value ?? "";
        var owner = _element.OwnerDocument!;
        var runtime = PageRuntime.FindBrowsingContext(realm.Engine, owner);
        var parser = runtime?.Parser;
        if (source.Length > 0 && parser is null)
        {
            DomFailures.Refuse(realm, "HTMLMediaElement.load", "NotSupportedError", "This document has no media resource owner.");
        }

        CheckLoad(realm);
        var invalidSource = selection.Element is not null && source.Length == 0;
        var operation = selection.Element is null || invalidSource ? null : new LoadOperation();
        var resolved = "";
        Task<MediaResourceResponse>? request;
        try
        {
            // Request setup and all URL/DOM/constraint reads occur on the page loop. Do not
            // widen this catch into a constraint-swallowing media error translation.
            request = operation is null ? null : parser!.RequestMediaAsync(_element, source, out resolved, operation.Token);
        }
        catch (MediaSourceException)
        {
            operation?.Dispose();
            operation = null;
            request = null;
            invalidSource = true;
        }
        catch
        {
            operation?.Dispose();
            throw;
        }

        var previousNetworkState = NetworkState;
        _loadOperation?.Cancel();
        _loadOperation = operation;
        var generation = ++_loadGeneration;
        CurrentTime = 0;
        if (_playbackRate != _defaultPlaybackRate)
        {
            _playbackRate = _defaultPlaybackRate;
            QueueEvent(realm, "ratechange");
        }
        _error = invalidSource ? new BrowserMediaError(4, "The selected media source is not loadable.") : null;
        _networkState = invalidSource ? 3 : operation is null ? 0 : 2;
        _currentSrc = resolved;
        if (previousNetworkState == 2) QueueEvent(realm, "abort", generation);
        if (previousNetworkState != 0) QueueEvent(realm, "emptied", generation);
        if (operation is null)
        {
            if (invalidSource) QueueEvent(realm, "error", generation);
            return;
        }

        // This observer is allocated only for an actual explicit load. Native mutation
        // callbacks cancel transport; they never run script, fetch, or inspect a DOM tree.
        var subscription = owner.ObserveMutations(_element, new MutationObserverOptions
        {
            Attributes = true,
            AttributeFilter = ["src"],
            ChildList = !ReferenceEquals(selection.Element, _element),
            Subtree = !ReferenceEquals(selection.Element, _element)
        });
        // The native callback's target contains weak engine/runtime references only;
        // it cannot share the load completion's realm-capturing closure.
        var watch = new SourceWatch(this, realm, parser!, operation, selection);
        subscription.PendingRecord = watch.Pending;
        var engine = realm.Engine;
        EventHandler? disposed = null;
        void Release(bool cancel = false)
        {
            subscription.Dispose();
            if (cancel) operation.Cancel();
            operation.Dispose();
            engine.Disposed -= disposed;
        }
        disposed = (_, _) => Release(cancel: true);
        engine.Disposed += disposed;
        QueueEvent(realm, "loadstart", generation);

        // Completion code is captured here, then invoked only by the retained driver seam
        // on the loop. The asynchronous wait reads CLR transport state only.
        _ = AwaitResource(request!, parser!, operation, error =>
        {
            var fresh = false;
            try
            {
                if (!ReferenceEquals(_loadOperation, operation)) return;
                if (error is not null && error is not (SubresourceFetchException or OperationCanceledException))
                {
                    // Host failures remain failures of the owning page task, never a fake media result.
                    ExceptionDispatchInfo.Capture(error).Throw();
                }
                var work = completionWork ?? new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
                work.Check();
                if (ReferenceEquals(owner, _element.OwnerDocument) && !operation.WasCanceled
                    && Matches(selection, SelectSource(realm, work), work))
                {
                    var currentUrl = PageUrl.Resolve(source, DomDocumentState.BaseUri(owner, work.Check, work.Token));
                    fresh = work.Equal(currentUrl, resolved);
                }
                work.Check();
            }
            catch
            {
                if (ReferenceEquals(_loadOperation, operation)) ClearLoad();
                throw;
            }
            finally
            {
                Release();
            }
            // All budgeted reads completed before detaching the live operation and committing facts.
            if (!ReferenceEquals(_loadOperation, operation)) return;
            if (!fresh) { ClearLoad(); return; }
            _loadOperation = null;
            // Fetching bytes is not decoding them. Report the actual failure, without
            // inventing metadata, buffers, tracks, dimensions or successful playback.
            _networkState = 3;
            _error = error is not null
                ? new BrowserMediaError(2, "The media resource request failed.")
                : new BrowserMediaError(4, "No media decoder is available.");
            QueueEvent(realm, "error", generation);
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
            operation.Dispose();
        }
        parser.TryPostResourceCompletion(() => complete(error));
    }

    private void ClearLoad()
    {
        ++_loadGeneration;
        _loadOperation = null;
        _networkState = 0;
        _currentSrc = "";
        _error = null;
    }

    private static bool Matches(SourceSelection previous, SourceSelection current, DomReadWork work)
        => ReferenceEquals(previous.Element, current.Element)
           && (previous.Value is null ? current.Value is null : work.Equal(current.Value, previous.Value));

    private SourceSelection SelectSource(DomRealm realm, DomReadWork? work = null)
    {
        work ??= new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var source = work.Attribute(_element, "src");
        work.Check();
        if (source is not null) return new SourceSelection(_element, source);
        for (var child = _element.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is Element candidate && work.Equal(candidate.NamespaceUri, Namespaces.Html)
                && work.Equal(candidate.LocalName, "source") && work.Attribute(candidate, "src") is { } value)
            {
                work.Check();
                return new SourceSelection(candidate, value);
            }
        }
        work.Check();
        return default;
    }

    private static void CheckLoad(DomRealm realm)
    {
        realm.CancellationToken.ThrowIfCancellationRequested();
        realm.Engine.Constraints.Check();
    }

    private sealed class LoadOperation : IDisposable
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
        public void Dispose()
        {
            lock (_gate)
            {
                _cancellation?.Dispose();
                _cancellation = null;
            }
            GC.SuppressFinalize(this);
        }
    }

    private readonly record struct SourceSelection(Element? Element, string? Value);

    /// <summary>A native mutation signal cannot retain an engine through its node's registrations.</summary>
    private sealed class SourceWatch
    {
        private readonly WeakReference<BrowserMediaState> _state;
        private readonly WeakReference<DomRealm> _realm;
        private readonly WeakReference<ParserDriver> _parser;
        private readonly WeakReference<LoadOperation> _operation;
        private readonly SourceSelection _selection;
        private bool _posted;

        internal SourceWatch(BrowserMediaState state, DomRealm realm, ParserDriver parser, LoadOperation operation, SourceSelection selection)
        {
            _state = new(state);
            _realm = new(realm);
            _parser = new(parser);
            _operation = new(operation);
            _selection = selection;
        }

        internal void Pending(MutationSubscription pending)
        {
            var records = pending.TakeRecords();
            if (!_state.TryGetTarget(out var state) || !_operation.TryGetTarget(out var operation)
                || !ReferenceEquals(state._loadOperation, operation)) return;
            if (ReferenceEquals(_selection.Element, state._element))
            {
                // A direct src ignores fallback descendants and source/track children entirely.
                foreach (var record in records)
                    if (record.Kind == MutationRecordKind.Attributes && ReferenceEquals(record.Target, state._element)
                        && record.AttributeNamespace is null && record.AttributeName == "src")
                    {
                        operation.Cancel();
                        state.InvalidateLoad();
                        return;
                    }
                return;
            }
            // Source-child mutations may change which candidate wins. Recheck bounded
            // selection on the loop instead of inspecting the tree inside a native callback.
            if (!_posted && _parser.TryGetTarget(out var parser))
            {
                _posted = parser.TryPostResourceCompletion(Recheck);
            }
        }

        private void Recheck()
        {
            _posted = false;
            if (!_state.TryGetTarget(out var state) || !_operation.TryGetTarget(out var operation)
                || !_realm.TryGetTarget(out var realm) || !ReferenceEquals(state._loadOperation, operation)) return;
            try
            {
                var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
                work.Check();
                var same = Matches(_selection, state.SelectSource(realm, work), work);
                work.Check();
                if (same) return;
                operation.Cancel();
                state.InvalidateLoad();
            }
            catch
            {
                operation.Cancel();
                state.ClearLoad();
                throw;
            }
        }
    }

    private void InvalidateLoad()
    {
        ++_loadGeneration;
        _networkState = 0;
        _currentSrc = "";
        _error = null;
    }
}

internal sealed class BrowserMediaError(int code, string message)
{
    internal int Code { get; } = code;
    internal string Message { get; } = message;
}
