namespace Jint.Runtime;

internal interface IScriptOrModule
{
    /// <summary>
    /// Returns the location the script or module was loaded from.
    /// </summary>
    string Location { get; }
}
