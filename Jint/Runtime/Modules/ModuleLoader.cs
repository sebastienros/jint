namespace Jint.Runtime.Modules;

/// <summary>
/// Base template for module loaders.
/// </summary>
public abstract class ModuleLoader : IModuleLoader
{
    public abstract ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest);

    public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        Module moduleRecord;
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

            moduleRecord = ModuleFactory.BuildBytesModule(engine, resolved, bytes);
        }
        else
        {
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
                moduleRecord = ModuleFactory.BuildTextModule(engine, resolved, code);
            }
            else if (resolved.ModuleRequest.IsJsonModule())
            {
                moduleRecord = ModuleFactory.BuildJsonModule(engine, resolved, code);
            }
            else
            {
                moduleRecord = ModuleFactory.BuildSourceTextModule(engine, resolved, code);
            }
        }

        // Attach the host-defined [[ModuleSource]] (used by source-phase imports). Returns null for
        // ordinary modules, leaving behaviour unchanged.
        moduleRecord.ModuleSource = GetModuleSource(engine, resolved);

        return moduleRecord;
    }

    protected abstract string LoadModuleContents(Engine engine, ResolvedSpecifier resolved);

    /// <summary>
    /// Returns the host-defined <c>[[ModuleSource]]</c> object (an %AbstractModuleSource% instance) for the
    /// resolved module, or <see langword="null"/> when the module has no source representation. The default
    /// returns <see langword="null"/>, so a source-phase import (<c>import source x from "..."</c>) of an
    /// ordinary JavaScript module is rejected. Hosts that integrate module sources (e.g. WebAssembly)
    /// override this.
    /// </summary>
    protected virtual Jint.Native.Object.ObjectInstance? GetModuleSource(Engine engine, ResolvedSpecifier resolved) => null;

    /// <summary>
    /// Loads module contents as raw bytes. Override in derived classes for efficient binary loading.
    /// </summary>
    protected virtual byte[] LoadModuleContentsAsBytes(Engine engine, ResolvedSpecifier resolved)
    {
        var text = LoadModuleContents(engine, resolved);
        return System.Text.Encoding.UTF8.GetBytes(text);
    }
}
