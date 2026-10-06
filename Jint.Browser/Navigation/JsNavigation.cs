using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.GlobalEvents;
using Jint.WebApi.Streams;
using Jint.WebApi.StructuredClone;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Navigation;

/// <summary>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#navigation-interface.
/// The engine-affine view of the page's one, engine-free session history.
/// </summary>
internal sealed class JsNavigation : JsEventTarget
{
    private readonly ConditionalWeakTable<HistoryEntry, JsNavigationHistoryEntry> _entries = new();
    private readonly Dictionary<string, MethodTracker> _upcoming = new(StringComparer.Ordinal);
    private NavigationOperation? _ongoing;
    private long _serial;
    private bool _unloaded;

    internal JsNavigation(NavigationRealm owner) : base(owner.Engine, owner.Realm)
    {
        Owner = owner;
        Runtime = ReferenceEquals(owner.Realm, Engine._mainRealm) ? PageRuntime.Find(Engine) : null;
        DocumentId = Runtime?.Page.History.CurrentDocumentId ?? -1;
        _prototype = owner.Prototype("Navigation");
        if (Enabled && Runtime!.Page.History.ActivationEntry is { } entry)
        {
            Activation = new JsNavigationActivation(owner, Wrap(entry),
                Runtime.Page.History.ActivationFrom is { } from ? Wrap(from) : null, Runtime.Page.History.ActivationType);
        }
    }

    internal NavigationRealm Owner { get; }
    internal PageRuntime? Runtime { get; }
    internal long DocumentId { get; }
    internal bool Active => !_unloaded && Runtime is not null;
    private bool CanNavigate => Active && !Runtime!.IsUnloading;
    private bool Enabled => Active && !Runtime!.Page.IsInitialAboutBlank && Runtime.Page.History.Current is { Origin.IsOpaque: false };
    internal JsNavigationActivation? Activation { get; }
    internal JsNavigationTransition? Transition { get; private set; }
    internal JsNavigationHistoryEntry? CurrentEntry => Enabled && Runtime!.Page.History.Current is { } entry ? Wrap(entry) : null;
    internal CancellationToken NavigationCancellation => _ongoing?.Event.Signal.CancellationToken ?? default;
    internal long OngoingId => _ongoing?.Id ?? 0;

    private JsNavigationHistoryEntry Wrap(HistoryEntry entry) => _entries.GetValue(entry, value => new JsNavigationHistoryEntry(this, value));

    private List<HistoryEntry> VisibleEntries()
    {
        var entries = new List<HistoryEntry>();
        if (!Enabled) return entries;
        var history = Runtime!.Page.History;
        var (first, last) = history.NavigationRange();
        for (var i = first; i <= last; i++) entries.Add(history.At(i)!);
        return entries;
    }

    internal JsArray Entries() => Owner.Realm.Intrinsics.Array.ConstructFast(VisibleEntries().Select(entry => (JsValue) Wrap(entry)).ToArray());

    internal int IndexOf(HistoryEntry entry)
    {
        if (!Enabled) return -1;
        var history = Runtime!.Page.History;
        var (first, last) = history.NavigationRange();
        for (var i = first; i <= last; i++)
        {
            if (ReferenceEquals(history.At(i), entry)) return i - first;
        }
        return -1;
    }

    internal bool CanTraverse(int delta) => Adjacent(delta) is not null;

