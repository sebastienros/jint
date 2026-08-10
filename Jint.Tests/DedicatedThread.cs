#nullable enable

using System.Runtime.ExceptionServices;
using Xunit.Sdk;

namespace Jint.Tests;

/// <summary>
/// Runs a test body on a dedicated thread, rather than on the xUnit worker thread.
/// <para>
/// Three unrelated reasons to want one, all served here so there is a single implementation. Deep
/// recursion needs a larger stack than the default thread provides
/// (<see cref="LargeStackSize"/>). A test whose failure mode is a <em>non-terminating</em>
/// engine — an uninterruptible walk over a corrupt lexical environment chain, say — needs
/// <paramref name="joinTimeout"/>: an execution constraint cannot stop one of those, because Jint
/// does not evaluate constraints for event-loop jobs and so cannot interrupt a continuation.
/// Without a timeout a single regression wedges the whole class (xUnit runs a class's tests
/// sequentially) and CI hangs instead of reporting. And a body that <em>blocks</em> on wall-clock
/// asynchronous work must not do so on a thread-pool thread — that is <see cref="RunAsync"/>, whose
/// doc explains why it is the asynchronous one that solves it.
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

    /// <summary>
    /// The platform default stack, which is what a body wanting the thread rather than the stack should
    /// ask for. <see cref="LargeStackSize"/> reserves 16 MB of address space per concurrent test, and the
    /// tests that need it are a small, deliberate set.
    /// </summary>
    public const int DefaultStackSize = 0;

    /// <summary>
    /// Runs <paramref name="action"/> on a dedicated thread and hands back a task that completes with it,
    /// so a <c>Task</c>-returning test releases its thread-pool worker for the whole time the body blocks.
    /// <para>
    /// That release is the entire point, and it is why the synchronous <see cref="Run"/> cannot be used
    /// for this: xUnit runs test bodies on <c>.NET TP Worker</c> threads, so a <see cref="Thread.Join()"/>
    /// keeps the pool exactly one worker short — the same shortage the body was moved off the pool to
    /// avoid. A test that blocks on wall-clock asynchronous work is waiting on a continuation that itself
    /// needs a pool worker (a <see cref="Task.Delay(int)"/> resumption, a <c>CancelAfter</c> callback, an
    /// asynchronous module load settling), so blocking a worker to wait for one is a resource inversion:
    /// under enough parallelism the pool's injection rate — order one thread per 500 ms once saturated —
    /// decides whether the test's promise budget is met, and on a small CI runner it is not. Returning the
    /// task instead costs the pool nothing while the body waits.
    /// </para>
    /// </summary>
    public static Task RunAsync(Action action, int maxStackSize = DefaultStackSize)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                    completion.SetResult(true);
                }
                catch (Exception e)
                {
                    completion.SetException(e);
                }
            },
            maxStackSize)
        {
            IsBackground = true,
        };

        thread.Start();

        return completion.Task;
    }

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
