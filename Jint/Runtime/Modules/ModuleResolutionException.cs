namespace Jint.Runtime.Modules;

public sealed class ModuleResolutionException : JintException
{
    public ModuleResolutionException(string resolverAlgorithmError, string specifier, string? parent, string? filePath)
        : base($"{resolverAlgorithmError} in module '{parent ?? "(null)"}': '{specifier}'")
    {
        ResolverAlgorithmError = resolverAlgorithmError;
        Specifier = specifier;
        FilePath = filePath;
    }

    public string ResolverAlgorithmError { get; }
    public string Specifier { get; }
    public string? FilePath { get; }
}
