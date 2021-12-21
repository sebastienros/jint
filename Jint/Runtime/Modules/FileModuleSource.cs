using System;
using System.IO;

namespace Jint.Runtime.Modules;

internal sealed class FileModuleSource : IModuleSource
{
    internal static readonly FileModuleSource Instance = new();

    public bool TryLoadModuleSource(Uri location, out string moduleSourceCode)
    {
        try
        {
            moduleSourceCode = File.ReadAllText(location.AbsolutePath);
            return true;
        }
        catch
        {
            moduleSourceCode = null;
            return false;
        }
    }
}