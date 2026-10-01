using System.Runtime.CompilerServices;
using System.Text;
using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.WebApi.Fetch;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime.Parsing;

internal sealed partial class ParserDriver
{
    private enum CssImportLoadResult { Completed, Failed, Stale }
    private readonly ConditionalWeakTable<CssStyleSheet, ImportSheetState> _cssImportSheets = new();
    private readonly ConditionalWeakTable<CssImportRule, ImportAttempt> _cssImportAttempts = new();
    private readonly Dictionary<CssStyleSheet, ImportScan> _cssImportScans = new();
    private readonly Dictionary<CssStyleSheet, ImportNotification> _cssImportNotifications = new();
    private readonly HashSet<object> _activeCssImportSources = new();
    private readonly Dictionary<CssStyleSheet, int> _activeCssImportRoots = new();
    private readonly ConditionalWeakTable<Element, InlineImportWatch> _inlineImportWatches = new();
    private readonly List<WeakReference<MutationSubscription>> _inlineImportSubscriptions = new();

    private sealed record InlineImportWatch(Document Document, MutationSubscription Subscription);

    private void WatchInlineImportSource(Element owner, Document document)
    {
        if (_inlineImportWatches.TryGetValue(owner, out var existing))
        {
            if (ReferenceEquals(existing.Document, document)) return;
            existing.Subscription.Dispose();
            _inlineImportWatches.Remove(owner);
        }
        var subscription = document.ObserveMutations(owner, new MutationObserverOptions
        {
            ChildList = true,
            CharacterData = true,
            Attributes = true,
            AttributeFilter = ["type"],
            Subtree = true
        });
        var weak = new WeakReference<ParserDriver>(this);
        subscription.PendingRecord = pending =>
        {
            var records = pending.TakeRecords();
            if (!weak.TryGetTarget(out var driver) || driver._disposed)
            {
                pending.Dispose();
                return;
            }
            if (!ReferenceEquals(owner.OwnerDocument, document)) return;
            foreach (var record in records)
                if (record.Kind != MutationRecordKind.Attributes || ReferenceEquals(record.Target, owner))
                    NativeCssStyleSheets.InvalidateImportSourceAtArrival(document, owner);
        };
        _inlineImportWatches.Add(owner, new(document, subscription));
        if ((_inlineImportSubscriptions.Count & 63) == 0)
            _inlineImportSubscriptions.RemoveAll(static reference => !reference.TryGetTarget(out _));
        _inlineImportSubscriptions.Add(new(subscription));
    }

    private sealed class ImportSheetState
    {
        internal object Topology = new();
        internal object? CompletedTopology;
        internal object? RootGeneration;
        internal bool Failed;
        internal string? Charset;
        internal string[]? Keys;
    }

    private sealed class ImportAttempt
    {
        internal bool Attempted;
        internal bool Failed;
    }

    private sealed class ImportScan(CssImportSource source)
    {
        internal CssImportSource Source = source;
        internal object Nonce = new();
        internal bool Posted;
        internal bool Running;
        internal bool Delaying;
    }

    private sealed class ImportNotification
    {
        internal bool Delaying;
        internal bool Posted;
        internal bool Running;
    }

    private sealed class ImportFrame(CssStyleSheet sheet, CssImportRule? through, string charset,
        string? sourceUrl, string? baseUrl, ImportSheetState state, string[] keys)
    {
        internal CssStyleSheet Sheet { get; } = sheet;
        internal CssImportRule? Through { get; } = through;
        internal string Charset { get; } = charset;
        internal string? SourceUrl { get; } = sourceUrl;
        internal string? BaseUrl { get; } = baseUrl;
        internal ImportSheetState State { get; } = state;
        internal object Topology { get; } = state.Topology;
        internal string[] Keys { get; } = keys;
        internal int Next;
        internal bool Failed;
    }

