using System.Runtime.InteropServices;
using Jint.HtmlParser;

namespace Jint.Browser.Runtime.Parsing;

internal sealed partial class ParserDriver
{
    private readonly Queue<Document> _pendingNativeDocuments = new();
    private readonly HashSet<Document> _pendingNativeDocumentSet = new(ReferenceEqualityComparer.Instance);
    private ResourceEnvelope? _activeResourceRecord;
    private int _activeMetaCursor;
    private bool _drainingResourceRecords;
    private bool _recoveringNativeNotifications;

    internal bool HasPendingNativeRecovery => !_disposed &&
        (_pendingNativeDocuments.Count != 0 || _activeResourceRecord is not null || _resourceRecords.Count != 0);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ResourceEnvelope(ResourceWatch Watch, MutationRecord Record, bool ImageDelegated);

    private void EnlistNativeNotifications(Document document)
    {
        if (_pendingNativeDocumentSet.Add(document)) _pendingNativeDocuments.Enqueue(document);
    }

    /// <summary>Recovers native scheduling only on a healthy, already budgeted page entry.</summary>
    internal void RecoverNativeMutationNotifications()
    {
        if (!HasPendingNativeRecovery || _recoveringNativeNotifications) return;
        _recoveringNativeNotifications = true;
        try
        {
            while (_pendingNativeDocuments.TryPeek(out var document))
            {
                _runtime.Dom.CancellationToken.ThrowIfCancellationRequested();
                _runtime.Engine.Constraints.Check();
                document.FlushPendingMutationNotifications();
                // No callback between successful flush and removal. A reentrant same-document
                // capture stays enlisted throughout the flush, which consumes its new tickets too.
                _pendingNativeDocuments.Dequeue();
                _pendingNativeDocumentSet.Remove(document);
            }
            DrainResourceRecords();
        }
        finally { _recoveringNativeNotifications = false; }
    }

    private bool TryActivateResourceRecord(out ResourceEnvelope entry)
    {
        if (_activeResourceRecord is { } active) { entry = active; return true; }
        if (!_resourceRecords.TryDequeue(out entry)) return false;
        _activeResourceRecord = entry;
        _activeMetaCursor = 0;
        return true;
    }

    private void ClearNativeRecovery()
    {
        foreach (var document in _pendingNativeDocuments) document.ClearPendingMutationNotifications();
        if (_activeResourceRecord is { } active) ClearCapturedDocuments(active);
        foreach (var entry in _resourceRecords) ClearCapturedDocuments(entry);
        _pendingNativeDocuments.Clear();
        _pendingNativeDocumentSet.Clear();
        _activeResourceRecord = null;
        _activeMetaCursor = 0;
    }

    private static void ClearCapturedDocuments(ResourceEnvelope entry)
    {
        entry.Watch.Document.ClearPendingMutationNotifications();
        foreach (var fact in entry.Record.HtmlMetaInsertions) fact.Document.ClearPendingMutationNotifications();
    }
}
