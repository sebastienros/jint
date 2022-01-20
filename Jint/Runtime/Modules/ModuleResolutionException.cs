#nullable enable

namespace Jint.Runtime.Modules;

public sealed class ModuleResolutionException : JintException
{
    public ModuleResolutionException(string message, string specifier, string? parent)
        : base($"{message} in module '{parent ?? "(null)"}': '{specifier}'")
    {
    }
}