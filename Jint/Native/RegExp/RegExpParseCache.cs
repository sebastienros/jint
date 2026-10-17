using System.Collections.Concurrent;
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
/// timeout is read from the engine at match time rather than baked in.
/// </para>
/// <para>
/// The cache is bounded: once it reaches <see cref="Capacity"/> distinct entries it is cleared and rebuilt,
/// so a workload generating unboundedly many distinct patterns can never grow it without limit. Typical
/// scripts use a small, stable set of patterns and fill it once.
/// </para>
/// </summary>
internal static class RegExpParseCache
{
    // Cleared (not LRU-evicted) on overflow: cheap, thread-safe, and self-healing when the working set shifts.
    private const int Capacity = 256;

    private static readonly ConcurrentDictionary<Key, JintRegExpEngine> _cache = new();

    private readonly record struct Key(string Pattern, string Flags);

    /// <summary>
    /// Returns the cached matcher for <paramref name="pattern"/> and <paramref name="flags"/>, compiling (and
    /// caching) it on a miss. An invalid pattern or flags string throws the <c>SyntaxError</c> and is not cached.
    /// </summary>
    public static JintRegExpEngine GetOrCompile(Realm realm, string pattern, string flags)
    {
        var key = new Key(pattern, flags);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var compiled = RegExpConstructor.Compile(realm, pattern, flags);
        if (_cache.Count >= Capacity)
        {
            _cache.Clear();
        }

        _cache.TryAdd(key, compiled);
        return compiled;
    }
}
