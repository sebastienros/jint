namespace Jint.Runtime;

/// <summary>
/// Carries host state from where script registers a callback to where the engine later runs it, such as across an <c>await</c>.
/// </summary>
/// <remarks>
/// <para>
/// Implements the host side of <see href="https://tc39.es/ecma262/#sec-hostmakejobcallback">HostMakeJobCallback</see>
/// (<see cref="Capture"/>) and <see href="https://tc39.es/ecma262/#sec-hostcalljobcallback">HostCallJobCallback</see>
/// (<see cref="Enter"/> and <see cref="Exit"/>) for every promise reaction, thenable resolution and
/// <c>FinalizationRegistry</c> cleanup callback. Install one with <see cref="Options.HostOptions.JobCallbacks"/>.
/// </para>
/// <para>
/// Every member runs on the engine's thread, inside the operation that registered or runs the callback, and may
/// call back into the engine. They must not throw: an exception propagates out of that operation, and from
/// <see cref="Enter"/> or <see cref="Exit"/> it ends the job the reaction belonged to.
/// </para>
/// <para>
/// The object <see cref="Capture"/> returns is held strongly for as long as the reaction holding it, so a promise
/// that never settles keeps it alive for the engine's lifetime. Return <see langword="null"/> when there is nothing
/// to carry: that callback then runs under whatever state is current when its job runs, with no
/// <see cref="Enter"/> or <see cref="Exit"/>.
/// </para>
/// </remarks>
public abstract class JobCallbackHooks
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobCallbackHooks"/> class.
    /// </summary>
    protected JobCallbackHooks()
    {
    }

    /// <summary>
    /// Returns the host state to carry to a callback script is registering now, or <see langword="null"/> to carry nothing.
    /// </summary>
    /// <remarks>
    /// Called once per registration — a <c>then</c>, an <c>await</c>, a thenable being adopted, a
    /// <c>FinalizationRegistry</c> being constructed — not once per handler, and never for a reaction without a
    /// callable handler, which has no callback to run.
    /// </remarks>
    /// <param name="engine">The engine the callback is being registered with.</param>
    protected internal abstract object? Capture(Engine engine);

    /// <summary>
    /// Installs host state <see cref="Capture"/> returned, before the engine runs the callback it was captured for.
    /// </summary>
    /// <remarks>
    /// The engine always pairs a call that returns with exactly one <see cref="Exit"/>, including when the callback
    /// throws. Calls nest when a callback drains the event loop itself, so save the previous state in the token
    /// rather than in a field.
    /// </remarks>
    /// <param name="engine">The engine about to run the callback.</param>
    /// <param name="hostDefined">The non-null object <see cref="Capture"/> returned for this callback.</param>
    /// <returns>A token handed to the matching <see cref="Exit"/>, typically the state being replaced.</returns>
    protected internal abstract object? Enter(Engine engine, object hostDefined);

    /// <summary>
    /// Restores the host state <see cref="Enter"/> replaced, after the callback has returned or thrown.
    /// </summary>
    /// <param name="engine">The engine that ran the callback.</param>
    /// <param name="token">The token the matching <see cref="Enter"/> returned.</param>
    protected internal abstract void Exit(Engine engine, object? token);
}
