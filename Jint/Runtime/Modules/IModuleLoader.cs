#nullable enable

using Esprima.Ast;

namespace Jint.Runtime.Modules;

/// <summary>
/// Module loader interface that allows defining how module loadings requests are handled.
/// </summary>
public interface IModuleLoader
{
    /// <summary>
    /// Loads a module from given location.
    /// </summary>
    public Module LoadModule(Engine engine, ResolvedSpecifier moduleResolution);
}