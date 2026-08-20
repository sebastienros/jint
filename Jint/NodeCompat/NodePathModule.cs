using Jint.Native;
using Jint.Runtime;

namespace Jint.NodeCompat;

/// <summary>Which flavour of <c>node:path</c> a module exposes.</summary>
internal enum NodePathFlavor
{
    /// <summary><c>node:path</c>, which follows the configured platform.</summary>
    Platform,

    /// <summary><c>node:path/posix</c>.</summary>
    Posix,

    /// <summary><c>node:path/win32</c>.</summary>
    Win32,
}

/// <summary>
/// Builds the JavaScript surface of <c>node:path</c>, <c>node:path/posix</c> and <c>node:path/win32</c> over
/// <see cref="NodePosixPath"/> and <see cref="NodeWin32Path"/>.
/// <para>
/// https://nodejs.org/api/path.html
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Both flavours are built whichever module was imported, because each one exposes the other: Node's
/// <c>path.posix.win32 === path.win32</c> and <c>path.win32.posix === path.posix</c>, and a script that
/// normalizes a Windows path on a Linux host reaches the Windows flavour exactly that way. The two objects and
/// their cross-links are per module record, so importing <c>node:path</c> and <c>node:path/win32</c> in one
/// engine yields two <c>win32</c> objects rather than one — Node's are the same object, and nothing in the
/// documented surface distinguishes them beyond identity.
/// </para>
/// <para>
/// <c>path.matchesGlob</c> is deliberately absent. It is the one member of the module that is not string
/// arithmetic: Node implements it with a bundled <c>minimatch</c>, and an absent function is what lets a
/// script feature-detect it instead of receiving an approximation of glob semantics.
/// </para>
/// </remarks>
internal static class NodePathModule
{
    internal static List<KeyValuePair<string, JsValue>> CreateExports(
        Engine engine,
        NodeBuiltinModuleConfiguration configuration,
        NodePathFlavor flavor)
    {
        var realm = engine.Realm;

        var posixEntries = CreateEntries(engine, realm, configuration, windows: false);
        var win32Entries = CreateEntries(engine, realm, configuration, windows: true);

        var posix = JsObject.CreateFromEntries(engine, posixEntries);
        var win32 = JsObject.CreateFromEntries(engine, win32Entries);

        // The two placeholders written above are replaced now that both objects exist. Writing an existing
        // slot keeps the shared layout the objects were created with; adding a property would not.
        LinkFlavours(posix, posixEntries, posix, win32);
        LinkFlavours(win32, win32Entries, posix, win32);

        var selectedIsWindows = flavor switch
        {
            NodePathFlavor.Posix => false,
            NodePathFlavor.Win32 => true,
            _ => configuration.PlatformIsWindows,
        };

        var entries = selectedIsWindows ? win32Entries : posixEntries;
        var selected = selectedIsWindows ? win32 : posix;

        var exports = new List<KeyValuePair<string, JsValue>>(entries.Count + 1);
        exports.AddRange(entries);

        // What `import path from 'node:path'` binds. Node's builtins are CommonJS modules whose
        // `module.exports` becomes the default export, with the named exports detected beside it.
        exports.Add(new KeyValuePair<string, JsValue>("default", selected));
        return exports;
    }