    private HistoryEntry? Adjacent(int delta)
    {
        if (!Enabled) return null;
        var history = Runtime!.Page.History;
        var (first, last) = history.NavigationRange();
        var index = history.Index + delta;
        return index >= first && index <= last ? history.At(index) : null;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-navigation-updatecurrententry</summary>
    internal JsValue Update(JsValue[] args)
    {
        RequireArgument(args);
        var options = NavigationValues.Dictionary(Owner.Realm, args[0]);
        var state = NavigationValues.Get(options, "state");
        if (state.IsUndefined()) Throw.TypeError(Owner.Realm, "NavigationUpdateCurrentEntryOptions.state is required.");
        var entry = CurrentEntry;
        if (entry is null) return DomFailures.Refuse(Owner.Dom, "updateCurrentEntry", DomExceptionNames.InvalidState, "There is no current navigation entry.");
        var record = NavigationValues.Serialize(Owner, state);
        entry.Entry.NavigationState = record;
        Dispatch(JsNavigationCurrentEntryChangeEvent.CreateTrusted(Owner.Dom, null, entry));
        return Undefined;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-navigation-navigate</summary>
    internal JsValue Navigate(JsValue[] args)
    {
        RequireArgument(args);
        var text = TypeConverter.ToString(args[0]);
        var options = NavigationValues.Dictionary(Owner.Realm, args.At(1));
        var info = NavigationValues.Get(options, "info");
        var history = NavigationValues.Enum(Owner.Realm, NavigationValues.Get(options, "history"), "auto", "auto", "push", "replace");
        var state = NavigationValues.Get(options, "state");
        var baseUri = Runtime?.BaseUri ?? (Owner.Dom.Document is { } document
            ? DomDocumentState.BaseUri(document, Engine.Constraints.Check, Owner.Dom.CancellationToken) : Owner.Dom.CreationUrl);
        var target = PageUrl.Parse(text, baseUri);
        if (target is null) return EarlyError(DomExceptionNames.Syntax, "The navigation URL cannot be parsed.");
        if (target.Scheme == "javascript") return EarlyError(DomExceptionNames.NotSupported, "Navigation to a javascript: URL is not supported.");
        if (history == "push" && Runtime?.Page.IsInitialAboutBlank == true)
            return EarlyError(DomExceptionNames.NotSupported, "The initial about:blank entry must be replaced.");
        SerializationRecord serialized;
        try { serialized = NavigationValues.Serialize(Owner, state); }
        catch (JavaScriptException exception) { return EarlyError(exception.Error); }
        if (!CanNavigate) return EarlyError(DomExceptionNames.InvalidState, "The document is inactive or unloading.");
        var url = target.Serialize();
        var type = history == "replace" || history == "auto" && (Runtime!.Page.IsInitialAboutBlank || url == Runtime.DocumentUrl)
            ? "replace" : "push";
        return Navigate(url, type, serialized, info);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-navigation-reload</summary>
    internal JsValue Reload(JsValue[] args)
    {
        var options = NavigationValues.Dictionary(Owner.Realm, args.At(0));
        var info = NavigationValues.Get(options, "info");
        var state = NavigationValues.Get(options, "state");
        SerializationRecord? serialized = CurrentEntry?.Entry.NavigationState;
        try
        {
            if (!state.IsUndefined()) serialized = NavigationValues.Serialize(Owner, state);
        }
        catch (JavaScriptException exception) { return EarlyError(exception.Error); }
        if (!CanNavigate) return EarlyError(DomExceptionNames.InvalidState, "The document is inactive or unloading.");
        return Navigate(Runtime!.DocumentUrl, "reload", serialized, info);
    }

    private JsValue Navigate(string url, string type, SerializationRecord? state, JsValue info)
    {
        var tracker = new MethodTracker(Owner, info, state);
        if (!Handle(url, type, tracker: tracker))
        {
            Runtime!.Page.RequestNavigation(url, replace: type != "push", reload: type == "reload", engine: Engine,
                navigationEventDispatched: true, navigationState: state, navigationId: OngoingId, navigationCancellation: NavigationCancellation);
        }
        return tracker.Result();
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#performing-a-navigation-api-traversal</summary>
    internal JsValue Traverse(JsValue[] args, int delta = 0)
    {
        string? key = null;
        if (delta == 0)
        {
            RequireArgument(args);
            key = TypeConverter.ToString(args[0]);
        }
        var options = NavigationValues.Dictionary(Owner.Realm, args.At(delta == 0 ? 1 : 0));
        var info = NavigationValues.Get(options, "info");
        if (!CanNavigate) return EarlyError(DomExceptionNames.InvalidState, "The document is inactive or unloading.");
        var entry = delta == 0 ? VisibleEntries().Find(candidate => candidate.Key == key) : Adjacent(delta);
        if (entry is null) return EarlyError(DomExceptionNames.InvalidState, "No matching navigation history entry exists.");
        if (_upcoming.TryGetValue(entry.Key, out var pending)) return pending.Result();
        var tracker = new MethodTracker(Owner, info, null);
        if (ReferenceEquals(entry, Runtime!.Page.History.Current))
        {
            tracker.Committed.Resolve(Wrap(entry));
            tracker.Finished.Resolve(Wrap(entry));
        }
        else
        {
            _upcoming.Add(entry.Key, tracker);
            var history = Runtime.Page.History;
            var (first, _) = history.NavigationRange();
            Runtime.Page.RequestTraversal(first + IndexOf(entry) - history.Index, rendererInitiated: true);
        }
        return tracker.Result();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#inner-navigate-event-firing-algorithm.
    /// True means cancellation or a same-document commit consumed the request; false continues the fetch.
    /// </summary>
    internal bool Handle(string url, string type, HistoryEntry? traversal = null, SerializationRecord? classicState = null,
        bool classicHistory = false, JsValue? source = null, JsValue? formData = null, string? download = null,
        bool userInitiated = false, MethodTracker? tracker = null)
    {
        if (!Enabled) return false;
        // HTML prepares to run script around the entire event/commit sequence, not around each listener.
        Engine.EnterExecutionContext(Engine.ExecutionContext);
        try
        {
            while (_ongoing is { } previous)
            {
                Engine.Constraints.Check();
                Abort(previous, Error(DomExceptionNames.Abort, "The navigation was superseded."));
            }
            if (traversal is not null && _upcoming.Remove(traversal.Key, out var upcoming)) tracker = upcoming;
            var sameDocument = classicHistory || traversal is not null && traversal.DocumentId == DocumentId
                || type is "push" or "replace" && formData is null && download is null && UrlParser.Parse(url)?.Fragment is not null
                    && PageUrl.IsSameDocument(Runtime!.DocumentUrl, url);
            var destinationEntry = traversal is not null && IndexOf(traversal) >= 0 ? Wrap(traversal) : null;
            var destinationState = traversal is not null
                ? destinationEntry is not null ? traversal.NavigationState : NavigationValues.Serialize(Owner, Null)
                : tracker is not null ? tracker.State : NavigationValues.Serialize(Owner, Null);
            var destination = new JsNavigationDestination(Owner, url, sameDocument, destinationState, destinationEntry);
            var signal = Owner.Realm.Intrinsics.AbortSignal.CreateSignal();
            var ev = new JsNavigateEvent(Engine, new JsString("navigate"), new EventInit(false, type != "traverse" || sameDocument, false),
                EventConstructor.TimeStampNow(Engine), type, destination, signal)
            {
                Navigation = this,
                CanIntercept = CanRewriteUrl(url) && (type != "traverse" || sameDocument),
                HashChange = !classicHistory && sameDocument && PageUrl.IsSameDocument(Runtime!.DocumentUrl, url)
                    && UrlParser.Parse(Runtime.DocumentUrl)?.Fragment != UrlParser.Parse(url)?.Fragment,
                Info = tracker?.Info ?? Undefined,
                SourceElement = source ?? Null,
                FormData = formData ?? Null,
                DownloadRequest = download,
                UserInitiated = userInitiated,
            };
            BrowserEventRealm.Of(Engine, Owner.Realm).CreateTrusted(BrowserEventInterfaces.NavigateEvent, ev);
            var operation = new NavigationOperation(++_serial, ev, tracker);
            _ongoing = operation;
            Dispatch(ev);
            if (signal.Aborted) return true;
            if (ev.CanceledFlag)
            {
                Abort(operation, Error(DomExceptionNames.Abort, "The navigate event was canceled."));
                return true;
            }
            if (ev.InterceptionState != "none")
            {
                operation.Transition = new JsNavigationTransition(Owner, type, CurrentEntry!, destination);
                Transition = operation.Transition;
                ev.InterceptionState = "committed";
            }
            // An uncanceled cross-document or intercepted navigation becomes the navigable's ongoing navigation,
            // aborting an uncommitted host load (set the ongoing navigation); a plain fragment navigation does not.
            if (type != "traverse" && (!sameDocument || ev.InterceptionState != "none")) Runtime!.Page.AbortBrowserUiNavigation();
            if (!sameDocument && ev.InterceptionState == "none") return false;

            var before = VisibleEntries();
            var from = CurrentEntry!;
            var oldUrl = Runtime!.DocumentUrl;
            Runtime.Page.CommitNavigationApi(Runtime, url, type, traversal, classicState, tracker?.State);
            operation.Entry = CurrentEntry!;
            tracker?.Committed.Resolve(operation.Entry);
            operation.Transition?.Committed.Resolve(Undefined);
            List<JsNavigationHistoryEntry>? disposed = null;
            foreach (var removed in before)
            {
                if (IndexOf(removed) < 0 && _entries.TryGetValue(removed, out var wrapper))
                    (disposed ??= []).Add(wrapper);
            }
            Dispatch(JsNavigationCurrentEntryChangeEvent.CreateTrusted(Owner.Dom, type, from));
            if (disposed is not null)
            {
                foreach (var entry in disposed)
                    DispatchAt(entry, Owner.Realm.Intrinsics.Event.CreateTrustedEvent(new JsString("dispose")));
            }
            RunHandlers(operation);
            Page.FireNavigationApiEvents(Runtime, oldUrl, url, type, classicHistory, ev.InterceptionState != "none", traversal?.State);
            return true;
        }
        finally
        {
            Engine.LeaveExecutionContext();
            Engine.CleanUpAfterRunningScript();
        }
    }

    private bool CanRewriteUrl(string url)
    {
        var current = UrlParser.Parse(Runtime!.DocumentUrl)!;
        var target = UrlParser.Parse(url)!;
        if (current.Scheme != target.Scheme || current.SerializeHostAndPort() != target.SerializeHostAndPort()
            || current.Username != target.Username || current.Password != target.Password) return false;
        if (current.Scheme is "http" or "https") return true;
        return current.SerializePath() == target.SerializePath() && current.Query == target.Query;
    }

    private void RunHandlers(NavigationOperation operation)
    {
        var ev = operation.Event;
        var promises = new List<JsPromise>();
        foreach (var handler in ev.Handlers)
        {
            try
            {
                promises.Add(StreamPromises.ResolvedWith(Engine, Owner.Realm, Engine.Call(handler, Undefined, [], expression: null)));
            }
            catch (JavaScriptException exception)
            {
                promises.Add(StreamPromises.RejectedWith(Engine, Owner.Realm, exception.Error, handled: true));
            }
        }
        ev.Handlers.Clear();
        if (promises.Count == 0) promises.Add(StreamPromises.ResolvedWithUndefined(Engine, Owner.Realm));
        var remaining = promises.Count;
        foreach (var promise in promises)
        {
            StreamPromises.UponPromise(Engine, promise, _ =>
            {
                if (--remaining != 0 || !IsOngoing(operation)) return;
                Complete(operation);
            }, reason =>
            {
                if (!IsOngoing(operation)) return;
                Abort(operation, reason, finish: true);
            });
        }
    }

    private void Complete(NavigationOperation operation)
    {
        Engine.EnterExecutionContext(Engine.ExecutionContext);
        try
        {
            _ongoing = null;
            operation.Event.Finish(success: true);
            operation.Tracker?.Finished.Resolve(operation.Entry!);
            Dispatch(Owner.Realm.Intrinsics.Event.CreateTrustedEvent(new JsString("navigatesuccess")));
            operation.Transition?.Finished.Resolve(Undefined);
            if (ReferenceEquals(Transition, operation.Transition)) Transition = null;
        }
        finally
        {
            Engine.LeaveExecutionContext();
            Engine.CleanUpAfterRunningScript();
        }
    }

    private bool IsOngoing(NavigationOperation operation) => Active && ReferenceEquals(_ongoing, operation) && !operation.Event.Signal.Aborted;

    private void Abort(NavigationOperation operation, JsValue reason, bool finish = false)
    {
        Engine.EnterExecutionContext(Engine.ExecutionContext);
        try
        {
            if (ReferenceEquals(_ongoing, operation)) _ongoing = null;
            var ev = operation.Event;
            if (finish) ev.Finish(success: false);
            if (ev.DispatchFlag) ev.CanceledFlag = true;
            ev.Signal.SignalAbort(reason);
            operation.Tracker?.Committed.Reject(reason);
            operation.Tracker?.Finished.Reject(reason);
            var details = ErrorEventDetails.FromReportedValue(reason, default);
            var error = new JsErrorEvent(Engine, new JsString("navigateerror"), new EventInit(false, false, false),
                EventConstructor.TimeStampNow(Engine), details)
            {
                IsTrusted = true,
                _prototype = Owner.Realm.Intrinsics.ErrorEvent.PrototypeObject,
            };
            Dispatch(error);
            operation.Transition?.Committed.Reject(reason);
            operation.Transition?.Finished.Reject(reason);
            if (ReferenceEquals(Transition, operation.Transition)) Transition = null;
        }
        finally
        {
            Engine.LeaveExecutionContext();
            Engine.CleanUpAfterRunningScript();
        }
    }

    internal void Failed(long id, string message)
    {
        if (_ongoing is { } operation && operation.Id == id) Abort(operation, Error(DomExceptionNames.Network, message));
    }

    internal void TraversalUnavailable(string key)
    {
        if (!_upcoming.Remove(key, out var tracker)) return;
        var error = Error(DomExceptionNames.InvalidState, "The traversal entry was removed.");
        tracker.Committed.Reject(error);
        tracker.Finished.Reject(error);
    }

    /// <summary>Cross-document commits leave the outgoing method promises pending, not fulfilled with foreign objects.</summary>
    internal void Unload()
    {
        _unloaded = true;
        _ongoing = null;
        _upcoming.Clear();
    }

    private void Dispatch(JsEvent ev) => DispatchAt(this, ev);
    private void DispatchAt(JsEventTarget target, JsEvent ev)
    {
        if (Runtime is { } runtime) PageEvents.Dispatch(runtime, target, ev);
        else target.DispatchEvent(ev);
    }

    private JsDomException Error(string name, string message) => Owner.Realm.Intrinsics.DomException.CreateException(name, message);
    private JsValue EarlyError(string name, string message) => EarlyError(Error(name, message));

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#navigation-api-early-error-result</summary>
    private JsValue EarlyError(JsValue error)
    {
        var tracker = new MethodTracker(Owner, Undefined, null, earlyError: true);
        tracker.Committed.Reject(error);
        tracker.Finished.Reject(error);
        return tracker.Result();
    }

    private void RequireArgument(JsValue[] args)
    {
        if (args.Length == 0) Throw.TypeError(Owner.Realm, "1 argument required, but only 0 present.");
    }

    internal sealed class MethodTracker
    {
        private readonly NavigationRealm _owner;
        internal MethodTracker(NavigationRealm owner, JsValue info, SerializationRecord? state, bool earlyError = false)
        {
            _owner = owner;
            Info = info;
            State = state;
            Committed = StreamPromises.NewPromise(owner.Engine, owner.Realm);
            Finished = StreamPromises.NewPromise(owner.Engine, owner.Realm);
            if (!earlyError) StreamPromises.MarkHandled(StreamPromises.PromiseOf(Finished));
        }
        internal JsValue Info { get; }
        internal SerializationRecord? State { get; }
        internal PromiseCapability Committed { get; }
        internal PromiseCapability Finished { get; }

        internal JsValue Result()
        {
            var result = new JsObject(_owner.Engine) { _prototype = _owner.Realm.Intrinsics.Object.PrototypeObject };
            result.DefineOwnPropertyUnchecked("committed", new PropertyDescriptor(Committed.PromiseInstance, PropertyFlag.ConfigurableEnumerableWritable));
            result.DefineOwnPropertyUnchecked("finished", new PropertyDescriptor(Finished.PromiseInstance, PropertyFlag.ConfigurableEnumerableWritable));
            return result;
        }
    }

    private sealed class NavigationOperation(long id, JsNavigateEvent ev, MethodTracker? tracker)
    {
        internal long Id { get; } = id;
        internal JsNavigateEvent Event { get; } = ev;
        internal MethodTracker? Tracker { get; } = tracker;
        internal JsNavigationHistoryEntry? Entry { get; set; }
        internal JsNavigationTransition? Transition { get; set; }
    }
}
