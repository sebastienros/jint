#nullable enable

using System.Runtime.ExceptionServices;
using Xunit.Sdk;

namespace Jint.Tests;

/// <summary>
/// Runs a test body on a dedicated thread, rather than on the xUnit worker thread.
/// <para>
/// Two unrelated reasons to want one, both served here so there is a single implementation. Deep
/// recursion needs a larger stack than the default thread provides
/// (<see cref="LargeStackSize"/>). And a test whose failure mode is a <em>non-terminating</em>
/// engine — an uninterruptible walk over a corrupt lexical environment chain, say — needs
/// <paramref name="joinTimeout"/>: an execution constraint cannot stop one of those, because Jint
/// does not evaluate constraints for event-loop jobs and so cannot interrupt a continuation.
/// Without a timeout a single regression wedges the whole class (xUnit runs a class's tests
/// sequentially) and CI hangs instead of reporting.
/// </para>
/// <para>
/// The exception is re-thrown through <see cref="ExceptionDispatchInfo"/>, which preserves its
/// type, its original stack trace and its <see cref="Exception.Data"/> — a regression surfaces in
/// CI as a navigable failure rather than as a flattened string.
/// </para>
/// </summary>
internal static class DedicatedThread
{
    /// <summary>
    /// Enough stack for the deep-recursion tests; the platform default is far smaller.
    /// </summary>
    public const int LargeStackSize = 16 * 1024 * 1024;

    public static void Run(Action action, TimeSpan? joinTimeout = null, string? timeoutMessage = null, int maxStackSize = LargeStackSize)
    {
        ExceptionDispatchInfo? exception = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception = ExceptionDispatchInfo.Capture(e);
                }
            },
            maxStackSize)
        {
            // A thread that outlives its join must not keep the process alive...
            IsBackground = true,

            // ...and must not compete with the rest of the run for a core while it does. Background
            // only governs process exit; a spinning thread goes on burning CPU until then, which is
            // exactly the condition the suite's known wall-clock flakes fail under.
            Priority = ThreadPriority.Lowest,
        };

        thread.Start();

        if (joinTimeout is { } timeout)
        {
            if (!thread.Join(timeout))
            {
                throw new XunitException(timeoutMessage ?? $"the test body did not complete within {timeout}");
            }
        }
        else
        {
            thread.Join();
        }

        exception?.Throw();
    }
}
