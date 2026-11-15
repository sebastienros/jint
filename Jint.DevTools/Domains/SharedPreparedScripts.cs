using Acornima.Ast;

namespace Jint.DevTools.Domains;

/// <summary>
/// A process-wide cache of parsed scripts, shared by every engine: a <see cref="Prepared{TProgram}"/> is
/// reusable across engines and thread-safe, so a script every page receives — an automation client's
/// injected script, a framework bundle — is parsed once per process rather than once per page.
/// </summary>
/// <remarks>
/// Prepared without static analysis, so a miss costs what the engine's own parse costs; the per-engine state
/// static analysis would publish is rebuilt lazily by each engine instead. Bounded by total source length,
/// cleared wholesale when the bound is reached.
/// </remarks>
internal static class SharedPreparedScripts
{
    /// <summary>Below this a parse is cheaper than the bookkeeping; console expressions stay uncached.</summary>
    internal const int MinimumLength = 1024;

    private const long MaxCachedCharacters = 64L * 1024 * 1024;

    private static readonly object Gate = new();
    private static readonly Dictionary<Key, Prepared<Script>> Cache = new();
    private static long _characters;

    internal static Prepared<Script> GetOrPrepare(string code, string? source, int line, bool strict, bool retainSourceText)
    {
        var key = new Key(code, source, line, strict, retainSourceText);
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var parsing = retainSourceText ? new ScriptParsingOptions { RetainFunctionSourceText = true } : ScriptParsingOptions.Default;
        if (line > 1)
        {
            parsing = parsing with { SourceOffset = Acornima.Position.From(line, 0) };
        }

        var prepared = Engine.PrepareScript(code, source, strict, new ScriptPreparationOptions
        {
            ParsingOptions = parsing,
            StaticAnalysis = false,
        });

        lock (Gate)
        {
            if (_characters + code.Length > MaxCachedCharacters)
            {
                Cache.Clear();
                _characters = 0;
            }

            if (Cache.TryAdd(key, prepared))
            {
                _characters += code.Length;
            }
        }

        return prepared;
    }

    private readonly record struct Key(string Code, string? Source, int Line, bool Strict, bool RetainSourceText);
}
