using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>
/// The hand-off between the thread AngleSharp's parser runs on and the page loop that owns the engine and
/// the DOM. Exactly one of the two holds the baton, and only the holder touches either.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why there are two threads at all.</b> AngleSharp's parse is an asynchronous method whose every
/// <c>await</c> carries <c>ConfigureAwait(false)</c>, so the moment a step of it genuinely suspends — which
/// an external <c>&lt;script src&gt;</c> is the first thing to do — the parse and the scripting hook with it
/// resume on a pool thread. Driving it on the page loop and blocking would therefore let the engine be
/// entered from two threads with nothing to say so. So the parse gets a thread of its own, and everything it
/// asks for that needs the engine or the DOM comes back here.
/// </para>
/// <para>
/// <b>The hand-off is a blocking handshake, not a continuation.</b> <see cref="RunOnLoop{T}"/> parks the
/// parser thread outright until the loop has finished the work, which is a stronger version of the
/// <c>RunContinuationsAsynchronously</c> the design named: the parser cannot resume inline on the loop
/// thread because it cannot resume at all until the loop releases it. The whole invariant follows from that
/// one property, and <see cref="ParserThreadId"/> is what lets a caller assert it rather than trust it.
/// </para>
/// <para>
/// <b>Timers fire exactly where a browser fires them.</b> While the parser is tokenizing it holds the baton
/// and the loop runs nothing — which is right, because in a browser the parser *is* the task the event loop
/// is running. While the loop is fetching a parser-blocking script it holds the baton and pumps
/// <see cref="Engine.TaskOperations.ProcessTasks"/>, so timers, promise jobs and animation frames run while
/// the page waits for the network. That is the browser-correct timing, and it is what
/// <see cref="PumpUntil(Task)"/> is for.
/// </para>
/// </remarks>
internal sealed class ParserBaton : IDisposable
{
    private readonly ConcurrentQueue<Request> _pending = new();
    private readonly SemaphoreSlim _arrived = new(0);
    private readonly Engine _engine;
    private readonly Engine.TaskOperations _tasks;
    private readonly TimeSpan _idle;
    private readonly CancellationToken _cancellationToken;

    private volatile int _parserThreadId;

    internal ParserBaton(Engine engine, TimeSpan idle, CancellationToken cancellationToken)
    {
        _engine = engine;
        _tasks = engine.Tasks;
        _idle = idle <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(5) : idle;
        _cancellationToken = cancellationToken;
    }

    /// <summary>The thread the loop runs on, which is the only thread that may touch the engine.</summary>
    internal int LoopThreadId { get; } = Environment.CurrentManagedThreadId;

    /// <summary>
    /// The thread the parse was last seen on, or <c>0</c> before it started. Compared against the thread the
    /// parse began on, a change means AngleSharp genuinely suspended somewhere this driver did not expect.
    /// </summary>
    internal int ParserThreadId => _parserThreadId;

    /// <summary>Whether the parse ever resumed on a thread other than the one it started on.</summary>
    internal bool ParserHopped { get; private set; }

    /// <summary>The engine the loop owns. Read only from the loop thread.</summary>
    internal Engine Engine => _engine;

    /// <summary>Runs <paramref name="work"/> on the page loop and blocks the parser thread until it is done.</summary>
    /// <remarks>
    /// Called on the parser thread. What <paramref name="work"/> throws is rethrown here with its stack
    /// preserved, so a failure inside a script or a fetch reaches AngleSharp as if it had happened inline.
    /// </remarks>
    internal T RunOnLoop<T>(Func<T> work)
    {
        Observe();

        var request = new Request(() => work());
        _pending.Enqueue(request);

        // The engine's own cross-thread entry, used for nothing but its documented side effect: it ends the
        // loop's park. The queue write happens first, so the wake can never be lost.
        _arrived.Release();
        _tasks.Post(static () => { });

        request.Done.Wait();

        if (request.Error is { } error)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return (T) request.Result!;
    }

    /// <summary>The same, for work with nothing to answer.</summary>
    internal void RunOnLoop(Action work) => RunOnLoop<object?>(() =>
    {
        work();
        return null;
    });

