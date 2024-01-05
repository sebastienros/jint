namespace Jint.Runtime.Modules;

/// <summary>
/// Module loader interface that allows defining how module loadings requests are handled.
/// </summary>
public interface IModuleLoader
{
    /// <summary>
    /// Resolves a specifier to a path or module
    /// </summary>
    ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest);

    /// <summary>
    /// Loads a module from given location.
    /// </summary>
    public Module LoadModule(Engine engine, ResolvedSpecifier resolved);
}
