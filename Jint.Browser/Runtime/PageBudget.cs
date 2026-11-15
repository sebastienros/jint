using System.Diagnostics;
using System.Globalization;
using Jint.Constraints;
using Jint.Runtime;

namespace Jint.Browser.Runtime;

/// <summary>
/// The two constraints that bound one turn of a page's loop, and the bracket that arms them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a bracket at all.</b> The repository-root <c>AGENTS.md</c> gotcha applies to a page twice over. A
/// page is a host-driven sequence of entries, so <c>Options.LimitExecutionTime</c> re-arms its deadline for
/// each one and bounds none of them together; and a page's event loop is <i>pumped</i>, so a job chain
/// running under <c>Tasks.ProcessTasks</c> never reaches <c>ExecuteWithConstraints</c> at all and a
/// per-entry timeout there never fires. The two constraints whose <c>Reset()</c> the host owns are the only
/// ones that survive both — <see cref="OperationDeadlineConstraint"/> for wall-clock and
/// <see cref="MemoryLimitConstraint"/> for allocation — and this is where a page arms them.
/// </para>
/// <para>
/// <b>One code path for both profiles.</b> The constraints are found on the engine rather than created here:
/// under <see cref="BrowserOptions.ForUntrustedContent"/> they are the pair
/// <c>Options.ForUntrustedCode</c> registered, and otherwise they are the pair
/// <c>BrowserEngineFactory</c> registered from <see cref="BrowserOptions.MaxTaskDuration"/> and
/// <see cref="BrowserOptions.MemoryLimit"/>. Either way a turn is armed the same way, which is what
/// <c>UntrustedCodeLimits.BeginOperation</c> does for a host operation — that method is not called here
/// because the budget a page turn takes is <see cref="BrowserOptions.MaxTaskDuration"/> rather than the
/// limits' own <c>MaxOperationDuration</c>, and because a page owns its engine and needs no reference-equality
/// handshake to prove it.
/// </para>
/// <para>
/// <b>A nested turn is a turn.</b> An inline <c>&lt;script&gt;</c> runs inside the mailbox request that is
/// parsing the document, so a bracket can be entered while one is already open. The inner one re-arms the
/// deadline with a full budget of its own and hands the enclosing turn a full budget back on the way out:
/// each script is bounded, and a document with many of them is not failed because it had many. The
/// allocation budget is <i>not</i> re-armed for a nested turn — the enclosing turn's is what bounds it —
/// because <see cref="MemoryLimitConstraint.Begin"/> refuses to start while the engine is executing, which
/// is exactly where a nested turn may be opened from once the parser driver can run a script from inside
/// one. So the two halves of a nested turn end differently and deliberately: a script that runs out of
/// <i>time</i> ends and the parse goes on, while one that runs out of <i>allocation</i> has spent the whole
/// turn's budget and the navigation fails with it. A document that allocated a page's budget away has
/// nothing left to finish loading with.
/// </para>
/// <para>
/// Everything here is touched on the thread that owns the engine, which is the page loop for a page and the
/// worker's own pump for a worker.
/// </para>
/// </remarks>
internal sealed class PageBudget : IEventLoopTaskBudget
{
    private readonly OperationDeadlineConstraint? _deadline;
    private readonly MemoryLimitConstraint? _memory;
    private readonly TimeSpan _turn;
    private readonly long _memoryLimit;
    private int _depth;
    private long _serial;
    private long _deadlineArmedAt;

    private PageBudget(OperationDeadlineConstraint? deadline, MemoryLimitConstraint? memory, TimeSpan turn, long memoryLimit)
    {
        _deadline = deadline;
        _memory = memory;
        _turn = turn;
        _memoryLimit = memoryLimit;
    }

    /// <summary>The budget bracketing every turn of <paramref name="engine"/>, or a disabled one.</summary>
    /// <remarks>
    /// A <see cref="MemoryLimitConstraint"/> is taken only where the runtime can account allocations at all:
    /// its <c>Begin</c> throws <see cref="PlatformNotSupportedException"/> where it cannot, and a page loop
    /// is the wrong place to discover that. Where accounting is unavailable the engine's own per-entry path
    /// fails just as loudly on the first entry, so nothing is hidden by declining it here.
    /// </remarks>
    internal static PageBudget For(Engine engine, BrowserOptions options)
    {
        var turn = options.MaxTaskDuration;
        var deadline = turn > TimeSpan.Zero ? engine.Constraints.Find<OperationDeadlineConstraint>() : null;

        var memory = MemoryLimitConstraint.Accuracy == MemoryLimitAccuracy.ExecutionThread
            ? engine.Constraints.Find<MemoryLimitConstraint>()
            : null;

        var budget = new PageBudget(deadline, memory, turn, options.MemoryLimit);
        engine.Tasks.ConfigureTaskBudget(budget);
        return budget;
    }

    bool IEventLoopTaskBudget.BeginTask(bool isTask)
    {
        if (!isTask && _depth > 0)
        {
            return false;
        }

        BeginTurn();
        return true;
    }

    void IEventLoopTaskBudget.EndTask()
    {
        if (IsArmed)
        {
            EndTurn();
        }
    }

