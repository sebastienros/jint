using Jint.Native;
using Jint.Runtime;

namespace Jint.NodeCompat;

/// <summary>
/// The registry of <c>node:</c> builtin modules Jint provides: which specifiers name one, and what each one
/// exports.
/// </summary>
/// <remarks>
/// <para>
/// The line drawn here is the whole design. Everything provided is a <b>pure string utility</b> — path
/// arithmetic, query-string encoding, URL parsing — computable from its arguments alone, with no file system,
/// no process, no network and no clock behind it. Everything that would need one of those is deliberately
/// absent rather than stubbed: <c>node:fs</c>, <c>node:buffer</c>, <c>node:crypto</c>, <c>node:os</c>,
/// <c>node:child_process</c>, <c>node:http</c>, <c>node:net</c>, <c>node:worker_threads</c>. An absent module
/// is a better answer than a throwing one, because a package that feature-detects can take its other branch.
/// </para>
/// <para>
/// A host that wants one of the absent names supplies it itself: a module registered under it with
/// <c>Engine.Modules.Add</c> is found before this registry is consulted, for the prefixed and un-prefixed
/// spellings alike.
/// </para>
/// </remarks>
internal static class NodeBuiltinModules
{
    internal const string Prefix = "node:";

    private const string PathModule = "node:path";
    private const string PathPosixModule = "node:path/posix";
    private const string PathWin32Module = "node:path/win32";
#if NET8_0_OR_GREATER
    private const string QueryStringModule = "node:querystring";
    private const string UrlModule = "node:url";
#endif

    /// <summary>
    /// The provided names, in the order the failure message lists them. Which entries exist depends on the
    /// target framework: the two URL-based modules are built on the engine's WHATWG URL implementation, which
    /// needs .NET 8.
    /// </summary>
    private static readonly string[] _names =
    [
        PathModule,
        PathPosixModule,
        PathWin32Module,
#if NET8_0_OR_GREATER
        QueryStringModule,
        UrlModule,
#endif
    ];

    /// <summary>The provided names as a message fragment: <c>a, b and c</c>.</summary>
    internal static string AvailableNames { get; } = FormatNames(_names);

    /// <summary>
    /// Whether <paramref name="specifier"/> names a builtin, and under which canonical <c>node:</c> key.
    /// </summary>
    /// <param name="specifier">The specifier as written in the import.</param>
    /// <param name="allowUnprefixed">
    /// Whether the un-prefixed spelling counts. Both spellings canonicalize to the same
    /// <c>node:</c> key, so they name one module record however the importer wrote it.
    /// </param>
    /// <param name="canonical">The canonical key, or null.</param>
    internal static bool TryCanonicalize(string specifier, bool allowUnprefixed, out string? canonical)
    {
        for (var i = 0; i < _names.Length; i++)
        {
            var name = _names[i];
            if (string.Equals(specifier, name, StringComparison.Ordinal))
            {
                canonical = name;
                return true;
            }

            if (allowUnprefixed
                && specifier.Length == name.Length - Prefix.Length
                && string.CompareOrdinal(specifier, 0, name, Prefix.Length, specifier.Length) == 0)
            {
                canonical = name;
                return true;
            }
        }

        canonical = null;
        return false;
    }

    /// <summary>Whether <paramref name="specifier"/> uses the <c>node:</c> scheme at all.</summary>
    internal static bool IsNodeScheme(string specifier)
        => specifier.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// The exports of one builtin: its named exports, plus a <c>default</c> carrying the module object a
    /// CommonJS <c>require</c> would have produced.
    /// </summary>
    internal static List<KeyValuePair<string, JsValue>> CreateExports(
        Engine engine,
        NodeBuiltinModuleConfiguration configuration,
        string canonicalName)
    {
        switch (canonicalName)
        {
            case PathModule:
                return NodePathModule.CreateExports(engine, configuration, NodePathFlavor.Platform);
            case PathPosixModule:
                return NodePathModule.CreateExports(engine, configuration, NodePathFlavor.Posix);
            case PathWin32Module:
                return NodePathModule.CreateExports(engine, configuration, NodePathFlavor.Win32);
#if NET8_0_OR_GREATER
            case QueryStringModule:
                return NodeQueryStringModule.CreateExports(engine);
            case UrlModule:
                return NodeUrlModule.CreateExports(engine, configuration);
#endif
            default:
                Throw.InvalidOperationException($"'{canonicalName}' is not a Node builtin module Jint provides.");
                return null!;
        }
    }

    private static string FormatNames(string[] names)
    {
        if (names.Length == 1)
        {
            return names[0];
        }

        return string.Join(", ", names, 0, names.Length - 1) + " and " + names[names.Length - 1];
    }
}
