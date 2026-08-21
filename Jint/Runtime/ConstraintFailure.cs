namespace Jint.Runtime;

/// <summary>
/// The failures that must keep propagating out of an event-loop job rather than becoming that job's own
/// error value.
/// </summary>
/// <remarks>
/// Every one of them exists to bound or abort execution, and a bound that turns into a promise rejection no
/// longer bounds anything — script observes it as an ordinary failed operation and carries on, in a loop if
/// it likes. Any cross-thread completion that catches broadly on a queued turn — an asynchronous module load,
/// a <c>fetch</c> — has to consult this list, which is why it lives here rather than on one of them.
/// </remarks>
internal static class ConstraintFailure
{
    /// <summary>
    /// Whether <paramref name="exception"/> must escape the job it was raised in.
    /// </summary>
    /// <remarks>
    /// A job boundary is the host boundary plus two: <see cref="Throw.MustPropagateHostException"/> is the
    /// list a CLR call is not allowed to swallow, and this is that list with
    /// <see cref="TimeoutException"/> and <see cref="OperationCanceledException"/> added. The two are apart
    /// on purpose — a host method may raise either of those as its own ordinary failure, so an interop
    /// boundary cannot read them as a bound, while for a queued turn they are Jint's own timeout and
    /// cancellation and nothing else. Defining this one over the other is what stops them drifting.
    /// <see cref="RecursionDepthOverflowException"/> reaches <i>these</i> paths whenever host code re-enters
    /// the engine — a module resolve hook or a virtual file system written in script, which is a shape hosts
    /// really do use.
    /// </remarks>
    internal static bool MustPropagate(Exception exception)
        => Throw.MustPropagateHostException(exception)
           || exception is TimeoutException or OperationCanceledException;
}
