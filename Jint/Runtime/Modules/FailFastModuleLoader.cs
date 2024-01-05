namespace Jint.Runtime.Modules;

internal sealed class FailFastModuleLoader : IModuleLoader
{
    public static readonly IModuleLoader Instance = new FailFastModuleLoader();

#pragma warning disable CA1822
    public Uri BasePath
#pragma warning restore CA1822
    {
        get
        {
            ExceptionHelper.ThrowInvalidOperationException("Cannot access base path when modules loading is disabled");
            return default;
        }
    }

    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);
    }

    public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
    {
        ThrowDisabledException();
        return default!;
    }

    private static void ThrowDisabledException()
    {
        ExceptionHelper.ThrowInvalidOperationException("Module loading has been disabled, you need to enable it in engine options");
    }
}
