using System;

namespace Jint.Runtime.Modules;

internal sealed class FailFastModuleLoader : IModuleLoader
{
    public static readonly IModuleLoader Instance = new FailFastModuleLoader();

    public bool TryLoadModule(string location, string referencingLocation, out string moduleSource, out string moduleLocation)
    {
        ThrowDisabledException();
        moduleSource = null;
        moduleLocation = null;
        return false;
    }

    public void AddModuleSource(params IModuleSource[] moduleSources)
    {
        ThrowDisabledException();
    }

    private static void ThrowDisabledException()
    {
        throw new InvalidOperationException("Module loading has been disabled, you need to enable it in engine options");
    }
}