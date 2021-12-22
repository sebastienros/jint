#nullable enable

namespace Jint.Runtime.Modules;

internal sealed class FailFastModuleLoader : IModuleLoader
{
    public static readonly IModuleLoader Instance = new FailFastModuleLoader();

    public ModuleLoaderResult LoadModule(Engine engine, string location, string? referencingLocation)
    {
        ThrowDisabledException();
        return default;
    }

    private static void ThrowDisabledException()
    {
        ExceptionHelper.ThrowInvalidOperationException("Module loading has been disabled, you need to enable it in engine options");
    }
}