using Esprima.Ast;

namespace Jint.Runtime.Modules;

internal sealed class FailFastModuleLoader : IModuleLoader
{
    public static readonly IModuleLoader Instance = new FailFastModuleLoader();

    public Uri BasePath
    {
        get
        {
            ExceptionHelper.ThrowInvalidOperationException("Cannot access base path when modules loading is disabled");
            return default;
        }
    }

    public ResolvedSpecifier Resolve(string? referencingModuleLocation, string specifier)
    {
        return new ResolvedSpecifier(specifier, specifier, null, SpecifierType.Bare);
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
