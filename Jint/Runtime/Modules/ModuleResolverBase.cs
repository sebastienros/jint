#nullable enable

namespace Jint.Runtime.Modules;

public abstract class ModuleResolverBase
{
    protected static bool IsRelative(string specifier)
    {
        return specifier.StartsWith("./") || specifier.StartsWith("../") || specifier.StartsWith("/");
    }
}