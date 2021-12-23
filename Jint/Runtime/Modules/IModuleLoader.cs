#nullable enable

using System;
using Esprima.Ast;

namespace Jint.Runtime.Modules;

/// <summary>
/// Module loading result.
/// </summary>
public readonly record struct ModuleLoaderResult(Module Module, Uri Location);

/// <summary>
/// Module loader interface that allows defining how module loadings requests are handled.
/// </summary>
public interface IModuleLoader
{
    /// <summary>
    /// Loads a module from given location.
    /// </summary>
    public ModuleLoaderResult LoadModule(Engine engine, string location, string? referencingLocation);
}