using System.Collections.Concurrent;
using System.Threading;

namespace Jint.Native.RegExp;

/// <summary>
/// How the .NET <see cref="System.Text.RegularExpressions.Regex"/> instances produced for JavaScript
/// patterns are code-generated. The trade-off (measured on .NET 10): a compiled regex matches
/// scan-heavy inputs roughly 2-16x faster than the built-in regex interpreter, but costs ~1.4 ms to
/// construct and JIT versus ~3 us interpreted, so compilation only pays off for reused patterns.
/// Under Native AOT the runtime silently ignores <see cref="System.Text.RegularExpressions.RegexOptions.Compiled"/>,
/// making all three modes equivalent to <see cref="Interpreted"/> there.
/// </summary>
internal enum RegexCompilation : byte
{
    /// <summary>Always use the .NET regex interpreter (explicit <c>CompileRegex = false</c>).</summary>
    Interpreted,

    /// <summary>
    /// Construct with <see cref="System.Text.RegularExpressions.RegexOptions.Compiled"/> eagerly
    /// (explicit <c>CompileRegex = true</c> and the prepared script/module default, where reuse is declared).
    /// </summary>
    Compiled,

    /// <summary>
    /// Interpret a pattern on its first constructions and upgrade to
    /// <see cref="System.Text.RegularExpressions.RegexOptions.Compiled"/> when the same pattern keeps
    /// being constructed — at that point <see cref="RegExpParseCache"/> has proven reuse, so the one-time
    /// compilation cost is amortized. One-shot patterns (cold-start scripts, data-driven
    /// <c>new RegExp(...)</c> floods) never pay it.
    /// </summary>
    Adaptive,
}

/// <summary>
/// Process-wide bounded cache of .NET <see cref="System.Text.RegularExpressions.Regex"/> adaptations
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
/// The cache also drives <see cref="RegexCompilation.Adaptive"/> tiering: the first construction of a
/// pattern populates the interpreted lane, and once enough further constructions of the same pattern
/// prove reuse it is upgraded to a compiled regex (dropping the interpreted entry).
/// </para>
/// <para>
/// The cache is bounded: once it reaches <see cref="Capacity"/> distinct entries it is cleared and rebuilt,
/// so a workload generating unboundedly many distinct patterns can never grow it without limit. Typical
/// scripts use a small, stable set of patterns and fill it once. A clear also resets adaptive reuse
/// tracking, which keeps distinct-pattern floods on the cheap interpreted path.
/// </para>
/// </summary>
internal static class RegExpParseCache
{
    // Bounded to cap retained compiled automata. Cleared (not LRU-evicted) on overflow: cheap, thread-safe,
    // and self-healing when the working set shifts. Real scripts rarely approach this many distinct regexes.
    private const int Capacity = 256;

    // Number of adaptive re-constructions (hits on an interpreted entry) required before upgrading to a
    // compiled regex, i.e. the upgrade happens on the (AdaptiveUpgradeThreshold + 1)th construction.
    // Deliberately above the two sightings produced by running the same one-shot script twice (e.g.
    // Test262's strict + sloppy dual runs of tests that eval tens of thousands of distinct literals,
    // which would otherwise mass-upgrade under parallel lockstep); genuinely hot patterns sail past it.
    private const int AdaptiveUpgradeThreshold = 2;

    private static readonly ConcurrentDictionary<Key, Entry> _cache = new();

    private readonly record struct Key(string Pattern, string Flags, bool Compiled, long TimeoutTicks);

    private sealed class Entry
    {
        public Entry(in RegExpParseResult result)
        {
            Result = result;
        }

        public readonly RegExpParseResult Result;

        /// <summary>Number of adaptive re-constructions observed since the entry was created.</summary>
        public int AdaptiveHits;
    }

    /// <summary>
    /// Returns a cached adaptation for the given regex inputs, adapting (and caching on success) on a miss.
    /// Only successful adaptations are cached; a failure means the pattern needs the custom engine, which the
    /// caller resolves per-realm. Callers must already have excluded custom-engine patterns
    /// (<see cref="RegExpConstructor.NeedCustomEngine"/>) before calling this.
    /// </summary>
    public static RegExpParseResult GetOrAdapt(string pattern, string flags, RegexCompilation compilation, TimeSpan timeout)
    {
        var compiledKey = new Key(pattern, flags, Compiled: true, timeout.Ticks);

        if (compilation == RegexCompilation.Compiled)
        {
            return _cache.TryGetValue(compiledKey, out var cached) ? cached.Result : Adapt(compiledKey);
        }

        var interpretedKey = compiledKey with { Compiled = false };

        if (compilation == RegexCompilation.Interpreted)
        {
            return _cache.TryGetValue(interpretedKey, out var cached) ? cached.Result : Adapt(interpretedKey);
        }

        // RegexCompilation.Adaptive
        if (_cache.TryGetValue(compiledKey, out var compiled))
        {
            return compiled.Result;
        }

        if (_cache.TryGetValue(interpretedKey, out var interpreted))
        {
            if (Interlocked.Increment(ref interpreted.AdaptiveHits) < AdaptiveUpgradeThreshold)
            {
                return interpreted.Result;
            }

            // Repeated construction of the same pattern: reuse is proven, so the one-time compilation cost
            // is amortized from here on. Upgrade and drop the now-redundant interpreted entry.
            var result = Adapt(compiledKey);
            if (!result.Success)
            {
                // Shouldn't happen (the same pattern already adapted successfully), but never trade a
                // working .NET adaptation for a custom-engine fallback.
                return interpreted.Result;
            }

            _cache.TryRemove(interpretedKey, out _);
            return result;
        }

        return Adapt(interpretedKey);
    }

    private static RegExpParseResult Adapt(in Key key)
    {
#pragma warning disable CS0618 // Tokenizer.AdaptRegExp is obsolete but is the supported adaptation entry point.
        var result = Tokenizer.AdaptRegExp(
            key.Pattern, key.Flags, key.Compiled, TimeSpan.FromTicks(key.TimeoutTicks), throwIfNotAdaptable: false,
            Engine.BaseParserOptions.EcmaVersion, Engine.BaseParserOptions.ExperimentalESFeatures);
#pragma warning restore CS0618

        if (result.Success)
        {
            if (_cache.Count >= Capacity)
            {
                _cache.Clear();
            }

            _cache.TryAdd(key, new Entry(result));
        }

        return result;
    }
}
