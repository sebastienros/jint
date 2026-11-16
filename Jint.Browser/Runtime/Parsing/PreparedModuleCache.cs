using Acornima.Ast;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>
/// Module scripts parsed once per process and shared by every page that imports them — what
/// <c>Jint.DevTools.PreparedScriptCache</c> does for classic scripts: an admin's modules are the same programs
/// on every page of a test suite, and a prepared module is shareable between engines.
/// </summary>
/// <remarks>
/// Keyed by the text, where it was loaded from (which a parse records as the module's location) and whether the
/// engine retains function source text; only an engine whose parsing is unconstrained uses it, as for scripts.
/// Bounded in source characters, least recently used first.
/// </remarks>
internal static class PreparedModuleCache
{
    /// <summary>Below this a parse costs less than the lookup's hash of the text.</summary>
    internal const int MinimumLength = 1024;

    /// <summary>The bound on the cached source, in UTF-16 code units.</summary>
    private const long MaxCachedCharacters = 16L * 1024 * 1024;

    private static readonly object Gate = new();
    private static readonly Dictionary<Key, LinkedListNode<Entry>> Entries = new();
    private static readonly LinkedList<Entry> Recency = new();
    private static long _characters;

    /// <summary>Answers the prepared form of <paramref name="code"/>, parsing it on the first request.</summary>
    /// <exception cref="ScriptPreparationException">The text does not parse; nothing is cached.</exception>
    internal static Prepared<Module> GetOrPrepare(string code, string source, bool retainSourceText)
    {
        var key = new Key(code, source, retainSourceText);
        lock (Gate)
        {
            if (Entries.TryGetValue(key, out var hit))
            {
                Recency.Remove(hit);
                Recency.AddFirst(hit);
                return hit.Value.Module;
            }
        }

        var prepared = Engine.PrepareModule(code, source, new ModulePreparationOptions
        {
            ParsingOptions = retainSourceText ? new ModuleParsingOptions { RetainFunctionSourceText = true } : ModuleParsingOptions.Default,
        });

        if (code.Length > MaxCachedCharacters)
        {
            return prepared;
        }

        lock (Gate)
        {
            if (Entries.TryGetValue(key, out var raced))
            {
                return raced.Value.Module;
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

    private readonly record struct Key(string Code, string Source, bool RetainSourceText);

    private readonly record struct Entry(Key Key, Prepared<Module> Module);
}
