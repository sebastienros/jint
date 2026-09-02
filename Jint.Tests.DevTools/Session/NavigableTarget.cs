using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// A target that replaces its engine, which is what a page does on every navigation.
/// </summary>
/// <remarks>
/// <para>
/// The suite declares one rather than waiting for <c>Jint.Browser</c> to bring one, for the reason
/// <c>DomainLifecycleTests</c> declares a domain: an extension point nothing exercises is a design nobody has
/// tried. It is the smallest thing that is a real target — an identity, a loop thread that owns its engine,
/// and <see cref="DevToolsTarget.Replace"/> called from that thread — and everything the built-in domains do
/// about a swap is visible through it.
/// </para>
/// <para>
/// The loop is the shape every host's is: drain the mailbox, run the event loop, park. It reads
/// <see cref="DevToolsTarget.Runtime"/> afresh on every turn, because that is the whole point — a loop that
/// captured the engine would go on pumping the document that was replaced.
/// </para>
/// </remarks>
internal sealed class NavigableTarget : DevToolsTarget
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly Thread _thread;

    private int _closed;

    /// <summary>Creates a target over <paramref name="engine"/> and starts pumping it.</summary>
    /// <param name="engine">The engine of the first document.</param>
    /// <param name="url">Where the target says it is.</param>
    /// <param name="browserContextId">Which context it belongs to, or <see langword="null"/>.</param>
    /// <param name="waitForDebuggerOnStart">Whether it runs nothing until a client releases it.</param>
    internal NavigableTarget(
        Engine engine,
        string url = "about:blank",
        string? browserContextId = null,
        bool waitForDebuggerOnStart = false)
        : base(
            type: "page",
            title: "Test page",
            url: url,
            browserContextId: browserContextId,
            openerId: null,
            describer: null,
            waitForDebuggerOnStart: waitForDebuggerOnStart)
    {
        InstallRuntime(engine);

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Navigable target " + TargetId,
        };

        _thread.Start();
    }

    /// <summary>Commits the next document, which builds an engine and hands it to the target.</summary>
    /// <param name="next">What builds the engine, run on the loop thread.</param>
    /// <returns>A task that completes once the swap has been made and announced.</returns>
    /// <remarks>
    /// Posted rather than called, because replacing the engine is an engine-owning operation and the caller
    /// is a test thread. That is the arrangement a page is in: the loop that runs the old document is the one
    /// that builds the new.
    /// </remarks>
    internal Task NavigateAsync(Func<Engine> next)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Runtime.Dispatcher.Post(_ =>
        {
            try
            {
                Replace(next());
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });

        return completion.Task;
    }

    /// <summary>Runs host work on the loop thread and answers what it returned.</summary>
    internal Task<T> PostAsync<T>(Func<Engine, T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        Runtime.Dispatcher.Post(engine =>
        {
            try
            {
                completion.TrySetResult(work(engine));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });

        return completion.Task;
    }

    /// <inheritdoc cref="PostAsync{T}"/>
    internal Task PostAsync(Action<Engine> work) => PostAsync<object?>(engine =>
    {
        work(engine);
        return null;
    });

    /// <inheritdoc/>
    internal override async ValueTask CloseAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        ActiveDebugger?.Detach();
        await _stopping.CancelAsync().ConfigureAwait(false);

        if (_thread.IsAlive)
        {
            _thread.Join(TimeSpan.FromSeconds(5));
        }

        DisposeRuntime();
        _stopping.Dispose();
    }

    private void Run()
    {
        var token = _stopping.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                // Read afresh: a navigation replaces all three of these under the loop.
                var runtime = Runtime;
                runtime.Dispatcher.Drain();
                runtime.Engine.Tasks.ProcessTasks();
                runtime.Engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(20), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
#pragma warning disable CA1031 // the loop is the last thing between one bad command and a dead target
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }
}
