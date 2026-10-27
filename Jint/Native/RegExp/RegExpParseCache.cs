using System.Collections.Concurrent;
using System.Threading;
using Jint.Runtime;
using Jint.Runtime.RegExp;

namespace Jint.Native.RegExp;

/// <summary>
/// Process-wide bounded cache of compiled <see cref="JintRegExpEngine"/> matchers, keyed by pattern and flags.
/// <para>
/// Regex literals already memoize their matcher on the AST node, but that only helps a reused
/// <c>Prepared&lt;Script&gt;</c>; running a script from source (fresh parse, fresh Engine) recompiles the same
/// literal every time, and dynamic <c>new RegExp(...)</c> has no per-node memo at all. A shared cache closes
/// that gap because a compilation is a pure function of the key and the matcher is immutable and thread-safe:
/// match state lives on the JS RegExp instance and in per-thread scratch, never on the matcher, and the match
/// timeout is read from the engine at match time rather than baked in. The regex timeout only bounds the work
/// a compile may do (see <see cref="JintRegExpEngine.Compile"/>), so it is not part of the key: a matcher
/// another engine already paid to compile costs this one nothing, whatever its budget, and a compile that
/// outran its budget is not cached, so it cannot poison a later engine with a larger one.
/// </para>
/// <para>
/// A pattern is cached the second time it is compiled, not the first. Typical scripts use a small, stable set
/// of patterns, which then costs one extra compile per pattern per process; a script building patterns from
/// data compiles each once and never fills the cache with matchers nothing will ask for again, which is what
/// made every such compile pay for the cache (the locks behind <c>ConcurrentDictionary.Count</c>, a clear and
/// regrow every few hundred patterns, and a cache full of dead matchers kept alive into the older generations).
/// </para>
/// <para>
/// The cache is bounded by generations: once the current one holds <see cref="Capacity"/> entries it becomes
/// the previous one, which is still consulted and whose hits are re-admitted, so a working set that outgrows
/// one generation is not recompiled wholesale; a workload generating unboundedly many distinct patterns can
/// never hold more than two generations' worth.
/// </para>
/// </summary>
internal static class RegExpParseCache
{
    internal const int Capacity = 256;

    // Every pattern's hash goes into a slot of this table on its first compile; finding its own hash already
    // there is what admits it. Collisions in either direction are benign: a false admit caches one pattern that
    // was seen once, a false evict costs one more compile before the pattern is cached.
    private const int SeenSlots = 4096;
    private static readonly int[] _seen = new int[SeenSlots];

    private static readonly int _concurrencyLevel = System.Math.Max(2, Environment.ProcessorCount);

    private static ConcurrentDictionary<Key, JintRegExpEngine> _current = NewGeneration();
    private static ConcurrentDictionary<Key, JintRegExpEngine>? _previous;
    private static int _count;

    private readonly record struct Key(string Pattern, string Flags);

    /// <summary>
    /// Returns the cached matcher for <paramref name="pattern"/> and <paramref name="flags"/>, compiling it on a
    /// miss and caching it once the pattern has been seen twice. An invalid pattern or flags string throws the
    /// <c>SyntaxError</c> and is not cached, nor is a compile that outran <paramref name="timeout"/> (see
    /// <see cref="JintRegExpEngine.Compile"/>).
    /// </summary>
    public static JintRegExpEngine GetOrCompile(Realm realm, string pattern, string flags, TimeSpan timeout)
    {
        var key = new Key(pattern, flags);
        var current = _current;
        if (current.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var previous = _previous;
        if (previous is not null && previous.TryGetValue(key, out cached))
        {
            // Still in use across a generation boundary: carry it forward, so the working set survives the swap.
            Admit(current, key, cached);
            return cached;
        }

        var compiled = RegExpConstructor.Compile(realm, pattern, flags, timeout);

        var hash = key.GetHashCode();
        ref var slot = ref _seen[hash & (SeenSlots - 1)];
        if (slot != hash)
        {
            // First sighting: remember it, hand the matcher out uncached so a one-shot pattern dies young.
            slot = hash;
            return compiled;
        }

        Admit(current, key, compiled);
        return compiled;
    }

    private static void Admit(ConcurrentDictionary<Key, JintRegExpEngine> generation, Key key, JintRegExpEngine matcher)
    {
        if (!generation.TryAdd(key, matcher))
        {
            return;
        }

        // The count is approximate under contention (a few adders may race past the capacity into the generation
        // being retired, or into the new one before the count resets), which bounds the cache at Capacity plus a
        // handful of entries per generation rather than exactly Capacity; exactness would cost the lock the count
        // replaces. Exactly one adder observes the crossing and retires whatever generation is current, which is
        // the right one even when that adder's own entry went into an already retired generation.
        if (Interlocked.Increment(ref _count) == Capacity)
        {
            _previous = _current;
            _current = NewGeneration();
            Volatile.Write(ref _count, 0);
        }
    }

    private static ConcurrentDictionary<Key, JintRegExpEngine> NewGeneration() => new(_concurrencyLevel, Capacity);
}
