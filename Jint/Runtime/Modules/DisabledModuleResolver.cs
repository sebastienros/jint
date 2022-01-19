#nullable enable

namespace Jint.Runtime.Modules;

public class DisabledModuleResolver : ModuleResolverBase, IModuleResolver
{
    public static readonly IModuleResolver Instance = new DisabledModuleResolver();

    public ResolvedSpecifier Resolve(string referencingModuleLocation, string specifier)
    {
        if (IsRelative(specifier))
        {
            ExceptionHelper.ThrowInvalidOperationException("Module loading has been disabled, you need to enable it in engine options");
            return default;
        }

        return new ResolvedSpecifier(specifier, specifier, null, SpecifierType.Bare);
    }
}