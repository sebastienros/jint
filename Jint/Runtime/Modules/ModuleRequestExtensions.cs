namespace Jint.Runtime.Modules;

public static class ModuleRequestExtensions
{
    /// <summary>
    /// Returns true if the provided <paramref name="request"/>
    /// is a json module, otherwise false.
    /// </summary>
    /// <example>
    /// The following JavaScript import statement imports a JSON module
    /// for which this method would return true.
    /// <code>
    /// import value from 'config.json' with { type: 'json' }
    /// </code>
    /// </example>
    public static bool IsJsonModule(this ModuleRequest request)
    {
        return request.Attributes != null
            && Array.Exists(request.Attributes, x => string.Equals(x.Key, "type", StringComparison.Ordinal) && string.Equals(x.Value, "json", StringComparison.Ordinal));
    }
}
