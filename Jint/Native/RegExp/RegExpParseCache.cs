using System.Collections.Concurrent;

namespace Jint.Native.RegExp;

/// <summary>
/// Process-wide bounded cache of compiled .NET <see cref="System.Text.RegularExpressions.Regex"/> adaptations
/// (wrapped in Acornima's <c>RegExpParseResult</c>), keyed by the inputs that fully determine the result:
/// pattern, flags, the compiled-codegen flag and the match timeout.
/// <para>
/// Regex literals already cache their adaptation on the AST node, but that cache only helps a reused
/// <c>Prepared&lt;Script&gt;</c>; running a script from source (fresh parse, fresh Engine) re-adapts the same
/// literal every time, and dynamic <c>new RegExp(...)</c> has no per-node cache at all. A shared cache closes
/// that gap because a successful adaptation is a pure function of the key and the produced Regex is immutable
/// and thread-safe (match state lives on the JS RegExp instance / returned Match, not on the Regex).
/// </para>
/// <para>
/// The cache is bounded: once it reaches <see cref="Capacity"/> distinct entries it is cleared and rebuilt,
/// so a workload generating unboundedly many distinct patterns can never grow it without limit. Typical
/// scripts use a small, stable set of patterns and fill it once.
/// </para>
/// </summary>
internal static class RegExpParseCache
{
    // Bounded to cap retained compiled automata. Cleared (not LRU-evicted) on overflow: cheap, thread-safe,
    // and self-healing when the working set shifts. Real scripts rarely approach this many distinct regexes.
    private const int Capacity = 256;

    private static readonly ConcurrentDictionary<Key, RegExpParseResult> _cache = new();

    private readonly record struct Key(string Pattern, string Flags, bool Compiled, long TimeoutTicks);

    /// <summary>
    /// Returns a cached adaptation for the given regex inputs, adapting (and caching on success) on a miss.
    /// Only successful adaptations are cached; a failure means the pattern needs the custom engine, which the
    /// caller resolves per-realm. Callers must already have excluded custom-engine patterns
    /// (<see cref="RegExpConstructor.NeedCustomEngine"/>) before calling this.
    /// </summary>
    public static RegExpParseResult GetOrAdapt(string pattern, string flags, bool compiled, TimeSpan timeout)
    {
        var key = new Key(pattern, flags, compiled, timeout.Ticks);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

#pragma warning disable CS0618 // Tokenizer.AdaptRegExp is obsolete but is the supported adaptation entry point.
        var result = Tokenizer.AdaptRegExp(
            pattern, flags, compiled, timeout, throwIfNotAdaptable: false,
            Engine.BaseParserOptions.EcmaVersion, Engine.BaseParserOptions.ExperimentalESFeatures);
#pragma warning restore CS0618

        if (result.Success)
        {
            if (_cache.Count >= Capacity)
            {
                _cache.Clear();
            }

            _cache.TryAdd(key, result);
        }

        return result;
    }
}