    // CSS Cascade 5 §2.2 / CSSOM fetching CSS style sheets. Installation and CSSOM topology changes
    // enter this lane; ordinary style queries never fetch. Media filters applicability, not this load.
    private CssImportLoadResult LoadCssImports(Element owner, Func<bool> ownerRequestIsCurrent, bool mayPump,
        string? inheritedCharset = null, Func<bool>? ownerRequestIdentityIsCurrent = null)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_disposed || owner.OwnerDocument is not { } document) return CssImportLoadResult.Stale;
        var work = new CssValueWork(_cancellationToken, _runtime.Engine.Constraints.Check);
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, work);
        if (source is null) return CssImportLoadResult.Stale;
        // Freeze the owner document's network position before parsing or a network wait can pump.
        var metadata = DomDocumentState.Of(document);
        var origin = metadata.Origin.IsOpaque ? null : UrlParser.Parse(metadata.Origin.Serialized);
        var documentUrl = metadata.Url;
        var charset = inheritedCharset ?? metadata.CharacterSet;
        var frames = new List<ImportFrame>();
        var ancestry = new HashSet<string>(StringComparer.Ordinal);
        CssStyleSheet? root = null;
        var active = false;
        CssImportConnectivity? connectivity = null;
        // The optional identity predicate must be O(1): e.g. the exact StyleRequest token.
        // The full eligibility predicate can walk DOM ancestors and is boundary-only.
        bool Current()
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || !NativeCssStyleSheets.IsCurrent(source)) return false;
            var current = ownerRequestIdentityIsCurrent?.Invoke() ?? true;
            _cancellationToken.ThrowIfCancellationRequested();
            return current && !_disposed && NativeCssStyleSheets.IsCurrent(source);
        }
        bool BoundaryCurrent(CssImportRule? pending = null)
        {
            while (true)
            {
                if (!Current()) return false;
                var requestCurrent = ownerRequestIsCurrent();
                if (!Current() || !requestCurrent) return false;
                if (connectivity is null || !connectivity.IsCurrent)
                    connectivity = NativeCssStyleSheets.CaptureImportConnectivity(source, work);
                work.Charge(frames.Count);
                work.CheckCancellation();
                if (!Current()) return false;
                // A last callback can change ancestry. Rebuild its charged proof before trying
                // again; never traverse an uncharged path after that callback.
                if (!connectivity.IsCurrent) continue;
                return connectivity.Connected && (frames.Count == 0 || ActiveLinksCurrent(source, frames, pending));
            }
        }
        try
        {
            if (!BoundaryCurrent()) return CssImportLoadResult.Stale;
            if (source.Sheet is null && !NativeCssStyleSheets.MayContainImport(source, work))
                return BoundaryCurrent() ? CssImportLoadResult.Completed : CssImportLoadResult.Stale;
            root = NativeCssStyleSheets.EnsureSheet(source, work);
            if (!BoundaryCurrent() || !_activeCssImportSources.Add(source.SourceGeneration)) return CssImportLoadResult.Stale;
            active = true;
            _activeCssImportRoots.TryGetValue(root, out var count);
            _activeCssImportRoots[root] = count + 1;
            var rootState = _cssImportSheets.GetValue(root, static _ => new());
            if (!ReferenceEquals(rootState.RootGeneration, source.SourceGeneration))
            {
                rootState.RootGeneration = source.SourceGeneration;
                rootState.CompletedTopology = null;
                rootState.Charset = charset;
            }
            else charset = rootState.Charset ?? charset;
            if (ReferenceEquals(rootState.CompletedTopology, rootState.Topology))
                return rootState.Failed ? CssImportLoadResult.Failed : CssImportLoadResult.Completed;
            var rootUrl = owner.LocalName == "link" ? source.Attachment.SourceUrl?.AbsoluteUri : null;
            var rootKeys = rootUrl is null ? Array.Empty<string>() : new[] { UrlKey(rootUrl, work) };
            Push(root, null, charset, rootUrl, source.Attachment.BaseUrl?.AbsoluteUri, rootKeys);
            while (frames.Count != 0)
            {
                work.Charge(1);
                if (!Current()) return CssImportLoadResult.Stale;
                var frame = frames[^1];
                var rules = NativeCssParsing.ImportRules(frame.Sheet, work);
                if (frame.Next == rules.Count)
                {
                    // A callback can delete an ancestor without changing this source generation.
                    // Validate before popping, while the exact path is still retained.
                    if (!BoundaryCurrent()) return CssImportLoadResult.Stale;
                    frame.State.Failed = frame.Failed;
                    if (ReferenceEquals(frame.Topology, frame.State.Topology)) frame.State.CompletedTopology = frame.Topology;
                    foreach (var key in frame.Keys) ancestry.Remove(key);
                    frames.RemoveAt(frames.Count - 1);
                    if (frames.Count == 0) return frame.Failed ? CssImportLoadResult.Failed : CssImportLoadResult.Completed;
                    frames[^1].Failed |= frame.Failed;
                    continue;
                }
                // A deletion can move the next slot backwards while a fetch pumps; the coalesced CSSOM
                // scan revisits that topology afterwards. Active links still require exact identity.
                if (frame.Next > rules.Count) frame.Next = rules.Count;
                if (frame.Next == rules.Count) continue;
                var rule = rules[frame.Next++];
                if (rule is not CssImportRule import) continue;
                if (import.StyleSheet is { } existing)
                {
                    var completed = _cssImportSheets.GetValue(existing, static _ => new());
                    if (ReferenceEquals(completed.CompletedTopology, completed.Topology))
                    { frame.Failed |= completed.Failed; continue; }
                    var existingUrl = existing.Attachment.SourceUrl?.AbsoluteUri;
                    var keys = completed.Keys ?? (existingUrl is null ? Array.Empty<string>() : new[] { UrlKey(existingUrl, work) });
                    if (keys.Any(ancestry.Contains)) continue;
                    Push(existing, import, completed.Charset ?? frame.Charset, existingUrl, existing.Attachment.BaseUrl?.AbsoluteUri, keys);
                    continue;
                }
                var attempt = _cssImportAttempts.GetValue(import, static _ => new());
                if (attempt.Attempted) { frame.Failed |= attempt.Failed; continue; }
                var requested = PageUrl.Resolve(import.Href, frame.BaseUrl);
                if (requested is null)
                {
                    Failed(import.Href, "The imported stylesheet URL is invalid.", attempt, frame);
                    continue;
                }
                var requestedKey = UrlKey(requested, work);
                if (ancestry.Contains(requestedKey)) { attempt.Attempted = true; continue; }
                if (!BoundaryCurrent(import)) return CssImportLoadResult.Stale;
                string? failure = null;
                var referrer = UrlParser.Parse(frame.SourceUrl ?? documentUrl);
                var fetched = FetchBytes(requested, owner, "imported stylesheet", PageRequestKind.Stylesheet, mayPump,
                    onFailure: message => failure = message, fetchSource: new FetchSource(referrer, origin));
                if (!BoundaryCurrent(import)) return CssImportLoadResult.Stale;
                if (fetched is not { } body)
                {
                    Failed(requested, failure ?? "The imported stylesheet could not be loaded.", attempt, frame);
                    continue;
                }
                var finalKey = UrlKey(body.Url, work);
                if (!BoundaryCurrent(import)) return CssImportLoadResult.Stale;
                if (ancestry.Contains(finalKey)) { attempt.Attempted = true; continue; }
                // Tokenizer callbacks only check the root generation and immediate rule: the O(depth)
                // ancestry proof belongs to publication, not every byte/token checkpoint.
                var parsing = CssValueWork.Guard(work, () =>
                {
                    work.CheckCancellation();
                    if (!Current() || !ReferenceEquals(import.ParentStyleSheet, frame.Sheet) || import.StyleSheet is not null)
                        throw new CssImportSourceStaleException();
                });
                // The parent sheet's encoding is the environment encoding. Response MIME eligibility
                // remains follow-up work (LOAD-02); this is not complete CSS fetching.
                var (text, childCharset) = CssStyleSheetDecoding.Decode(body.Bytes, body.ContentType, frame.Charset);
                parsing.Charge(text.Length);
                var child = NativeCssParsing.CreateSheet(text, parsing);
                var childKeys = requestedKey == finalKey ? new[] { finalKey } : new[] { requestedKey, finalKey };
                // Charge before the final callback, then prove every active link with token checks only.
                work.Charge(NativeCssParsing.ImportRules(child, work).Count);
                if (!BoundaryCurrent(import)) return CssImportLoadResult.Stale;
                _cancellationToken.ThrowIfCancellationRequested();
                var url = new Uri(body.Url, UriKind.Absolute);
                import.SetStyleSheet(child, url, url, new CssValueWork(_cancellationToken));
                attempt.Attempted = true;
                var childState = _cssImportSheets.GetValue(child, static _ => new());
                childState.Charset = childCharset;
                childState.Keys = childKeys;
                Push(child, import, childCharset, body.Url, body.Url, childKeys);
            }
            return CssImportLoadResult.Completed;
        }
        catch (CssImportSourceStaleException) { return CssImportLoadResult.Stale; }
        finally
        {
            if (active)
            {
                _activeCssImportSources.Remove(source.SourceGeneration);
                if (root is not null && _activeCssImportRoots.TryGetValue(root, out var count))
                {
                    if (count == 1)
                    {
                        _activeCssImportRoots.Remove(root);
                        if (_cssImportScans.TryGetValue(root, out var scan)) PostCssImportScan(root, scan);
                    }
                    else _activeCssImportRoots[root] = count - 1;
                }
            }
        }

        void Push(CssStyleSheet sheet, CssImportRule? through, string inheritedCharset,
            string? sourceUrl, string? baseUrl, string[] keys)
        {
            foreach (var key in keys) ancestry.Add(key);
            frames.Add(new(sheet, through, inheritedCharset, sourceUrl, baseUrl,
                _cssImportSheets.GetValue(sheet, static _ => new()), keys));
        }

        void Failed(string url, string message, ImportAttempt attempt, ImportFrame frame)
        {
            attempt.Attempted = true;
            attempt.Failed = true;
            frame.Failed = true;
            Report(PageErrorKind.ReportedError, message, url);
        }
    }

    private bool ActiveLinksCurrent(CssImportSource source, List<ImportFrame> frames, CssImportRule? pending = null)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (!NativeCssStyleSheets.IsCurrent(source) || frames.Count == 0 ||
            !ReferenceEquals(frames[0].Sheet, source.Sheet) || (pending is not null &&
            (pending.StyleSheet is not null || !ReferenceEquals(pending.ParentStyleSheet, frames[^1].Sheet)))) return false;
        for (var i = 1; i < frames.Count; i++)
        {
            if ((i & 1023) == 0) _cancellationToken.ThrowIfCancellationRequested();
            var through = frames[i].Through!;
            if (!ReferenceEquals(through.ParentStyleSheet, frames[i - 1].Sheet) ||
                !ReferenceEquals(through.StyleSheet, frames[i].Sheet)) return false;
        }
        return true;
    }

    private static string UrlKey(string url, CssValueWork work)
    {
        work.Charge(url.Length);
        var key = UrlParser.Parse(url)?.Serialize(excludeFragment: true) ?? url;
        work.Charge(key.Length);
        return key;
    }

    internal void QueueCssImports(CssStyleSheet changedSheet)
    {
        // The producer has already committed. Persist ownership and its load delay in O(1),
        // before any cancellation/constraint checkpoint or callback-capable ancestry work.
        if (_disposed || _cssImportNotifications.ContainsKey(changedSheet)) return;
        var notification = new ImportNotification();
        _cssImportNotifications.Add(changedSheet, notification);
        BeginResourceDelay();
        notification.Delaying = true;
        notification.Posted = true;
        try { _resourceTasks.Post(() => RunCssImportNotification(changedSheet, notification)); }
        catch { notification.Posted = false; throw; }
    }

    private void RunCssImportNotification(CssStyleSheet changedSheet, ImportNotification notification)
    {
        if (!notification.Delaying || notification.Running) return;
        notification.Posted = false;
        notification.Running = true;
        var prepared = false;
        try { PrepareCssImportScan(changedSheet); prepared = true; }
        finally
        {
            notification.Running = false;
            // Successful preparation transfers its delay to ImportScan, or proves this owner
            // stale. Interruption leaves the notification indexed for a healthy turn's retry.
            if (prepared) ReleaseCssImportNotification(changedSheet, notification);
        }
    }

    private bool HasPendingCssImportRecovery => _cssImportNotifications.Count != 0;

    private void RecoverCssImportNotifications()
    {
        if (_cssImportNotifications.Count == 0) return;
        var work = new CssValueWork(_cancellationToken, _runtime.Engine.Constraints.Check);
        while (true)
        {
            var count = _cssImportNotifications.Count;
            work.Charge(count);
            work.CheckCancellation();
            if (count != _cssImportNotifications.Count) continue;
            // Charge before snapshot allocation and avoid enumerating a callback-mutable map.
            var pending = _cssImportNotifications.ToArray();
            foreach (var entry in pending)
            {
                work.Charge(1);
                if (entry.Value.Delaying && !entry.Value.Posted && !entry.Value.Running)
                    RunCssImportNotification(entry.Key, entry.Value);
            }
            return;
        }
    }

    private void ReleaseCssImportNotification(CssStyleSheet changedSheet, ImportNotification notification)
    {
        _cssImportNotifications.Remove(changedSheet);
        if (!notification.Delaying) return;
        notification.Delaying = false;
        EndResourceDelay();
    }

    private void PrepareCssImportScan(CssStyleSheet changedSheet)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return;
        var work = new CssValueWork(_cancellationToken, _runtime.Engine.Constraints.Check);
        var root = changedSheet;
        var path = new List<CssStyleSheet>();
        while (true)
        {
            work.Charge(1);
            path.Add(root);
            if (root.Attachment.ImportOwner is not { } import) break;
            if (import.ParentStyleSheet is not { } parent || !ReferenceEquals(import.StyleSheet, root)) return;
            root = parent;
        }
        if (root.Attachment.OwnerNode is not Element owner || owner.OwnerDocument is not { } document) return;
        var source = NativeCssStyleSheets.CaptureImportSource(document, owner, work);
        if (source is null || !ReferenceEquals(source.Sheet, root)) return;
        work.Charge(path.Count);
        work.CheckCancellation();
        if (!NativeCssStyleSheets.IsCurrent(source)) return;
        for (var i = 0; i + 1 < path.Count; i++)
        {
            if ((i & 1023) == 0) _cancellationToken.ThrowIfCancellationRequested();
            if (path[i].Attachment.ImportOwner is not { } import || !ReferenceEquals(import.StyleSheet, path[i]) ||
                !ReferenceEquals(import.ParentStyleSheet, path[i + 1])) return;
        }
        var topology = new object();
        foreach (var sheet in path) _cssImportSheets.GetValue(sheet, static _ => new()).Topology = topology;
        if (!_cssImportScans.TryGetValue(root, out var scan))
        {
            scan = new(source);
            _cssImportScans.Add(root, scan);
            BeginResourceDelay();
            scan.Delaying = true;
        }
        else { scan.Source = source; scan.Nonce = new(); }
        PostCssImportScan(root, scan);
    }

    private void PostCssImportScan(CssStyleSheet root, ImportScan scan)
    {
        if (_disposed || scan.Posted || scan.Running || !scan.Delaying || _activeCssImportRoots.ContainsKey(root)) return;
        scan.Posted = true;
        try { _resourceTasks.Post(() => RunCssImportScan(root, scan)); }
        catch { ReleaseCssImportScan(root, scan); throw; }
    }

    private void RunCssImportScan(CssStyleSheet root, ImportScan scan)
    {
        scan.Posted = false;
        if (!scan.Delaying) return;
        scan.Running = true;
        var nonce = scan.Nonce;
        try
        {
            if (!_disposed && NativeCssStyleSheets.IsCurrent(scan.Source))
            {
                using var mutation = _runtime.Layout.BeginMutation();
                LoadCssImports(scan.Source.Owner, () => NativeCssStyleSheets.IsCurrent(scan.Source), mayPump: true,
                    ownerRequestIdentityIsCurrent: () => NativeCssStyleSheets.IsCurrent(scan.Source));
            }
        }
        finally
        {
            scan.Running = false;
            if (!_disposed && scan.Delaying && !ReferenceEquals(nonce, scan.Nonce) && NativeCssStyleSheets.IsCurrent(scan.Source))
                PostCssImportScan(root, scan);
            else ReleaseCssImportScan(root, scan);
        }
    }

    private void ReleaseCssImportScan(CssStyleSheet root, ImportScan scan)
    {
        _cssImportScans.Remove(root);
        if (!scan.Delaying) return;
        scan.Delaying = false;
        EndResourceDelay();
    }

    private void DisposeCssImports()
    {
        foreach (var reference in _inlineImportSubscriptions)
            if (reference.TryGetTarget(out var subscription)) subscription.Dispose();
        _inlineImportSubscriptions.Clear();
        _inlineImportWatches.Clear();
        foreach (var notification in _cssImportNotifications.Values)
        {
            if (!notification.Delaying) continue;
            notification.Delaying = false;
            EndResourceDelay();
        }
        _cssImportNotifications.Clear();
        foreach (var scan in _cssImportScans.Values)
        {
            if (!scan.Delaying) continue;
            scan.Delaying = false;
            EndResourceDelay();
        }
        _cssImportScans.Clear();
        _cssImportSheets.Clear();
        _cssImportAttempts.Clear();
    }
}
