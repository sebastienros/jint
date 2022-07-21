namespace Jint.Runtime.Modules;

public sealed class ModuleResolutionException : JintException
{
    public string ResolverAlgorithmError { get; }
    public string Specifier { get; }
    
    public ModuleResolutionException(string message, string specifier, string? parent)
        : base($"{message} in module '{parent ?? "(null)"}': '{specifier}'")
    {
        ResolverAlgorithmError = message;
        Specifier = specifier;
    }
}