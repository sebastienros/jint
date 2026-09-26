using System.Collections.ObjectModel;

namespace Jint.HtmlParser;

/// <summary>A reusable, explicitly drained native mutation observer.</summary>
public sealed class MutationSubscription : IDisposable
{
    private readonly List<MutationRegistration> _registrations = [];
    private List<WeakReference<Node>>? _transientNodes;
    private List<MutationRecord>? _records;
    private bool _disposed;

    internal MutationSubscription() { }

    // Trusted host scheduling only; the native mutation stack never invokes script.
    internal Action<MutationSubscription>? PendingRecord { get; set; }
    private bool _captureHtmlMetaInsertions;
    internal bool CaptureHtmlMetaInsertions
    {
        get => _captureHtmlMetaInsertions;
        set
        {
            _captureHtmlMetaInsertions = value;
            if (!value) return;
            foreach (var registration in _registrations)
                if (registration.Target.TryGetTarget(out var target))
                    (target as Document ?? target.OwnerDocument!).MarkHtmlMetaCapturePresent();
        }
    }
    // Trusted budget facts for one native preparation only; no author code or DOM reentry.
    internal Func<(Action<int>? Checkpoint, CancellationToken Token)>? CreateCaptureWork { get; set; }

    public void Observe(Node target, MutationObserverOptions options)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(target);
        var normalized = NormalizedMutationOptions.Create(options);
        foreach (var registration in _registrations)
        {
            if (!registration.Target.TryGetTarget(out var existing) || !ReferenceEquals(existing, target))
            {
                continue;
            }

            RemoveTransients(registration);
            registration.Options = normalized;
            return;
        }

        var created = new MutationRegistration(this, target, normalized);
        _registrations.Add(created);
        target.AddMutationRegistration(created, transient: false);
    }

    public IReadOnlyList<MutationRecord> TakeRecords()
    {
        ThrowIfDisposed();
        return Drain();
    }

    public IReadOnlyList<MutationRecord> TakeRecordsForDelivery()
    {
        ThrowIfDisposed();
        var records = Drain();
        RemoveTransients(null);
        return records;
    }

    public void Disconnect()
    {
        foreach (var registration in _registrations)
        {
            if (registration.Target.TryGetTarget(out var target))
            {
                target.RemoveMutationRegistration(registration, transient: false);
            }
        }

        _registrations.Clear();
        RemoveTransients(null);
        _records = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            PendingRecord = null;
            CreateCaptureWork = null;
            CaptureHtmlMetaInsertions = false;
            _disposed = true;
        }
    }

    internal void AddTransientNode(Node node)
        => (_transientNodes ??= []).Add(new WeakReference<Node>(node));

    internal void Enqueue(MutationRecord record) => (_records ??= []).Add(record);

    internal void NotifyIfPending()
    {
        // An earlier subscription may have drained or disconnected this one, including
        // during a nested mutation. Do not send a stale trailing pending signal.
        if (_records is { Count: > 0 }) PendingRecord?.Invoke(this);
    }

    private ReadOnlyCollection<MutationRecord> Drain()
    {
        var records = _records;
        _records = null;
        return records is null || records.Count == 0
            ? Array.AsReadOnly(Array.Empty<MutationRecord>())
            : Array.AsReadOnly(records.ToArray());
    }

    private void RemoveTransients(MutationRegistration? source)
    {
        if (_transientNodes is null)
        {
            return;
        }

        for (var i = _transientNodes.Count - 1; i >= 0; i--)
        {
            if (!_transientNodes[i].TryGetTarget(out var node))
            {
                _transientNodes.RemoveAt(i);
                continue;
            }

            node.RemoveTransientRegistrations(this, source);
            if (!node.HasTransientRegistration(this))
            {
                _transientNodes.RemoveAt(i);
            }
        }

        if (_transientNodes.Count == 0)
        {
            _transientNodes = null;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

internal sealed class MutationRegistration(MutationSubscription subscription, Node target, NormalizedMutationOptions options)
{
    internal MutationSubscription Subscription { get; } = subscription;
    internal WeakReference<Node> Target { get; } = new(target);
    internal NormalizedMutationOptions Options { get; set; } = options;
}
