using Jint.Native;
using Jint.Native.Json;

namespace Jint.Runtime.Modules;

/// <summary>
/// Factory which creates a single runtime <see cref="Module"/> from a given source.
/// </summary>
public static class ModuleFactory
{
    /// <summary>
    /// The name a module knows itself by: <see cref="ResolvedSpecifier.Key"/>, the string the loader
    /// itself chose, with one exception for a <c>file:</c> uri. Reducing a url to <see cref="Uri.LocalPath"/>
    /// drops its scheme, host and query, so a module that knows itself as <c>/lib/a.js</c> has no origin
    /// left for a relative import of its own to resolve against - and this name is exactly what comes
    /// back as <c>referencingModuleLocation</c> on the next <see cref="ModuleLoader.Resolve"/> - nor one
    /// to report through <c>import.meta.url</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The key is deliberately preferred over <see cref="Uri.AbsoluteUri"/>, which is not the url the host
    /// gave us: it strips a default port, lowercases the host, collapses dot segments and percent-encodes,
    /// and the .NET Framework and .NET Core uri parsers disagree on some of that, so the same host code
    /// would see a different name per target framework. The key round-trips by construction and is already
    /// the identity the module map and the module-builder registry are keyed on, so a module's own name and
    /// the name it is cached under cannot drift apart.
    /// </para>
    /// <para>
    /// The <c>file:</c> exception keeps, for that one scheme, the reduction described above: a host loading
    /// from disk has always been handed a filesystem path and code doing <c>Path.GetDirectoryName</c> on it
    /// would break, so such a module still has to reconstruct its origin from the loader's base path. That
    /// is the trade for not renaming every module every host already loads from disk.
    /// </para>
    /// <para>
    /// A relative uri answers the only question left for the uri - is this a file? - no better than an
    /// absent one does, so both leave the key. That gives up a diagnostic: a loader returning a relative
    /// uri used to fail loudly here, because <see cref="Uri.LocalPath"/> throws
    /// <see cref="InvalidOperationException"/> on one. It now builds a module named by its key, which is
    /// the same shape a loader returning no uri at all has always produced.
    /// </para>
    /// </remarks>
    private static string LocationOf(ResolvedSpecifier resolved)
    {
        var uri = resolved.Uri;
        if (uri is not null && uri.IsAbsoluteUri && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return resolved.Key;
    }

    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/>
    /// from the provided javascript <paramref name="code"/>.
    /// </summary>
    /// <remarks>
    /// The returned modules location (see <see cref="Module.Location"/>) is <see cref="ResolvedSpecifier.Key"/>,
    /// or the <see cref="Uri.LocalPath"/> of <see cref="ResolvedSpecifier.Uri"/> when that is an absolute
    /// <c>file:</c> uri. It is never null. This name is also the module's identity for stack traces and for
    /// the debugger, and the <c>referencingModuleLocation</c> passed to <see cref="ModuleLoader.Resolve"/>
    /// for the module's own imports.
    /// </remarks>
    /// <exception cref="ParseErrorException">Is thrown if the provided <paramref name="code"/> can not be parsed.</exception>
    /// <exception cref="JavaScriptException">Is thrown if an error occured when parsing <paramref name="code"/>.</exception>
    public static Module BuildSourceTextModule(Engine engine, ResolvedSpecifier resolved, string code, ModuleParsingOptions? parsingOptions = null)
    {
        var source = LocationOf(resolved);
        var parserOptions = (parsingOptions ?? ModuleParsingOptions.Default).GetParserOptions();
        var parser = new Parser(parserOptions);
        var module = parser.ParseModuleGuarded(engine, code, source);

        return BuildSourceTextModule(engine, new Prepared<AstModule>(module, parserOptions));
    }

    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/>
    /// from the parsed <paramref name="preparedModule"/>.
    /// </summary>
    /// <remarks>
    /// The returned modules location (see <see cref="Module.Location"/>) will be set
    /// to <see cref="SourceLocation.SourceFile"/> of the <paramref name="preparedModule"/>.
    /// </remarks>
    public static Module BuildSourceTextModule(Engine engine, in Prepared<AstModule> preparedModule)
    {
        if (!preparedModule.IsValid)
        {
            Throw.InvalidPreparedModuleArgumentException(nameof(preparedModule));
        }

        var hasTopLevelAwait = HoistingScope.HasTopLevelAwait(preparedModule.Program!);
        return new SourceTextModule(engine, engine.Realm, in preparedModule, preparedModule.Program!.Location.SourceFile, isAsync: hasTopLevelAwait);
    }

    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/> for the
    /// provided JSON module <paramref name="jsonString"/>.
    /// </summary>
    /// <remarks>
    /// The returned modules location (see <see cref="Module.Location"/>) is <see cref="ResolvedSpecifier.Key"/>,
    /// or the <see cref="Uri.LocalPath"/> of <see cref="ResolvedSpecifier.Uri"/> when that is an absolute
    /// <c>file:</c> uri. It is never null, where before it was null for a loader returning no uri.
    /// </remarks>
    /// <exception cref="JavaScriptException">Is thrown if an error occured when parsing <paramref name="jsonString"/>.</exception>
    public static Module BuildJsonModule(Engine engine, ResolvedSpecifier resolved, string jsonString)
    {
        var source = LocationOf(resolved);
        JsValue module;
        try
        {
            module = new JsonParser(engine).Parse(jsonString);
        }
        catch (Exception)
        {
            Throw.SyntaxError(engine.Realm, $"Could not load module {source}");
            module = null;
        }

        return BuildJsonModule(engine, module, source);
    }

    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/>
    /// from the parsed JSON provided in <paramref name="parsedJson"/>.
    /// </summary>
    /// <remarks>
    /// The returned modules location (see <see cref="Module.Location"/>) will be set
    /// to <paramref name="location"/>.
    /// </remarks>
    public static Module BuildJsonModule(Engine engine, JsValue parsedJson, string? location)
    {
        return new SyntheticModule(engine, engine.Realm, parsedJson, location);
    }

    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/> for the
    /// provided bytes module data.
    /// </summary>
    /// <remarks>
    /// https://tc39.es/proposal-import-bytes/#sec-create-bytes-module
    /// <para>
    /// The returned modules location (see <see cref="Module.Location"/>) is <see cref="ResolvedSpecifier.Key"/>,
    /// or the <see cref="Uri.LocalPath"/> of <see cref="ResolvedSpecifier.Uri"/> when that is an absolute
    /// <c>file:</c> uri. It is never null, where before it was null for a loader returning no uri.
    /// </para>
    /// </remarks>
    public static Module BuildBytesModule(Engine engine, ResolvedSpecifier resolved, byte[] bytes)
    {
        var arrayBuffer = engine.Realm.Intrinsics.ArrayBuffer.Construct(bytes);
        arrayBuffer._isImmutable = true;

        var uint8Array = engine.Realm.Intrinsics.Uint8Array.Construct([arrayBuffer], engine.Realm.Intrinsics.Uint8Array);

        return new SyntheticModule(engine, engine.Realm, uint8Array, LocationOf(resolved));
    }

    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/> for the
    /// provided text module contents.
    /// </summary>
    /// <remarks>
    /// https://tc39.es/proposal-import-text/#sec-create-text-module
    /// <para>
    /// The returned modules location (see <see cref="Module.Location"/>) is <see cref="ResolvedSpecifier.Key"/>,
    /// or the <see cref="Uri.LocalPath"/> of <see cref="ResolvedSpecifier.Uri"/> when that is an absolute
    /// <c>file:</c> uri. It is never null, where before it was null for a loader returning no uri.
    /// </para>
    /// </remarks>
    public static Module BuildTextModule(Engine engine, ResolvedSpecifier resolved, string text)
    {
        return new SyntheticModule(engine, engine.Realm, JsString.Create(text), LocationOf(resolved));
    }
}
