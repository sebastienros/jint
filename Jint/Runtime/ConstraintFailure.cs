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
    /// <see cref="RecursionDepthOverflowException"/> belongs here for the same reason as the rest, and reaches
    /// these paths whenever host code re-enters the engine — a module resolve hook or a virtual file system
    /// written in script, which is a shape hosts really do use.
    /// <see cref="PlatformNotSupportedException"/> is on the list for the same reason but arrives by a
    /// different route: <see cref="Jint.Constraints.MemoryLimitConstraint"/> raises it when the runtime does
    /// not expose <c>GC.GetAllocatedBytesForCurrentThread</c>, so a configured allocation budget cannot be
    /// enforced at all. That is a bound failing closed, and a job that turned it into a rejection would go on
    /// running unbounded.
    /// </remarks>
    internal static bool MustPropagate(Exception exception) => exception
        is ExecutionCanceledException
        or ParsingLimitException
        or MemoryLimitExceededException
        or StatementsCountOverflowException
        or RecursionDepthOverflowException
        or TimeoutException
        or OperationCanceledException
        or PlatformNotSupportedException
        or OutOfMemoryException;
}
