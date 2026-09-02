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
    All,

    /// <summary>
    /// Only an exception a <c>catch</c> clause on the stack will land in stops the engine, which is the
    /// complement of <see cref="Uncaught"/>.
    /// </summary>
    /// <remarks>
    /// What a tool offers as "pause on caught exceptions". The engine decides it where the throw happens, so
    /// a handler is never raised for a throw it would have to decline — which it could only do by returning a
    /// <see cref="StepMode"/>, and every one but <see cref="StepMode.Unchanged"/> cancels a step in flight.
    /// </remarks>
    Caught,
}
