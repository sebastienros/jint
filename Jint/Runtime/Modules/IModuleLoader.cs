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
    /// The base path from which file-based modules will be resolved
    /// </summary>
    Uri BasePath { get; }

    /// <summary>
    /// Loads a module from given location.
    /// </summary>
    public ModuleLoaderResult LoadModule(Engine engine, string location);
}