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
    internal void MarkDeferredRangeSignal() => _pendingDeferredRangeSignal = true;

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
                    if (!_pendingDeferredRangeSignal) break;
                    _pendingDeferredRangeSignal = false;
                    PendingRangeChanges?.Invoke();
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
