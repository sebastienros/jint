namespace Jint.Runtime.Modules;

/// <summary>
/// Base template for module loaders.
/// </summary>
public abstract class ModuleLoader : IModuleLoader
{
    public abstract ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest);

    public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        string code;
        try
        {
            code = LoadModuleContents(engine, resolved);
        }
        catch (Exception)
        {
            ExceptionHelper.ThrowJavaScriptException(engine, $"Could not load module {resolved.ModuleRequest.Specifier}", AstExtensions.DefaultLocation);
            return default!;
        }

        var isJson = resolved.ModuleRequest.IsJsonModule();
        Module moduleRecord = isJson
            ? ModuleFactory.BuildJsonModule(engine, resolved, code)
            : ModuleFactory.BuildSourceTextModule(engine, resolved, code);

        return moduleRecord;
    }

    protected abstract string LoadModuleContents(Engine engine, ResolvedSpecifier resolved);
}
