namespace Jint.Runtime.Modules;

/// <summary>
/// Module loader interface that allows defining how module loadings requests are handled.
/// </summary>
public interface IModuleLoader
{
    /// <summary>
    /// Tries to load amoudle.
    /// </summary>
    /// <param name="location"></param>
    /// <param name="referencingLocation"></param>
    /// <param name="moduleSource"></param>
    /// <param name="moduleLocation"></param>
    /// <returns></returns>
    public bool TryLoadModule(string location, string referencingLocation, out string moduleSource, out string moduleLocation);

    /// <summary>
    /// Add module loader sources to use. By default Jint only tries to load from local files.
    /// </summary>
    public void AddModuleSource(params IModuleSource[] moduleSources);
}