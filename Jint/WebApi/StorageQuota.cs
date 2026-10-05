#if NET8_0_OR_GREATER
using System.Threading;

namespace Jint.WebApi;

/// <summary>Shared retained-data budget: https://storage.spec.whatwg.org/#storage-quota.</summary>
/// <remarks>Store locks precede this lock. This class never calls a store or an engine.</remarks>
internal sealed class StorageQuota(long maxBytes)
{
    private readonly Lock _gate = new();
    private readonly List<Entry> _entries = [];
    private long _bytes;
    private int _registrations;

    internal long MaxBytes => maxBytes;

    internal bool TryResize(Account account, long bytes, out long requested)
    {
        lock (_gate)
        {
            var previous = account.Entry?.Bytes ?? 0;
            var delta = bytes - previous;
            if (delta > maxBytes - _bytes || _registrations >= Math.Max(256, _entries.Count / 2))
            {
                // Deleted Cache objects remain usable. Charge live handles, but reclaim dead ones
                // before refusing a write, without finalizers or retaining their contents.
                var retained = 0;
                for (var i = 0; i < _entries.Count; i++)
                {
                    var candidate = _entries[i];
                    if (candidate.Owner.TryGetTarget(out _))
                    {
                        _entries[retained++] = candidate;
                    }
                    else
                    {
                        _bytes -= candidate.Bytes;
                    }
                }
                _entries.RemoveRange(retained, _entries.Count - retained);
                _registrations = 0;
            }
            requested = delta > long.MaxValue - _bytes ? long.MaxValue : _bytes + delta;
            if (delta > maxBytes - _bytes) return false;
            if (account.Entry is null && bytes != 0)
            {
                account.Entry = new Entry(account);
                _entries.Add(account.Entry);
                _registrations++;
            }
            if (account.Entry is { } entry) entry.Bytes = bytes;
            _bytes = requested;
            return true;
        }
    }

    internal sealed class Account
    {
        internal Entry? Entry;
    }

    internal sealed class Entry(Account owner)
    {
        internal readonly WeakReference<Account> Owner = new(owner);
        internal long Bytes;
    }
}
#endif
