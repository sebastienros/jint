namespace Jint.HtmlParser;

// Selection owns this token. The range holds it weakly and retains no Browser closure.
internal sealed class RangeChangeSubscription(Document selectionDocument) : IDisposable
{
    internal Document Document { get; } = selectionDocument;
    private bool _connected = true;
    private bool _pending;
    internal bool Connected => _connected;
    internal void MarkPending() { if (_connected) _pending = true; }
    internal bool TakePendingChange()
    {
        var pending = _connected && _pending;
        _pending = false;
        return pending;
    }
    internal void Disconnect() { _connected = false; _pending = false; }
    public void Dispose() => Disconnect();
}

// Per-document operation scopes defer scheduling until every native phase has finished.
// The no-subscription path creates neither a collection nor a queue.
internal readonly struct RangeMutationScope : IDisposable
{
    private readonly Document _first;
    private readonly Document? _second;
    internal RangeMutationScope(Document first, Document? second = null)
    {
        _first = first;
        _second = ReferenceEquals(first, second) ? null : second;
        first.RangeOperationDepth++;
        if (_second is not null) _second.RangeOperationDepth++;
    }
    internal void DeferNotifications()
    {
        _first.DeferRangeScheduling = true;
        if (_second is not null) _second.DeferRangeScheduling = true;
    }
    public void Dispose()
    {
        // Common case: one document with no range that changed inside this scope.
        if (_second is null && _first.ChangedRanges is null && !_first.DeferRangeScheduling)
        {
            _first.RangeOperationDepth--;
            return;
        }
        DisposeSlow();
    }

    private void DisposeSlow()
    {
        var defer = _first.DeferRangeScheduling || _second?.DeferRangeScheduling == true;
        // Close both ownership scopes before any scheduling sink can fail.
        var first = Finish(_first);
        var second = _second is null ? null : Finish(_second);
        HashSet<Document>? signals = null;
        if (first is not null) foreach (var range in first) range.CollectChanges(ref signals, defer ? _first : null);
        if (second is not null) foreach (var range in second) range.CollectChanges(ref signals, defer ? _first : null);
        if (!defer) DomRange.ScheduleChanges(signals);
    }
    private static HashSet<DomRange>? Finish(Document document)
    {
        if (--document.RangeOperationDepth != 0) return null;
        document.DeferRangeScheduling = false;
        var changed = document.ChangedRanges;
        document.ChangedRanges = null;
        return changed;
    }

}

public sealed partial class DomRange
{
    private List<WeakReference<RangeChangeSubscription>>? _subscriptions;
    private int _subscriptionSweepCursor;
    private int _changeDepth;
    private bool _changed;
    internal RangeChangeSubscription ObserveChanges(Document selectionDocument) => ObserveChanges(selectionDocument, null);

    internal RangeChangeSubscription ObserveChanges(Document selectionDocument, Action<int>? workCheckpoint)
    {
        ArgumentNullException.ThrowIfNull(selectionDocument);
        var subscriptions = _subscriptions ??= [];
        var steps = 0;
        var budget = Math.Min(8, subscriptions.Count);
        for (var scanned = 0; scanned < budget && subscriptions.Count != 0; scanned++)
        {
            workCheckpoint?.Invoke(++steps);
            var index = _subscriptionSweepCursor % subscriptions.Count;
            if (!subscriptions[index].TryGetTarget(out var target) || !target.Connected)
            {
                subscriptions[index] = subscriptions[^1];
                subscriptions.RemoveAt(subscriptions.Count - 1);
            }
            else _subscriptionSweepCursor = index + 1;
        }
        var subscription = new RangeChangeSubscription(selectionDocument);
        subscriptions.Add(new(subscription));
        workCheckpoint?.Invoke(++steps);
        return subscription;
    }
    private readonly struct ChangeScope : IDisposable
    {
        private readonly DomRange _range;
        internal ChangeScope(DomRange range) { _range = range; range._changeDepth++; }
        public void Dispose() { if (--_range._changeDepth == 0) _range.FlushChanges(); }
    }
    private ChangeScope Changing() => new(this);
    private void Changed()
    {
        if (_subscriptions is null) return;
        _changed = true;
        if (_changeDepth == 0) FlushChanges();
    }
    internal void FlushChanges()
    {
        HashSet<Document>? signals = null;
        CollectChanges(ref signals);
        ScheduleChanges(signals);
    }
    internal void CollectChanges(ref HashSet<Document>? signals, Document? deferredCarrier = null)
    {
        if (!_changed || _changeDepth != 0) return;
        var document = LiveTraversalTracking.DocumentOf(Start.Container);
        if (document.RangeOperationDepth != 0)
        {
            (document.ChangedRanges ??= []).Add(this);
            return;
        }
        _changed = false;
        if (_subscriptions is not { } subscriptions) return;
        subscriptions.RemoveAll(static slot => !slot.TryGetTarget(out var target) || !target.Connected);
        foreach (var slot in subscriptions)
        {
            if (!slot.TryGetTarget(out var subscription)) continue;
            subscription.MarkPending();
            if (subscription.Document.PendingRangeChanges is not null)
            {
                if (deferredCarrier is not null) subscription.Document.MarkDeferredRangeSignal(deferredCarrier);
                else (signals ??= []).Add(subscription.Document);
            }
        }
        if (subscriptions.Count == 0) _subscriptions = null;
    }
    internal static void ScheduleChanges(HashSet<Document>? signals)
    {
        if (signals is null) return;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        foreach (var target in signals)
        {
            try { target.ScheduleRangeChanges(); }
            catch (Exception exception) { failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception); }
        }
        failure?.Throw();
    }
}
