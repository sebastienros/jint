using System;

namespace Jint.Runtime.Modules;

/// <summary>
/// A way to resolve a module from given location.
/// </summary>
public interface IModuleSource
{
    /// <summary>
    /// Tries to load a module from given source.
    /// </summary>
    public bool TryLoadModuleSource(Uri location, out string moduleSourceCode);
}