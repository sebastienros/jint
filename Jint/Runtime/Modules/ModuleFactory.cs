using Esprima;
using Jint.Native;
using Jint.Native.Json;

namespace Jint.Runtime.Modules;

/// <summary>
/// Factory which creates a single runtime <see cref="Module"/> from a given source.
/// </summary>
public static class ModuleFactory
{
    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/>
    /// from the provided javascript <paramref name="code"/>.
    /// </summary>
    /// <remarks>
    /// The returned modules location (see <see cref="Module.Location"/>) points to
    /// <see cref="Uri.LocalPath"/> if <see cref="ResolvedSpecifier.Uri"/> is not null. If
    /// <see cref="ResolvedSpecifier.Uri"/> is null, the modules location source will be null as well.
    /// </remarks>
    /// <exception cref="ParserException">Is thrown if the provided <paramref name="code"/> can not be parsed.</exception>
    /// <exception cref="JavaScriptException">Is thrown if an error occured when parsing <paramref name="code"/>.</exception>
    public static Module BuildSourceTextModule(Engine engine, ResolvedSpecifier resolved, string code)
    {
        var source = resolved.Uri?.LocalPath ?? resolved.Key;
        Esprima.Ast.Module module;
        try
        {
            module = new JavaScriptParser().ParseModule(code, source);
        }
        catch (ParserException ex)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, $"Error while loading module: error in module '{source}': {ex.Error}");
            module = null;
        }
        catch (Exception)
        {
            ExceptionHelper.ThrowJavaScriptException(engine, $"Could not load module {source}", (Location) default);
            module = null;
        }

        return BuildSourceTextModule(engine, module);
    }

    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/>
    /// from the parsed <paramref name="parsedModule"/>.
    /// </summary>
    /// <remarks>
    /// The returned modules location (see <see cref="Module.Location"/>) will be set
    /// to <see cref="Location.Source"/> of the <paramref name="parsedModule"/>.
    /// </remarks>
    public static Module BuildSourceTextModule(Engine engine, Esprima.Ast.Module parsedModule)
    {
        return new SourceTextModule(engine, engine.Realm, parsedModule, parsedModule.Location.Source, async: false);
    }

    /// <summary>
    /// Creates a <see cref="Module"/> for the usage within the given <paramref name="engine"/> for the
    /// provided JSON module <paramref name="jsonString"/>.
    /// </summary>
    /// <remarks>
    /// The returned modules location (see <see cref="Module.Location"/>) points to
    /// <see cref="Uri.LocalPath"/> if <see cref="ResolvedSpecifier.Uri"/> is not null. If
    /// <see cref="ResolvedSpecifier.Uri"/> is null, the modules location source will be null as well.
    /// </remarks>
    /// <exception cref="JavaScriptException">Is thrown if an error occured when parsing <paramref name="jsonString"/>.</exception>
    public static Module BuildJsonModule(Engine engine, ResolvedSpecifier resolved, string jsonString)
    {
        var source = resolved.Uri?.LocalPath;
        JsValue module;
        try
        {
            module = new JsonParser(engine).Parse(jsonString);
        }
        catch (Exception)
        {
            ExceptionHelper.ThrowJavaScriptException(engine, $"Could not load module {source}", (Location) default);
            module = null;
        }

        return BuildJsonModule(engine, module, resolved.Uri?.LocalPath);
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
}
