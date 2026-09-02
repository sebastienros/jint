namespace Jint.Runtime.Debugger;

/// <summary>
/// Which thrown exceptions stop the engine, set through <see cref="DebugHandler.PauseOnExceptions"/>.
/// </summary>
public enum ExceptionPauseMode
{
    /// <summary>
    /// No exception stops the engine. This is the default.
    /// </summary>
    None,

    /// <summary>
    /// Only an exception with no <c>catch</c> clause on the stack to land in stops the engine.
    /// </summary>
    Uncaught,

    /// <summary>
    /// Every exception stops the engine, whether or not something will catch it.
    /// </summary>
    All
}
