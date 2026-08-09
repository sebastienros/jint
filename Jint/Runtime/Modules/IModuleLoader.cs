namespace Jint.Runtime.Modules;

/// <summary>
/// Module loader interface that allows defining how module loadings requests are handled.
/// </summary>
public interface IModuleLoader
{
    /// <summary>
    /// Resolves a specifier to a path or module
    /// </summary>
    /// <remarks>
    /// Called at most once per referrer/specifier pair: per
    /// <see href="https://tc39.es/ecma262/#sec-HostLoadImportedModule">HostLoadImportedModule</see> the answer
    /// for a given pair must be consistent, so the engine memoizes it in the referrer's
    /// <c>[[LoadedModules]]</c> and a repeat import is answered from that memo without consulting the loader
    /// at all. A loader is therefore a mapping, not a checkpoint — per-call access control (vetoing a repeat
    /// import that succeeded before) cannot be implemented here, because the veto is never asked for.
    /// </remarks>
    ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest);

    /// <summary>
    /// Loads a module from given location.
    /// </summary>
    public Module LoadModule(Engine engine, ResolvedSpecifier resolved);
}
