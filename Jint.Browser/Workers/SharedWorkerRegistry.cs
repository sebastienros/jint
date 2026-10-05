using System.Runtime.InteropServices;
using Jint.WebApi;
using Jint.WebApi.Messaging;

namespace Jint.Browser.Workers;

/// <summary>The context's shared worker manager, holding only host handles, endpoints and scalar identity.</summary>
/// <remarks>
/// https://html.spec.whatwg.org/multipage/workers.html#shared-worker-manager
/// All registry state is locked; no script or engine construction runs under the lock.
/// The last document's departure removes the entry and terminates immediately, with no grace period.
/// </remarks>
internal sealed class SharedWorkerRegistry
{
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<SharedWorkerKey, SharedWorkerEntry> _entries = [];
    private readonly HashSet<SharedWorkerEntry> _live = [];

    internal int LiveCount
    {
        get { lock (_gate) return _live.Count; }
    }

    internal void ReportError(SharedWorkerEntry entry, Exception exception, string name)
    {
        Action<Exception, string>[] reporters;
        lock (_gate)
        {
            reporters = entry.Clients.DistinctBy(client => client.Owner)
                .Select(client => client.ReportError).OfType<Action<Exception, string>>().ToArray();
        }
        foreach (var report in reporters) report(exception, name);
    }

    internal SharedWorkerEntry? Connect(SharedWorkerKey key, SharedWorkerSettings settings,
        SharedWorkerClient client, int limit, out bool created, out bool quota)
    {
        lock (_gate)
        {
            created = false;
            quota = false;
            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.Settings != settings) return null;
            }
            else
            {
                // Bound unique shared globals across the context, not the number of connections.
                if (_live.Count >= limit)
                {
                    quota = true;
                    return null;
                }
                entry = new SharedWorkerEntry(key, settings);
                _entries.Add(key, entry);
                _live.Add(entry);
                created = true;
            }
            entry.Clients.Add(client);
            if (entry.Ready) entry.Run!.QueueConnect(client.Inner);
            return entry;
        }
    }

    internal bool Publish(SharedWorkerEntry entry, SharedWorkerRun run)
    {
        lock (_gate)
        {
            if (entry.Ended) return false;
            entry.Run = run;
            return true;
        }
    }

    internal void Ready(SharedWorkerEntry entry)
    {
        lock (_gate)
        {
            if (entry.Ended) return;
            entry.Ready = true;
            foreach (var client in entry.Clients) entry.Run!.QueueConnect(client.Inner);
        }
    }

    internal void Release(object owner)
    {
        List<SharedWorkerClient> clients = [];
        List<SharedWorkerEntry> abandoned = [];
        lock (_gate)
        {
            foreach (var entry in _live.ToArray())
            {
                for (var i = entry.Clients.Count - 1; i >= 0; i--)
                {
                    if (!ReferenceEquals(entry.Clients[i].Owner, owner)) continue;
                    clients.Add(entry.Clients[i]);
                    entry.Clients.RemoveAt(i);
                }
                if (entry.Clients.Count == 0)
                {
                    Remove(entry);
                    _live.Remove(entry);
                    abandoned.Add(entry);
                }
            }
        }
        foreach (var client in clients) client.Close(drain: false);
        foreach (var entry in abandoned) entry.Run?.Connection.End();
    }

    internal void End(SharedWorkerEntry entry, WorkerEndReason reason)
    {
        SharedWorkerClient[] clients;
        lock (_gate)
        {
            Remove(entry);
            _live.Remove(entry);
            clients = entry.Clients.ToArray();
            entry.Clients.Clear();
        }
        foreach (var client in clients)
        {
            if (reason == WorkerEndReason.StartupFailed) client.Fail();
            else client.Close(drain: reason == WorkerEndReason.ClosedByWorker);
        }
    }

    // Also called synchronously by close(), before the running turn finishes.
    internal void BeginClose(SharedWorkerEntry entry)
    {
        lock (_gate)
        {
            Remove(entry);
            foreach (var client in entry.Clients) client.Inner.Close();
        }
    }

    private void Remove(SharedWorkerEntry entry)
    {
        entry.Ended = true;
        if (_entries.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, entry))
        {
            _entries.Remove(entry.Key);
        }
    }
}

[StructLayout(LayoutKind.Auto)]
internal readonly record struct SharedWorkerKey(string Origin, string Url, string Name, object? OpaqueOwner);

[StructLayout(LayoutKind.Auto)]
internal readonly record struct SharedWorkerSettings(string Name, string Type, string Credentials, bool ExtendedLifetime);

internal sealed class SharedWorkerEntry(SharedWorkerKey key, SharedWorkerSettings settings)
{
    internal SharedWorkerKey Key { get; } = key;
    internal SharedWorkerSettings Settings { get; } = settings;
    internal List<SharedWorkerClient> Clients { get; } = [];
    internal SharedWorkerRun? Run { get; set; }
    internal bool Ready { get; set; }
    internal bool Ended { get; set; }
}

/// <summary>Engine-free connection data. The error callback only posts to its owning document's queue.</summary>
internal sealed class SharedWorkerClient(object owner, MessagePortEndpoint outer, MessagePortEndpoint inner, Action error,
    Action<Exception, string>? reportError = null)
{
    private Action? _error = error;
    internal object Owner { get; } = owner;
    internal Action<Exception, string>? ReportError { get; } = reportError;
    internal MessagePortEndpoint Outer { get; } = outer;
    internal MessagePortEndpoint Inner { get; } = inner;

    internal void Fail()
    {
        Interlocked.Exchange(ref _error, null)?.Invoke();
        Close(drain: false);
    }

    internal void Close(bool drain)
    {
        Interlocked.Exchange(ref _error, null);
        Inner.Close();
        if (drain) Outer.BeginDrainThenClose();
        else Outer.Close();
    }
}
