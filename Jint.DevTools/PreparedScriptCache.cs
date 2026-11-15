using Acornima.Ast;

namespace Jint.DevTools;

/// <summary>
/// One parse per process of a script every page receives: an automation client's injected script, its helper
/// declarations, a framework bundle.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Prepared{TProgram}"/> is reusable across engines and safe to share between threads, which is
/// exactly what the engine asks a host to do with a script it runs repeatedly. Playwright evaluates a 329 KB
/// injected script in every new page, and a test suite loads the same bundle into every page it opens; both
/// were parsed again each time.
/// </para>
/// <para>
/// Scripts are prepared without static analysis, so a miss costs what the engine's own parse costs and each
/// engine rebuilds lazily what the analysis would have published. The key is everything that shapes the
/// parse: the source text, its name, the line it starts on, strict mode and whether function source text is
/// retained. A caller uses the cache only for an engine whose parsing options are otherwise the defaults.
/// </para>
/// <para>
/// Bounded by source length — a prepared script retains about fifteen bytes per character — and evicted least
/// recently used, so the scripts every page needs stay while one-off ones leave.
/// </para>
/// </remarks>
internal static class PreparedScriptCache
{
    /// <summary>Below this a parse costs less than the lookup's hash of the text; such scripts are not cached.</summary>
    internal const int MinimumLength = 1024;

    /// <summary>The bound on the cached source, in UTF-16 code units: about 30 MB of syntax trees.</summary>
    internal const long MaxCachedCharacters = 2L * 1024 * 1024;

    private static readonly TimeSpan DefaultRegexTimeout = new Options().Constraints.RegexTimeout;

    private static readonly object Gate = new();
    private static readonly Dictionary<Key, LinkedListNode<Entry>> Entries = new();
    private static readonly LinkedList<Entry> Recency = new();
    private static long _characters;

    /// <summary>How much source the cache currently holds; for tests.</summary>
    internal static long CachedCharacters
    {
        get
        {
            lock (Gate)
            {
                return _characters;
            }
        }
    }

    /// <summary>
    /// Whether an engine built from <paramref name="options"/> parses exactly as a cached script was parsed.
    /// </summary>
    /// <remarks>
    /// Source and node limits and a regular-expression timeout are part of what an engine's own parse applies;
    /// an engine that sets any of them parses for itself, so a cached tree never carries another engine's
    /// limits or bypasses its own. Strict mode and source-text retention are in the key instead.
    /// </remarks>
    internal static bool Suits(Options options)
        => options.Parsing.MaxSourceLength is null
            && options.Parsing.MaxNodeCount is null
            && options.Constraints.RegexTimeout == DefaultRegexTimeout;

    /// <summary>Answers the prepared form of <paramref name="code"/>, parsing it on the first request.</summary>
    /// <param name="code">The source text.</param>
    /// <param name="source">The name stack traces and the debugger know it by, or <see langword="null"/>.</param>
    /// <param name="line">The one-based line of the enclosing document the text starts on; 1 or less for none.</param>
    /// <param name="strict">Whether the engine runs its scripts in strict mode.</param>
    /// <param name="retainSourceText">Whether the engine retains function source text.</param>
    /// <exception cref="ScriptPreparationException">The text does not parse; nothing is cached.</exception>
    internal static Prepared<Script> GetOrPrepare(string code, string? source, int line, bool strict, bool retainSourceText)
    {
        var key = new Key(code, source, Math.Max(line, 1), strict, retainSourceText);
        lock (Gate)
        {
            if (Entries.TryGetValue(key, out var hit))
            {
                Recency.Remove(hit);
                Recency.AddFirst(hit);
                return hit.Value.Script;
            }
        }

        var parsing = retainSourceText ? new ScriptParsingOptions { RetainFunctionSourceText = true } : ScriptParsingOptions.Default;
        if (key.Line > 1)
        {
            parsing = parsing with { SourceOffset = Acornima.Position.From(key.Line, 0) };
        }

        var prepared = Engine.PrepareScript(code, source, strict, new ScriptPreparationOptions
        {
            ParsingOptions = parsing,
            StaticAnalysis = false,
        });

        if (code.Length > MaxCachedCharacters)
        {
            return prepared;
        }

        lock (Gate)
        {
            if (Entries.TryGetValue(key, out var raced))
            {
                return raced.Value.Script;
            }

            while (_characters + code.Length > MaxCachedCharacters && Recency.Last is { } oldest)
            {
                Recency.RemoveLast();
                Entries.Remove(oldest.Value.Key);
                _characters -= oldest.Value.Key.Code.Length;
            }

            Entries.Add(key, Recency.AddFirst(new Entry(key, prepared)));
            _characters += code.Length;
        }

        return prepared;
    }

    /// <summary>Forgets every cached script; for tests.</summary>
    internal static void Clear()
    {
        lock (Gate)
        {
            Entries.Clear();
            Recency.Clear();
            _characters = 0;
        }
    }

    private readonly record struct Key(string Code, string? Source, int Line, bool Strict, bool RetainSourceText);

    private readonly record struct Entry(Key Key, Prepared<Script> Script);
}
