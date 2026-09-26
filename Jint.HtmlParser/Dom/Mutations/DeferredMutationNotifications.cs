namespace Jint.HtmlParser;

public sealed partial class Document
{
    private bool _mayCaptureHtmlMetaInsertions;
    internal bool MayCaptureHtmlMetaInsertions => _mayCaptureHtmlMetaInsertions;
    internal void MarkHtmlMetaCapturePresent() => _mayCaptureHtmlMetaInsertions = true;
    private MutationNotificationTicket? _pendingMutationHead;
    private MutationNotificationTicket? _pendingMutationTail;
    private bool _flushingMutationNotifications;
    internal bool DeferRangeScheduling { get; set; }
    private bool _pendingDeferredRangeSignal;
    // Each document supplies its own intrusive scheduling node. A failed native
    // operation can enlist source/selection documents without allocating a list.
    private Document? _rangeSignalOwner;
    private Document? _rangeSignalNext;
    private Document? _rangeSignalPrevious;
    private Document? _rangeSignalHead;
    private Document? _rangeSignalTail;

    internal void MarkDeferredRangeSignal(Document carrier)
    {
        _pendingDeferredRangeSignal = true;
        if (_rangeSignalOwner is not null) return;
        _rangeSignalOwner = carrier;
        _rangeSignalPrevious = carrier._rangeSignalTail;
        if (carrier._rangeSignalTail is { } tail) tail._rangeSignalNext = this;
        else carrier._rangeSignalHead = this;
        carrier._rangeSignalTail = this;
    }

    private void UnlinkRangeSignal()
    {
        if (_rangeSignalOwner is not { } owner) return;
        if (_rangeSignalPrevious is { } previous) previous._rangeSignalNext = _rangeSignalNext;
        else owner._rangeSignalHead = _rangeSignalNext;
        if (_rangeSignalNext is { } next) next._rangeSignalPrevious = _rangeSignalPrevious;
        else owner._rangeSignalTail = _rangeSignalPrevious;
        _rangeSignalOwner = _rangeSignalNext = _rangeSignalPrevious = null;
    }

    internal void ScheduleRangeChanges()
    {
        _pendingDeferredRangeSignal = false;
        UnlinkRangeSignal();
        PendingRangeChanges?.Invoke();
    }

    internal void PublishMutationNotifications(MutationNotificationTicket ticket)
    {
        if (_pendingMutationTail is null) _pendingMutationHead = ticket;
        else _pendingMutationTail.Next = ticket;
        _pendingMutationTail = ticket;
    }

    internal void FlushPendingMutationNotifications()
    {
        if (_flushingMutationNotifications) return;
        _flushingMutationNotifications = true;
        try
        {
            while (true)
            {
                // Mutation pending signals precede range scheduling, as on a successful Node entry.
                if (_pendingMutationHead is not { } ticket)
                {
                    if (_rangeSignalHead is { } target)
                    {
                        // Advance before the callback. Never flush another document's
                        // mutation tickets as a side effect of scheduling its ranges.
                        target.UnlinkRangeSignal();
                        if (target._pendingDeferredRangeSignal) target.ScheduleRangeChanges();
                        continue;
                    }
                    if (!_pendingDeferredRangeSignal) break;
                    ScheduleRangeChanges();
                    continue;
                }
                if (ticket.TryTakeNext(out var subscription))
                {
                    subscription!.NotifyIfPending();
                    continue;
                }
                _pendingMutationHead = ticket.Next;
                ticket.Next = null;
                if (_pendingMutationHead is null) _pendingMutationTail = null;
            }
        }
        finally { _flushingMutationNotifications = false; }
    }

    internal void ClearPendingMutationNotifications()
    {
        _pendingMutationHead = null;
        _pendingMutationTail = null;
        _pendingDeferredRangeSignal = false;
        UnlinkRangeSignal();
        while (_rangeSignalHead is { } target)
        {
            target.UnlinkRangeSignal();
            target._pendingDeferredRangeSignal = false;
        }
    }
}

internal sealed class MutationNotificationTicket(MutationMatches matches)
{
    private int _cursor;
    internal MutationNotificationTicket? Next { get; set; }
    internal bool TryTakeNext(out MutationSubscription? subscription)
    {
        if (_cursor == matches.Entries.Count) { subscription = null; return false; }
        subscription = matches.Entries[_cursor++].Subscription;
        return true;
    }
}
