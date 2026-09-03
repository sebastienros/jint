using System.Threading.Channels;
using Jint.Runtime;

namespace Jint.Browser.Runtime;

/// <summary>
/// The one thread a page owns, and the only thread that ever touches its engine or its DOM.
/// </summary>
/// <remarks>
/// <para>
/// The shape is the engine's documented host loop: drain the mailbox, call <c>Tasks.ProcessTasks()</c>, then
/// park in <c>Tasks.WaitForScheduledWork</c> for the shorter of the idle interval and whatever the engine
/// says is next. Jint never starts a thread of its own, so a page that is not pumped runs no timer callback
/// and settles no promise; this is what pumps it.
/// </para>
/// <para>
/// <b>Every public <see cref="Page"/> method is a mailbox request.</b> A caller on another thread posts a
/// delegate and awaits its completion, and the delegate is what holds the engine — never the caller. That is
/// what keeps a <c>JsValue</c> from leaving the thread that owns it, and it is why every value crossing back
/// out is converted here before the task completes.
/// </para>
/// <para>
/// The engine is built on this thread and disposed on it, because both are engine-owning operations. A
/// request that arrives after the loop has stopped is failed with <see cref="ObjectDisposedException"/>
/// rather than left to hang.
/// </para>
/// <para>
/// <b>Every turn is bracketed.</b> One mailbox request and one <c>ProcessTasks</c> drain are each one turn,
/// and each takes the page's <see cref="PageBudget"/> — see
/// <see cref="BrowserOptions.MaxTaskDuration"/>. A request that runs out of budget fails its own task,
/// because the bracket is outside <see cref="PostAsync{T}"/>'s own <c>catch</c>; a drain that runs out
/// erupts here and is recorded like anything else that erupts, and the loop goes on.
/// </para>
/// </remarks>
internal sealed class PageLoop : IDisposable
{
    private readonly Channel<LoopRequest> _mailbox = Channel.CreateUnbounded<LoopRequest>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });

    private readonly CancellationTokenSource _closing = new();
    private readonly CancellationToken _closingToken;
    private CancellationTokenSource _documentClosing = new();
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<Engine> _engineFactory;
    private readonly Action<Exception> _onPumpError;
    private readonly Action<Engine>? _onTurnEnd;
    private readonly TimeSpan _idle;
    private readonly Thread _thread;

    private volatile Engine? _engine;
    private volatile Engine.TaskOperations? _tasks;
    private volatile PageBudget? _budget;
    private volatile bool _closed;
    private volatile int _turns;
    private Action<Engine?>? _beforeStop;

    /// <summary>The turn the loop currently has open, if any. Loop thread only.</summary>
    private PageBudget.TurnScope _turn;
    private bool _inTurn;

    /// <param name="name">What the loop's thread is called.</param>
    /// <param name="idle">The ceiling on a park with nothing due.</param>
    /// <param name="engineFactory">Builds the first engine, on the loop thread.</param>
    /// <param name="onPumpError">Where anything that erupts out of the pump is recorded.</param>
    /// <param name="onTurnEnd">
    /// What the page does at the end of each of the loop's own turns, with the engine the turn ran on, or
    /// <see langword="null"/> for a loop that does nothing there. It runs <i>outside</i> the turn's budget
    /// bracket, so a page's own bookkeeping cannot spend the budget a caller's request is bounded by or fail
    /// that request; anything it throws goes to <paramref name="onPumpError"/> like everything else the loop
    /// runs, because a page survives what watches it as well as what it runs.
    /// </param>
    internal PageLoop(
        string name,
        TimeSpan idle,
        Func<Engine> engineFactory,
        Action<Exception> onPumpError,
        Action<Engine>? onTurnEnd = null)
    {
        _idle = idle;
        _engineFactory = engineFactory;
        _onPumpError = onPumpError;
        _onTurnEnd = onTurnEnd;
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _closingToken = _closing.Token;
    }

    /// <summary>The page's cancellation token, cancelled when the page closes.</summary>
    /// <remarks>
    /// Taken from the source once, at construction, rather than read from it per call: the source is
    /// disposed as the page finishes closing, and the caller who most needs to see the cancellation is
    /// exactly the one still running then — a navigation the close overtook. A token already handed out
    /// keeps answering after its source is disposed; the <c>Token</c> property does not.
    /// </remarks>
    internal CancellationToken Closing => _closingToken;

    /// <summary>Whether the loop has been asked to stop.</summary>
    internal bool IsClosed => _closed;

    /// <summary>Cancellation for work tied to the engine and document the loop currently owns.</summary>
    /// <remarks>Read only from a mailbox request running on the loop thread.</remarks>
    internal CancellationToken DocumentClosing => _documentClosing.Token;

    /// <summary>The thread this loop runs on, which is the only thread allowed to touch the engine.</summary>
    internal Thread Thread => _thread;

    /// <summary>How many turns this loop has taken since it started.</summary>
    /// <remarks>
    /// <para>
    /// <b>One turn is what the page runtime's <c>AGENTS.md</c> calls one</b>, counted at the two places the
    /// loop takes one: a <b>bracketed mailbox request</b>, and a <b><c>ProcessTasks</c> drain</b>. Both are
    /// counted as they open, so a value read from the loop thread itself — from inside a request, a job or a
    /// listener — is the ordinal of the turn currently running, and two things that read the same number ran
    /// in the same turn. A drain that finds nothing due still counts, because the loop still took it.
    /// </para>
    /// <para>
    /// <b>A request posted <c>bracketed: false</c> is not a turn, and neither is a drain it performs.</b>
    /// Those are the pumps — <c>Page.WaitForIdleAsync</c> and <c>Page.WaitForNavigationAsync</c> — and each
    /// takes the page's <see cref="PageBudget"/> over its own drains directly rather than through the loop,
    /// so the count stands still for as long as one of them holds the thread. That is the honest reading:
    /// the loop is not turning, something else is using it.
    /// </para>
    /// <para>
    /// <b><see cref="ReplaceEngine"/>'s close-and-reopen is not a turn either.</b> A navigation is one
    /// mailbox request from the loop's point of view, and the swap in the middle of it re-arms the budget on
    /// the incoming engine rather than starting a new unit of work — so a document is replaced without the
    /// count moving, and a scheduling test that measures a navigation measures the request.
    /// </para>
    /// <para>
    /// An <see cref="int"/> and not a <see cref="long"/>, because the field is <c>volatile</c> — which is
    /// what a field written here and read from another thread has to be, and which C# does not allow on a
    /// 64-bit one. It is a scheduling instrument rather than a meter, and it wraps where an
    /// <see cref="int"/> does.
    /// </para>
    /// </remarks>
    internal int Turns => _turns;

    /// <summary>
    /// The engine the loop currently owns, for the members already running on the loop thread.
    /// </summary>
    /// <remarks>
    /// Reading it from anywhere else is the thread rule broken: an engine belongs to this thread, and the
    /// only sound way to reach one from outside is <see cref="PostAsync{T}"/>. It exists for the seams a
    /// script reaches — <c>history.back()</c> — which are already on the loop and need the engine they were
    /// called from without posting to themselves.
    /// </remarks>
    internal Engine? CurrentEngine => _engine;

    /// <summary>Starts the thread and completes once the engine exists, or faults if building it threw.</summary>
    internal Task StartAsync()
    {
        _thread.Start();
        return _started.Task;
    }

    /// <summary>Runs <paramref name="work"/> on the loop thread and completes when it returns.</summary>
    internal Task PostAsync(Action<Engine> work) => PostAsync<object?>(engine =>
    {
        work(engine);
        return null;
    });

    /// <summary>Runs <paramref name="work"/> on the loop thread and completes with what it answered.</summary>
    /// <param name="work">What to do with the engine, on the thread that owns it.</param>
    /// <param name="bracketed">
    /// Whether the request is one turn and takes the page's turn budget. <see langword="false"/> is for a
    /// request that <i>pumps</i> — it brackets each drain itself, so bracketing the request as well would
    /// charge a whole wait to one turn's budget and fail it. There are two of those and both are here in the
    /// package; a member added later wants the default.
    /// </param>
    internal Task<T> PostAsync<T>(Func<Engine, T> work, bool bracketed = true)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Request(Engine? engine)
        {
            if (engine is null)
            {
                completion.TrySetException(Disposed());
                return;
            }

            try
            {
                completion.TrySetResult(work(engine));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        if (_closed || !_mailbox.Writer.TryWrite(new LoopRequest(Request, bracketed)))
        {
            return Task.FromException<T>(Disposed());
        }

        // The engine's own cross-thread entry, used here for nothing but its documented side effect: it ends
        // a park. Without it a request would wait out the idle interval before the loop looked at the
        // mailbox again, and a page that had nothing else to do would answer every call late.
        //
        // EventLoop.WakeJob rather than an empty lambda of this file's own, because a checkpoint identifies a
        // wake by reference: this job sits on the queue for as long as the request takes, and any other
        // Action there would cost a listener returning to an empty stack the microtask checkpoint it is owed.
        try
        {
            _tasks?.Post(EventLoop.WakeJob);
        }
        catch (ObjectDisposedException)
        {
            // The loop disposed its engine between the mailbox write above and this line — it is stopping, or
            // a navigation is swapping the engine under us — and Engine.Tasks.Post refuses a disposed engine.
            // This call is only ever a wake: the request is already in the mailbox, where the loop's teardown
            // fails it or the next turn reads it. PostAsync's failure channel is the Task it returns, so this
            // must never leave synchronously.
        }

        return completion.Task;
    }

    /// <summary>Stops the loop, disposes the engine on its own thread, and completes when the thread ends.</summary>
    /// <param name="beforeStop">
    /// Teardown to run on the loop thread, after the last turn and before the engine is disposed.
    /// </param>
    /// <remarks>
    /// <paramref name="beforeStop"/> is not a mailbox request, and that is the point: a request would queue
    /// behind whatever is running, and what is running may be a wait this very call is trying to end. It runs
    /// in the loop's own teardown, where nothing can be ahead of it. A loop that never started runs it never.
    /// </remarks>
    internal Task CloseAsync(Action<Engine?>? beforeStop = null)
    {
        if (!_closed)
        {
            _beforeStop = beforeStop;
            _closed = true;
            _mailbox.Writer.TryComplete();
        }

        // Outside the guard, because the flag is also set by a loop whose engine failed to build, and a
        // token that is disposed without ever having been cancelled is one nothing can wait on. Cancel
        // is idempotent, so calling it on the way through a second close costs nothing.
        try
        {
            _closing.Cancel();
        }
        catch (AggregateException)
        {
            // A registration threw on its way out; the loop is stopping either way.
        }
        catch (ObjectDisposedException)
        {
            // Closed and disposed already; the token it handed out is cancelled and stays readable.
        }

        return _stopped.Task;
    }

    /// <summary>Releases the cancellation source, after <see cref="CloseAsync"/> has completed.</summary>
    public void Dispose()
    {
        _documentClosing.Dispose();
        _closing.Dispose();
    }

    private static ObjectDisposedException Disposed()
        => new(nameof(Page), "The page has been closed; its engine and its document no longer exist.");

    /// <summary>
    /// Replaces the engine a navigation ends, disposing the old one. Callable only from the loop thread.
    /// </summary>
    /// <remarks>
    /// A navigation is a new realm in a browser and a new engine here, and both halves of the swap are
    /// engine-owning operations — so both happen on the one thread that owns them, from inside a mailbox
    /// request the pump is already running.
    /// </remarks>
    internal Engine ReplaceEngine(Func<Engine> factory)
    {
        var previous = _engine;
        var previousDocumentClosing = _documentClosing;
        _documentClosing = new CancellationTokenSource();

        try
        {
            previousDocumentClosing.Cancel();
        }
        catch (AggregateException exception)
        {
            _onPumpError(exception);
        }

        // Before the swap, and before the new engine is built: a constraint belongs to one engine, so the
        // turn the request opened has to be closed on the engine it was opened on. Building the replacement
        // then costs the outgoing turn nothing, which also means the first script of the new document meets
        // a full budget rather than one an engine construction already spent.
        var bracketed = _inTurn;
        EndTurn();

        var replacement = factory();

        _engine = replacement;
        _tasks = replacement.Tasks;
        _budget = PageRuntime.Find(replacement)?.Budget;

        // The rest of the request — the parse, its inline scripts, DOMContentLoaded and load — is a turn of
        // the engine that will run it.
        BeginTurn(bracketed);

        if (previous is not null)
        {
            try
            {
                previous.Dispose();
            }
            catch (Exception exception)
            {
                _onPumpError(exception);
            }
        }

        return replacement;
    }

    private void Run()
    {
        try
        {
            var engine = _engineFactory();

            // Cached before the engine is visible to any other thread, because Engine.Tasks materializes on
            // first read and that read is not thread-safe. Everything after this point uses the one instance.
            _tasks = engine.Tasks;
            _budget = PageRuntime.Find(engine)?.Budget;
            _engine = engine;
            _started.TrySetResult();
        }
        catch (Exception exception)
        {
            _closed = true;
            _mailbox.Writer.TryComplete();
            FailPending();
            _started.TrySetException(exception);
            _stopped.TrySetResult();
            return;
        }

        try
        {
            Pump();
        }
        finally
        {
            _closed = true;
            _mailbox.Writer.TryComplete();
            FailPending();

            try
            {
                _beforeStop?.Invoke(_engine);
            }
            catch (Exception exception)
            {
                _onPumpError(exception);
            }

            try
            {
                _engine?.Dispose();
            }
            catch (Exception exception)
            {
                _onPumpError(exception);
            }

            _stopped.TrySetResult();
        }
    }

    private void Pump()
    {
        var reader = _mailbox.Reader;

        while (!_closing.IsCancellationRequested)
        {
            while (!_closing.IsCancellationRequested && reader.TryRead(out var request))
            {
                // The bracket is outside the request rather than inside it, and that is what makes a budget
                // failure the caller's: PostAsync's own try/catch is inside this one, so a TimeoutException
                // from the turn's deadline faults that request's Task and nothing reaches the pump.
                BeginLoopTurn(request.Bracketed);

                try
                {
                    request.Work(_engine);
                }
                finally
                {
                    EndLoopTurn();
                }
            }

            var tasks = _tasks!;

            if (_closing.IsCancellationRequested)
            {
                return;
            }

            try
            {
                // One drain is one turn: every timer callback, microtask, promise reaction and animation
                // frame that was due shares one time and one allocation budget, because none of them reaches
                // ExecuteWithConstraints and so none of them is bounded by a per-entry limit at all.
                BeginLoopTurn(bracketed: true);

                try
                {
                    tasks.ProcessTasks();
                }
                finally
                {
                    EndLoopTurn();
                }
            }
            catch (Exception exception)
            {
                // A job erupting out of the pump would otherwise end the page. The engine's diagnostics sink
                // catches everything a callback throws; what reaches here is what the sink does not cover, so
                // it is recorded and the loop goes on — a page survives its scripts.
                _onPumpError(exception);
            }

            if (_closing.IsCancellationRequested || reader.TryPeek(out _))
            {
                continue;
            }

            var next = tasks.TimeUntilNextScheduledWork;
            var wait = next is { } due && due < _idle ? due : _idle;
            if (wait <= TimeSpan.Zero)
            {
                continue;
            }

            try
            {
                tasks.WaitForScheduledWork(wait, _closing.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _onPumpError(exception);
            }
        }
    }

    /// <summary>Opens one of the loop's own turns: counts it, then arms the budget over it.</summary>
    /// <remarks>
    /// Counting here rather than in <see cref="BeginTurn"/> is what makes <see cref="Turns"/> the count of
    /// the loop's own turns and nothing else. <see cref="ReplaceEngine"/> re-brackets the request it is
    /// already running, on the engine that replaced the one the bracket was opened on; that is a budget
    /// re-armed rather than a second turn, and it goes to <see cref="BeginTurn"/> directly.
    /// </remarks>
    private void BeginLoopTurn(bool bracketed)
    {
        if (bracketed)
        {
            // Written on the loop thread and on no other, so the increment needs nothing beyond the volatile
            // write the field already is: every reader is on another thread and reads a published value.
            _turns++;
        }

        BeginTurn(bracketed);
    }

    /// <summary>Opens the loop's own turn on whichever engine it currently owns.</summary>
    private void BeginTurn(bool bracketed)
    {
        if (!bracketed || _budget is not { } budget)
        {
            return;
        }

        _turn = budget.BeginTurn();
        _inTurn = true;
    }

    /// <summary>Closes one of the loop's own turns: unarms the budget, then tells the page it ended.</summary>
    /// <remarks>
    /// <para>
    /// The hook runs <b>after</b> <see cref="EndTurn"/> and not inside it, for two reasons and both matter.
    /// A turn's budget bounds the work the turn was <i>for</i>: charging a page's own end-of-turn bookkeeping
    /// to it would let that bookkeeping fail a caller's request with a <see cref="TimeoutException"/> the
    /// caller could do nothing about. And <see cref="ReplaceEngine"/> closes a turn mid-request through
    /// <see cref="EndTurn"/> directly, so a navigation runs the hook <b>once</b> — at the end of the request,
    /// on the engine the swap installed and the document now showing — rather than twice, once about a
    /// document that has been thrown away.
    /// </para>
    /// <para>
    /// It is wrapped, like everything else the loop runs on a page's behalf. A page survives its scripts;
    /// it survives what watches them too.
    /// </para>
    /// </remarks>
    private void EndLoopTurn()
    {
        EndTurn();

        if (_onTurnEnd is not { } onTurnEnd || _engine is not { } engine)
        {
            return;
        }

        try
        {
            onTurnEnd(engine);
        }
        catch (Exception exception)
        {
            _onPumpError(exception);
        }
    }

    /// <summary>Closes it, if one is open. Safe to call when none is.</summary>
    private void EndTurn()
    {
        if (!_inTurn)
        {
            return;
        }

        _inTurn = false;
        var turn = _turn;
        _turn = default;
        turn.Dispose();
    }

    private void FailPending()
    {
        while (_mailbox.Reader.TryRead(out var request))
        {
            request.Work(null);
        }
    }

    /// <summary>One item of the mailbox: what to run, and whether the loop brackets it as a turn.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct LoopRequest(Action<Engine?> Work, bool Bracketed);
}