    private static void LinkFlavours(
        JsObject target,
        List<KeyValuePair<string, JsValue>> entries,
        JsObject posix,
        JsObject win32)
    {
        target.Set("posix", posix);
        target.Set("win32", win32);

        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Key, "posix", StringComparison.Ordinal))
            {
                entries[i] = new KeyValuePair<string, JsValue>("posix", posix);
            }
            else if (string.Equals(entries[i].Key, "win32", StringComparison.Ordinal))
            {
                entries[i] = new KeyValuePair<string, JsValue>("win32", win32);
            }
        }
    }

    /// <summary>
    /// One flavour's members, in the order Node's own module object carries them.
    /// </summary>
    private static List<KeyValuePair<string, JsValue>> CreateEntries(
        Engine engine,
        Realm realm,
        NodeBuiltinModuleConfiguration configuration,
        bool windows)
    {
        var cwd = windows ? configuration.WorkingDirectory : configuration.PosixWorkingDirectory;
        var platformIsWindows = configuration.PlatformIsWindows;
        var separator = windows ? NodeWin32Path.Separator : NodePosixPath.Separator;
        var delimiter = windows ? NodeWin32Path.Delimiter : NodePosixPath.Delimiter;

        return
        [
            Member(engine, realm, "resolve", 0, (_, arguments) =>
                JsString.Create(windows
                    ? NodeWin32Path.Resolve(cwd, platformIsWindows, RequirePaths(realm, arguments))
                    : NodePosixPath.Resolve(cwd, RequirePaths(realm, arguments)))),

            Member(engine, realm, "normalize", 1, (_, arguments) =>
                JsString.Create(windows
                    ? NodeWin32Path.Normalize(Path(realm, arguments))
                    : NodePosixPath.Normalize(Path(realm, arguments)))),

            Member(engine, realm, "isAbsolute", 1, (_, arguments) =>
                JsBoolean.Create(windows
                    ? NodeWin32Path.IsAbsolute(Path(realm, arguments))
                    : NodePosixPath.IsAbsolute(Path(realm, arguments)))),

            Member(engine, realm, "join", 0, (_, arguments) =>
                JsString.Create(windows
                    ? NodeWin32Path.Join(RequirePaths(realm, arguments))
                    : NodePosixPath.Join(RequirePaths(realm, arguments)))),

            Member(engine, realm, "relative", 2, (_, arguments) =>
            {
                var from = NodeBuiltinHelpers.RequireString(realm, arguments.At(0), "from");
                var to = NodeBuiltinHelpers.RequireString(realm, arguments.At(1), "to");
                return JsString.Create(windows
                    ? NodeWin32Path.Relative(cwd, platformIsWindows, from, to)
                    : NodePosixPath.Relative(cwd, from, to));
            }),

            // Node returns a non-string argument unchanged rather than validating it, so this one deliberately
            // does not go through RequireString.
            Member(engine, realm, "toNamespacedPath", 1, (_, arguments) =>
            {
                var argument = arguments.At(0);
                if (argument is not JsString text)
                {
                    return argument;
                }

                return JsString.Create(windows
                    ? NodeWin32Path.ToNamespacedPath(cwd, platformIsWindows, text.ToString())
                    : NodePosixPath.ToNamespacedPath(text.ToString()));
            }),

            Member(engine, realm, "dirname", 1, (_, arguments) =>
                JsString.Create(windows
                    ? NodeWin32Path.Dirname(Path(realm, arguments))
                    : NodePosixPath.Dirname(Path(realm, arguments)))),

            Member(engine, realm, "basename", 2, (_, arguments) =>
            {
                // Node validates the suffix first, so two bad arguments report the suffix.
                var suffixArgument = arguments.At(1);
                var suffix = suffixArgument.IsUndefined()
                    ? null
                    : NodeBuiltinHelpers.RequireString(realm, suffixArgument, "suffix");
                var path = Path(realm, arguments);
                return JsString.Create(windows
                    ? NodeWin32Path.Basename(path, suffix)
                    : NodePosixPath.Basename(path, suffix));
            }),

            Member(engine, realm, "extname", 1, (_, arguments) =>
                JsString.Create(windows
                    ? NodeWin32Path.Extname(Path(realm, arguments))
                    : NodePosixPath.Extname(Path(realm, arguments)))),

            Member(engine, realm, "format", 1, (_, arguments) =>
                JsString.Create(Format(realm, separator, arguments.At(0)))),

            Member(engine, realm, "parse", 1, (_, arguments) =>
            {
                var path = Path(realm, arguments);
                var parsed = windows ? NodeWin32Path.Parse(path) : NodePosixPath.Parse(path);
                return JsObject.CreateFromEntries(engine,
                [
                    new KeyValuePair<string, JsValue>("root", JsString.Create(parsed.Root)),
                    new KeyValuePair<string, JsValue>("dir", JsString.Create(parsed.Dir)),
                    new KeyValuePair<string, JsValue>("base", JsString.Create(parsed.Base)),
                    new KeyValuePair<string, JsValue>("ext", JsString.Create(parsed.Ext)),
                    new KeyValuePair<string, JsValue>("name", JsString.Create(parsed.Name)),
                ]);
            }),

            new KeyValuePair<string, JsValue>("sep", JsString.Create(separator.ToString())),
            new KeyValuePair<string, JsValue>("delimiter", JsString.Create(delimiter.ToString())),

            // Filled in by LinkFlavours once both objects exist.
            new KeyValuePair<string, JsValue>("win32", JsValue.Undefined),
            new KeyValuePair<string, JsValue>("posix", JsValue.Undefined),
        ];
    }

    private static KeyValuePair<string, JsValue> Member(Engine engine, Realm realm, string name, int length, JsCallDelegate body)
        => new(name, NodeBuiltinHelpers.Operation(engine, realm, name, length, body));

    private static string Path(Realm realm, JsCallArguments arguments)
        => NodeBuiltinHelpers.RequireString(realm, arguments.At(0), "path");

    /// <summary>
    /// The variadic <c>path</c> arguments of <c>join</c> and <c>resolve</c>.
    /// </summary>
    /// <remarks>
    /// Every argument is validated, which is what the documentation promises — "throws a <c>TypeError</c> if
    /// any of the arguments is not a string". Node's <c>resolve</c> is in fact laxer than its own contract: it
    /// walks the segments right to left and stops at the first absolute one, so a non-string to the left of an
    /// absolute segment is never looked at. Following the documented rule is both stricter and the behaviour a
    /// caller can reason about.
    /// </remarks>
    private static string[] RequirePaths(Realm realm, JsCallArguments arguments)
    {
        var paths = new string[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            paths[i] = NodeBuiltinHelpers.RequireString(realm, arguments[i], "path");
        }

        return paths;
    }

    /// <summary>
    /// <c>path.format(pathObject)</c>. The three "is it provided" tests are JavaScript truthiness, so a
    /// property that is absent, empty or otherwise falsy counts as absent and the next fallback applies.
    /// </summary>
    private static string Format(Realm realm, char separator, JsValue argument)
    {
        var pathObject = NodeBuiltinHelpers.RequireObject(realm, argument, "pathObject");

        var dir = Coerce(pathObject.Get("dir"));
        var root = Coerce(pathObject.Get("root"));
        var @base = Coerce(pathObject.Get("base"));
        var name = Coerce(pathObject.Get("name"));
        var ext = Coerce(pathObject.Get("ext"));

        return NodePathAlgorithms.Format(separator, dir, root, @base, name, ext);

        static string Coerce(JsValue value)
            => TypeConverter.ToBoolean(value) ? TypeConverter.ToString(value) : string.Empty;
    }
}