    /// <summary>Gets the number of the outermost turn opened most recently, or zero before the first.</summary>
    /// <remarks>
    /// Read on the loop, after a turn's failure has reached the pump: the turn that failed is still the latest
    /// one, because nothing has opened another yet. It is what a protocol command awaiting a promise is tagged
    /// with, so that the stop of its own task can answer it (<c>EngineDispatcher.StopAwaiting</c>).
    /// </remarks>
    internal long Turn => _serial;

    /// <summary>
    /// The sentence naming the budget that stopped a turn, or <see langword="null"/> when
    /// <paramref name="exception"/> is not one of this bracket's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which of the page's options to raise is the whole message.</b> The constraint says "the operation's
    /// time budget elapsed", which a reader of a Playwright failure cannot act on: the budget is
    /// <see cref="BrowserOptions.MaxTaskDuration"/>, which <c>jint-browser</c> spells
    /// <c>--max-task-duration</c>, and the page goes on running after it (#4619).
    /// </para>
    /// <para>
    /// A <see cref="TimeoutException"/> is this bracket's only when the turn's own deadline has actually
    /// passed. Under <see cref="BrowserOptions.ForUntrustedContent"/> the profile's per-entry
    /// <c>LimitExecutionTime</c> raises the same type, and naming the turn budget for that one would send the
    /// reader to the wrong option. The check is against the clock the deadline is armed from, read before it
    /// is armed, so it can only err towards the constraint's own message.
    /// </para>
    /// </remarks>
    internal string? Describe(Exception exception)
    {
        if (exception is TimeoutException
            && _deadline is not null
            && Stopwatch.GetElapsedTime(Volatile.Read(ref _deadlineArmedAt)) >= _turn)
        {
            return $"Task exceeded MaxTaskDuration ({Format(_turn)}): one task and its microtasks may run for at most "
                + $"{Format(_turn)}. Raise BrowserOptions.MaxTaskDuration (jint-browser --max-task-duration); 0 removes the ceiling.";
        }

        if (exception is MemoryLimitExceededException && _memory is not null && _memoryLimit > 0)
        {
            return $"Task exceeded MemoryLimit ({_memoryLimit.ToString(CultureInfo.InvariantCulture)} bytes): one task and its "
                + "microtasks allocated more than that. Raise BrowserOptions.MemoryLimit (jint-browser --memory-limit).";
        }

        return null;
    }

    /// <summary>A duration the way the command line takes one: <c>5s</c>, <c>250ms</c>, <c>1.5s</c>, <c>2m</c>.</summary>
    internal static string Format(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
        {
            return duration.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + "ms";
        }

        if (duration.TotalMinutes >= 1 && duration.Ticks % TimeSpan.TicksPerMinute == 0)
        {
            return duration.TotalMinutes.ToString("0", CultureInfo.InvariantCulture) + "m";
        }

        return duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";
    }

    /// <summary>Whether anything is actually bounded, which is what makes a turn worth bracketing.</summary>
    internal bool IsArmed => _deadline is not null || _memory is not null;

    /// <summary>Opens one turn. Dispose it in a <c>finally</c>; nesting is allowed and documented above.</summary>
    internal TurnScope BeginTurn()
    {
        if (!IsArmed)
        {
            return default;
        }

        var depth = ++_depth;

        if (depth == 1)
        {
            _serial++;
        }

        // Deliberately no cancellation token: the engine already carries a CancellationConstraint over the
        // page's own token (BrowserEngineFactory registers it), and handing the same token here as well
        // would make a closing page's pending work fail with a bare OperationCanceledException from
        // whichever constraint was checked first instead of the ExecutionCanceledException it fails with
        // today.
        ArmDeadline();

        if (depth == 1)
        {
            _memory?.Begin();
        }

        return new TurnScope(this);
    }

    private void EndTurn()
    {
        var depth = --_depth;

        if (depth > 0)
        {
            // Back to the enclosing turn, which gets a full budget rather than what is left of its own: the
            // time the nested turn spent was charged to a bound of its own, and a document is not failed for
            // containing more than one script.
            ArmDeadline();
            return;
        }

        _depth = 0;
        _deadline?.End();
        _memory?.End();
    }

    /// <summary>Arms the turn's deadline, noting first when it was armed so a stop can be attributed to it.</summary>
    private void ArmDeadline()
    {
        if (_deadline is null)
        {
            return;
        }

        Volatile.Write(ref _deadlineArmedAt, Stopwatch.GetTimestamp());
        _deadline.Begin(_turn);
    }

    /// <summary>Whether <paramref name="exception"/> is a budget this bracket armed running out.</summary>
    /// <remarks>
    /// The two the constraints throw, and nothing else: a <see cref="TimeoutException"/> is what
    /// <see cref="OperationDeadlineConstraint"/> and <c>LimitExecutionTime</c> both raise, and
    /// <see cref="MemoryLimitExceededException"/> is the allocation half.
    /// </remarks>
    internal static bool IsBudgetFailure(Exception exception)
        => exception is TimeoutException or MemoryLimitExceededException;

    /// <summary>One open turn. A <c>readonly struct</c>, so an armed turn costs no allocation.</summary>
    internal readonly struct TurnScope : IDisposable
    {
        private readonly PageBudget? _budget;

        internal TurnScope(PageBudget budget)
        {
            _budget = budget;
        }

        /// <inheritdoc />
        public void Dispose() => _budget?.EndTurn();
    }
}
