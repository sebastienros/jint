using System;

#nullable enable

namespace Jint.Runtime.Modules;

internal sealed class FailFastModuleLoader : IModuleLoader
{
    public static readonly IModuleLoader Instance = new FailFastModuleLoader();

    public Uri BasePath => throw new InvalidOperationException("Cannot access base path when modules loading is disabled");

    public ModuleLoaderResult LoadModule(Engine engine, string location)
    {
        ThrowDisabledException();
        return default;
    }

    private static void ThrowDisabledException()
    {
        ExceptionHelper.ThrowInvalidOperationException("Module loading has been disabled, you need to enable it in engine options");
    }
}