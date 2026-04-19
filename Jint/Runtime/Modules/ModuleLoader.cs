namespace Jint.Runtime.Modules;

/// <summary>
/// Base template for module loaders.
/// </summary>
public abstract class ModuleLoader : IModuleLoader
{
    public abstract ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest);

    public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        if (resolved.ModuleRequest.IsBytesModule())
        {
            byte[] bytes;
            try
            {
                bytes = LoadModuleContentsAsBytes(engine, resolved);
            }
            catch (Exception)
            {
                Throw.JavaScriptException(engine, $"Could not load module {resolved.ModuleRequest.Specifier}", AstExtensions.DefaultLocation);
                return default!;
            }

            return ModuleFactory.BuildBytesModule(engine, resolved, bytes);
        }

        string code;
        try
        {
            code = LoadModuleContents(engine, resolved);
        }
        catch (Exception)
        {
            Throw.JavaScriptException(engine, $"Could not load module {resolved.ModuleRequest.Specifier}", AstExtensions.DefaultLocation);
            return default!;
        }

        if (resolved.ModuleRequest.IsTextModule())
        {
            return ModuleFactory.BuildTextModule(engine, resolved, code);
        }

        var isJson = resolved.ModuleRequest.IsJsonModule();
        Module moduleRecord = isJson
            ? ModuleFactory.BuildJsonModule(engine, resolved, code)
            : ModuleFactory.BuildSourceTextModule(engine, resolved, code);

        return moduleRecord;
    }

    protected abstract string LoadModuleContents(Engine engine, ResolvedSpecifier resolved);

    /// <summary>
    /// Loads module contents as raw bytes. Override in derived classes for efficient binary loading.
    /// </summary>
    protected virtual byte[] LoadModuleContentsAsBytes(Engine engine, ResolvedSpecifier resolved)
    {
        var text = LoadModuleContents(engine, resolved);
        return System.Text.Encoding.UTF8.GetBytes(text);
    }
}
