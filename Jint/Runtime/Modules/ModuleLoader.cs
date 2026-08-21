namespace Jint.Runtime.Modules;

/// <summary>
/// Base template for module loaders.
/// </summary>
public abstract class ModuleLoader : IModuleLoader
{
    public abstract ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest);

    public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        // A NotSupportedException is API-misuse guidance, not a failed load: AsyncModuleLoader's synchronous
        // entry throws one saying how to reach the loader correctly, and reducing it to the generic message
        // below made it read as a missing file. It propagates as itself.
        Module moduleRecord;
        if (resolved.ModuleRequest.IsBytesModule())
        {
            byte[] bytes;
            try
            {
                bytes = LoadModuleContentsAsBytes(engine, resolved);
            }
            catch (Exception ex) when (ex is not NotSupportedException
                                       && !ModuleLoadCompletion.MustPropagateLoaderException(engine, ex))
            {
                Throw.JavaScriptException(engine, $"Could not load module {resolved.ModuleRequest.Specifier}", in AstExtensions.DefaultLocation);
                return default!;
            }

            engine.Modules.EnsureModuleRegistrationAllowed(
                Engine.ModuleCacheKey.From(resolved),
                bytes.Length);
            moduleRecord = ModuleFactory.BuildBytesModule(engine, resolved, bytes);
        }
        else
        {
            string code;
            try
            {
                code = LoadModuleContents(engine, resolved);
            }
            catch (Exception ex) when (ex is not NotSupportedException
                                       && !ModuleLoadCompletion.MustPropagateLoaderException(engine, ex))
            {
                Throw.JavaScriptException(engine, $"Could not load module {resolved.ModuleRequest.Specifier}", in AstExtensions.DefaultLocation);
                return default!;
            }

            engine.Modules.EnsureModuleRegistrationAllowed(
                Engine.ModuleCacheKey.From(resolved),
                System.Text.Encoding.UTF8.GetByteCount(code));
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

    /// <summary>
    /// Reaches <see cref="GetModuleSource"/> from the asynchronous load path, which builds the module record
    /// outside this class and must still attach the same host-defined <c>[[ModuleSource]]</c>.
    /// </summary>
    internal Jint.Native.Object.ObjectInstance? GetModuleSourceForAsyncLoad(Engine engine, ResolvedSpecifier resolved)
        => GetModuleSource(engine, resolved);

    /// <summary>
    /// Loads the module's source text. An ordinary loader or transport failure is reported to script as
    /// <c>Could not load module {specifier}</c>. Engine constraint failures and host-requested cancellation
    /// propagate instead, because reducing either to a catchable import rejection would defeat the bound.
    /// One further exception:
    /// <see cref="NotSupportedException"/> propagates as itself, reserved for telling a host that it reached
    /// this loader the wrong way rather than that a module is missing.
    /// <see cref="AsyncModuleLoader.LoadModuleContents"/> is the in-box use of it.
    /// </summary>
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
    /// Loads module contents as raw bytes. Override in derived classes for efficient binary loading. Failure is
    /// reported exactly as for <see cref="LoadModuleContents"/>, including its constraint/cancellation and
    /// <see cref="NotSupportedException"/> exceptions.
    /// </summary>
    protected virtual byte[] LoadModuleContentsAsBytes(Engine engine, ResolvedSpecifier resolved)
    {
        var text = LoadModuleContents(engine, resolved);
        return System.Text.Encoding.UTF8.GetBytes(text);
    }
}
