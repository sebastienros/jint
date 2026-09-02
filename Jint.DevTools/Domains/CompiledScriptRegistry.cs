using System.Globalization;
using Acornima.Ast;

namespace Jint.DevTools.Domains;

/// <summary>
/// The parsed scripts one target keeps: the ones <c>Runtime.compileScript</c> persisted, and the function
/// declarations <c>Runtime.callFunctionOn</c> has been sent.
/// </summary>
/// <remarks>
/// <para>
/// Two caches rather than one because they end differently. A persisted script is addressed by an
/// identifier the client holds, so it lives until the target does; a function declaration is addressed by
/// its own text, so it is a pure cache and evicting one costs a re-parse and nothing else.
/// </para>
/// <para>
/// The declaration cache is what makes the client path cheap rather than merely correct: a recorded
/// Puppeteer or Playwright run sends <c>Runtime.callFunctionOn</c> between 32 and 53 times, and the same
/// handful of declarations over and over. A <see cref="Prepared{TProgram}"/> is reusable and thread-safe, so
/// caching one is exactly what the engine asks callers to do.
/// </para>
/// <para>
/// Locked rather than engine-thread-only: both dictionaries are ordinary CLR state, and a target may be
/// spoken to by more than one attachment.
/// </para>
/// </remarks>
internal sealed class CompiledScriptRegistry
{
    /// <summary>
    /// How many distinct function declarations are kept. A client sends a small, fixed set of them — its own
    /// helper functions — so the bound is there for the client that generates a new one every call rather
    /// than for the one that behaves.
    /// </summary>
    private const int MaxCachedDeclarations = 128;

    private readonly object _gate = new();
    private readonly Dictionary<string, Prepared<Script>> _persisted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Prepared<Script>> _declarations = new(StringComparer.Ordinal);

    private int _next;

    /// <summary>Keeps <paramref name="script"/> and mints the identifier <c>Runtime.runScript</c> names it by.</summary>
    internal string Persist(in Prepared<Script> script)
    {
        lock (_gate)
        {
            var id = (++_next).ToString(CultureInfo.InvariantCulture);
            _persisted.Add(id, script);
            return id;
        }
    }

    /// <summary>Answers the script <paramref name="scriptId"/> names, or whether there is one.</summary>
    internal bool TryGetPersisted(string scriptId, out Prepared<Script> script)
    {
        lock (_gate)
        {
            return _persisted.TryGetValue(scriptId, out script);
        }
    }

    /// <summary>
    /// Answers the parsed form of one <c>callFunctionOn</c> declaration, parsing it the first time.
    /// </summary>
    /// <param name="declaration">The declaration exactly as the client sent it.</param>
    /// <returns>A script whose one expression evaluates to the function.</returns>
    /// <exception cref="ScriptPreparationException">The declaration is not parseable.</exception>
    internal Prepared<Script> Declaration(string declaration)
    {
        lock (_gate)
        {
            if (_declarations.TryGetValue(declaration, out var cached))
            {
                return cached;
            }
        }

        // Parenthesised so that `function () {}` is an expression rather than a declaration, and with the
        // closing parenthesis on its own line so that a declaration ending in `//# sourceURL=` -- which is
        // what every one of the recorded clients appends -- does not comment it out.
        var prepared = Engine.PrepareScript("(" + declaration + "\n)");

        lock (_gate)
        {
            if (_declarations.Count >= MaxCachedDeclarations)
            {
                // Wholesale rather than least-recently-used: a client sends a fixed set of declarations, so
                // reaching the bound means the set is not fixed and no eviction policy would help it.
                _declarations.Clear();
            }

            _declarations[declaration] = prepared;
        }

        return prepared;
    }
}