    /// <summary>
    /// Serves the parser until <paramref name="parse"/> is finished. Runs on the page loop and returns to it.
    /// </summary>
    internal void Serve(Task parse)
    {
        // The parse completing is itself a wake, so the loop never sits out a poll interval after the last
        // hand-off — and never has to poll at all.
        parse.ContinueWith(static (_, state) => ((SemaphoreSlim) state!).Release(), _arrived, TaskScheduler.Default);

        while (true)
        {
            _arrived.Wait();

            if (_pending.TryDequeue(out var request))
            {
                request.Run();
                continue;
            }

            if (parse.IsCompleted)
            {
                // A request cannot be in flight here: one implies the parser is blocked, which implies the
                // parse has not finished. Anything still queued was dequeued above.
                return;
            }
        }
    }

    /// <summary>
    /// Waits for <paramref name="task"/> on the loop, running due timers, promise jobs and animation frames
    /// while it is in flight. Called from inside served work, so the baton is held here.
    /// </summary>
    internal T PumpUntil<T>(Task<T> task)
    {
        PumpUntil((Task) task);
        return task.GetAwaiter().GetResult();
    }

    /// <inheritdoc cref="PumpUntil{T}(Task{T})" />
    internal void PumpUntil(Task task)
    {
        // The completion is a wake for the same reason the parse's is: without it the loop would sit out an
        // idle interval after the network answered.
        var tasks = _tasks;
        task.ContinueWith(static (_, state) => ((Engine.TaskOperations) state!).Post(static () => { }), tasks, TaskScheduler.Default);

        while (!task.IsCompleted)
        {
            try
            {
                tasks.ProcessTasks();
            }
            catch (Exception)
            {
                // A job erupting out of the pump must not abandon the parse: the diagnostics sink already
                // reports everything a callback throws, and what reaches here is what it does not cover.
                // Swallowing it here is the same trade PageLoop.Pump makes, for the same reason.
            }

            if (task.IsCompleted)
            {
                return;
            }

            var next = tasks.TimeUntilNextScheduledWork;
            var wait = next is { } due && due < _idle ? due : _idle;

            if (wait <= TimeSpan.Zero)
            {
                continue;
            }

            try
            {
                tasks.WaitForScheduledWork(wait, _cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The document is being left or the page is closing; the fetch is cancelled with the same
                // token, so the task is about to complete with a cancellation of its own.
                return;
            }
        }
    }

    /// <summary>
    /// Pumps until <paramref name="completed"/> answers <see langword="true"/> or <paramref name="deadline"/>
    /// passes, and answers whether it completed.
    /// </summary>
    /// <remarks>
    /// The overload for work that has no <see cref="Task"/> to await: a module import settles into the
    /// engine's own job queue, so it makes progress only when the loop gives it turns and there is nothing
    /// but the operation itself to ask.
    /// </remarks>
    internal bool PumpUntil(Func<bool> completed, TimeSpan deadline)
    {
        var tasks = _tasks;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        while (!completed())
        {
            if (System.Diagnostics.Stopwatch.GetElapsedTime(started) > deadline)
            {
                return false;
            }

            try
            {
                tasks.ProcessTasks();
            }
            catch (Exception)
            {
                // See the remarks on the Task overload: a job erupting out of the pump must not abandon the
                // load, and the diagnostics sink has already reported everything it covers.
            }

            if (completed())
            {
                return true;
            }

            var next = tasks.TimeUntilNextScheduledWork;
            var wait = next is { } due && due < _idle ? due : _idle;

            if (wait <= TimeSpan.Zero)
            {
                continue;
            }

            try
            {
                tasks.WaitForScheduledWork(wait, _cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return completed();
            }
        }

        return true;
    }

    /// <summary>Releases the wake handle, once the parse this baton served has finished.</summary>
    public void Dispose() => _arrived.Dispose();

    /// <summary>Records the thread the parse is on, and whether it has changed since it started.</summary>
    private void Observe()
    {
        var current = Environment.CurrentManagedThreadId;
        var previous = _parserThreadId;

        if (previous == 0)
        {
            _parserThreadId = current;
            return;
        }

        if (previous != current)
        {
            _parserThreadId = current;
            ParserHopped = true;
        }
    }

    /// <summary>One hand-off: what the loop is to run, and the parker waiting for it.</summary>
    private sealed class Request(Func<object?> work)
    {
        internal ManualResetEventSlim Done { get; } = new(false);

        internal object? Result { get; private set; }

        internal Exception? Error { get; private set; }

        internal void Run()
        {
            try
            {
                Result = work();
            }
            catch (Exception exception)
            {
                Error = exception;
            }
            finally
            {
                Done.Set();
            }
        }
    }
}
