using Jint.Native;
using Jint.Native.Json;

namespace Jint.Runtime.Modules;

/// <summary>
/// Factory which creates a single runtime <see cref="Module"/> from a given source.
/// </summary>
public static class ModuleFactory
{
    /// <summary>
    /// The name a module knows itself by. A <c>file:</c> url reads better as a filesystem path, but
    /// reducing any other url to one drops its scheme, host and query - and a module that knows itself
    /// as <c>/lib/a.js</c> has no origin left for a relative import of its own to resolve against, nor
    /// one to report through <c>import.meta.url</c>. A relative or absent uri leaves the resolved key,
    /// which is the only name such a module has.
    /// </summary>
    private static string? LocationOf(ResolvedSpecifier resolved)
    {
        var uri = resolved.Uri;
        if (uri is null || !uri.IsAbsoluteUri) return resolved.Key;

        return uri.IsFile ? uri.LocalPath : uri.AbsoluteUri;
    }

    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/>
    /// from the provided javascript <paramref name="code"/>.
    /// </summary>
    /// <remarks>
    /// The returned modules location (see <see cref="Module.Location"/>) is the whole url of
    /// <see cref="ResolvedSpecifier.Uri"/>, or its <see cref="Uri.LocalPath"/> for a <c>file:</c> url,
    /// falling back to <see cref="ResolvedSpecifier.Key"/> when there is no absolute uri.
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
    /// The returned modules location (see <see cref="Module.Location"/>) is the whole url of
    /// <see cref="ResolvedSpecifier.Uri"/>, or its <see cref="Uri.LocalPath"/> for a <c>file:</c> url,
    /// falling back to <see cref="ResolvedSpecifier.Key"/> when there is no absolute uri.
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
    /// </remarks>
    public static Module BuildTextModule(Engine engine, ResolvedSpecifier resolved, string text)
    {
        return new SyntheticModule(engine, engine.Realm, JsString.Create(text), LocationOf(resolved));
    }
}
