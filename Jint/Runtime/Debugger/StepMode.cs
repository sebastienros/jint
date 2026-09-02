namespace Jint.Runtime.Debugger;

/// <summary>
/// What the engine does after a <see cref="DebugHandler.DebugEventHandler"/> returns, which is how a handler
/// steps.
/// </summary>
public enum StepMode
{
    /// <summary>
    /// Run on. Nothing stops the engine until a breakpoint, a <c>debugger</c> statement or a thrown
    /// exception <see cref="DebugHandler.PauseOnExceptions"/> asked for.
    /// </summary>
    None,

    /// <summary>
    /// Stop at the next execution point of this frame, running any call it makes to completion.
    /// </summary>
    Over,

    /// <summary>
    /// Stop at the next execution point anywhere, including the first one inside a call.
    /// </summary>
    Into,

    /// <summary>
    /// Run on until this frame returns, and stop at the next execution point of its caller.
    /// </summary>
    Out,

    /// <summary>
    /// Leave the step mode as it is, which is what a handler that declined to pause has to say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other member <em>sets</em> the mode, so a handler that only meant "not this one" and answered
    /// <see cref="None"/> cancelled a step that was in flight. This one changes nothing: a step the client
    /// asked for before the notification is still armed after it.
    /// </para>
    /// <para>
    /// As the initial mode of an engine (<c>Options.Debugger.InitialStepMode</c>) it means
    /// <see cref="None"/>, there being no mode yet to keep.
    /// </para>
    /// </remarks>
    Unchanged,
}
