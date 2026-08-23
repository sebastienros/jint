using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Runtime;

namespace Jint.Constraints;

/// <summary>
/// A time budget — and, optionally, a cancellation token — that covers a whole host operation instead of a
/// single entry into the engine.
/// <para>
/// Every public entry that runs script (<c>Execute</c>, <c>Evaluate</c>, <c>Invoke</c>, <c>Engine.Call</c>,
/// the <c>JsValue.Call</c> extension helpers) is a complete top-level run: the engine resets every
/// registered constraint before the entry and again on the way out. That is what makes a reused engine
/// usable, but it also means <see cref="ConstraintsOptionsExtensions.LimitExecutionTime"/> arms a
/// <em>fresh</em> deadline for each entry. An operation built from several entries — import a module, call
/// the component it exported, then invoke a handler once per row — therefore has no wall-clock bound at
/// all, however small the configured interval: <c>foreach (var row in rows) handler.Call(row);</c> gives
/// every row the whole interval to itself. This constraint is the in-box answer for that shape. The host
/// brackets the operation, and nothing the engine does in between rewinds the budget.
/// </para>
/// <example>
/// <code>
/// // one instance per engine, owned by the host
/// var deadline = new OperationDeadlineConstraint();
/// var engine = new Engine(options => options.AddConstraint(deadline));
///
/// deadline.Begin(TimeSpan.FromSeconds(2), cancellationToken);
/// try
/// {
///     var component = engine.Modules.Import("./app.js");
///     foreach (var row in rows)
///     {
///         engine.Invoke("render", row);
///     }
/// }
/// finally
/// {
///     deadline.End();
/// }
/// </code>
/// </example>
/// <para>
/// The instance is host-owned on purpose: <c>Begin</c> and <c>End</c> are not something the engine can
/// infer, since only the host knows where its operation starts and stops. Register it with
/// <see cref="OptionsExtensions.AddConstraint(Options, Constraint)"/> and keep the reference, or — when one
/// <see cref="Options"/> is shared by several engines — register a factory
/// (<see cref="OptionsExtensions.AddConstraint(Options, System.Func{Constraint})"/>) and reach each engine's
/// own instance through <c>engine.Constraints.Find&lt;OperationDeadlineConstraint&gt;()</c>.
/// </para>
/// <para>
/// <b>What it does not do.</b> It is not a cross-engine budget: one instance carries one deadline and one
/// token, and an engine is single-threaded by contract, so the instance expects the same thread discipline
/// as the engine it is registered with — <c>Begin</c>, the entries it covers, and <c>End</c> all happen on
/// the thread driving that engine. It bounds nothing while disarmed, which is deliberate and close to
/// free: outside a <see cref="Begin"/>/<see cref="End"/> bracket a check reads two fields and takes no
/// timestamp, so an engine that is pooled between operations pays nothing for carrying it.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The deadline is compared inline against <see cref="Stopwatch.GetTimestamp"/> rather than observed
/// through a timer, for the same reason <c>TimeConstraint</c> is: a timer only makes elapsed time visible
/// once its callback has been scheduled, so detection would be bounded by the thread pool rather than by
/// the budget.
/// </para>
/// <para>
/// On <c>net8.0</c> and later the host may hand the instance a <c>TimeProvider</c> instead, which then
/// answers every timestamp the budget is measured against. The constructor that takes one is the whole
/// seam: the instance is the host's to create, so the clock is the host's to supply, and nothing about the
/// engine-registered <c>TimeoutInterval</c> constraint is involved. See <c>ConstraintClock</c> for why
/// <c>TimeProvider.System</c> costs nothing.
/// </para>
/// </remarks>
public sealed class OperationDeadlineConstraint : Constraint
{
    private static readonly object _endingScope = new();

    // No operation is in flight, so the deadline half of Check never fails. Mirrors TimeConstraint's
    // not-started sentinel, and is also what a non-positive budget arms.
    private const long Disarmed = 0;

#if NET8_0_OR_GREATER
    // Null unless the host named a clock, which is what keeps Check() and Begin() on the direct
    // Stopwatch read they have always used.
    private readonly TimeProvider? _timeProvider;

    // Stopwatch.Frequency unless _timeProvider is non-null; resolved once so arming a budget stays
    // arithmetic on a field.
    private readonly long _frequency;
#endif

    // Timestamp the current operation must not pass, on whichever clock this constraint reads, or Disarmed.
    private long _deadline;

    private CancellationToken _cancellationToken;
    private object? _scopeOwner;

#if NET8_0_OR_GREATER
    /// <summary>
    /// Creates a constraint measuring its budget against the system's monotonic clock
    /// (<see cref="Stopwatch.GetTimestamp"/>).
    /// </summary>
    public OperationDeadlineConstraint() : this(timeProvider: null)
    {
    }

    /// <summary>
    /// Creates a constraint measuring its budget against <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">
    /// The clock the budget is measured against. <see langword="null"/> and
    /// <see cref="TimeProvider.System"/> both mean the system's monotonic clock, and cost exactly what
    /// they did before this overload existed. Only <see cref="TimeProvider.GetTimestamp"/> and
    /// <see cref="TimeProvider.TimestampFrequency"/> are ever called — never
    /// <see cref="TimeProvider.CreateTimer"/>, because this constraint schedules nothing and only ever
    /// reads the clock from the thread driving the engine.
    /// </param>
    /// <remarks>
    /// The point of the overload is that a host can test what its own budget does without waiting for it:
    /// a fake clock makes "the operation ran out of time between these two entries" an exact statement
    /// rather than a race against a loaded machine.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="timeProvider"/> reports a non-positive <see cref="TimeProvider.TimestampFrequency"/>.
    /// </exception>
    public OperationDeadlineConstraint(TimeProvider? timeProvider)
    {
        _timeProvider = ConstraintClock.Resolve(timeProvider);
        _frequency = ConstraintClock.FrequencyOf(_timeProvider);
    }
#endif

    /// <summary>
    /// A deadline and a cancellation token are both external state that <see cref="Check"/> only reads and
    /// never consumes — a clock only advances, a token never un-cancels — so checking less often bounds how
    /// late the operation is failed rather than changing what is measured. See
    /// <see cref="Constraint.IsAmortizable"/> for the full obligation: this constraint counts nothing of its
    /// own and holds no budget over a quantity that can grow unboundedly between two checks, so it meets
    /// both halves of it and the interpreter's tight-loop lanes stay armed while it is registered.
    /// </summary>
    public override bool IsAmortizable => true;

    /// <summary>
    /// Arms the constraint for one host operation. Everything the engine runs from now until
    /// <see cref="End"/> — however many top-level entries that is — shares this one budget.
    /// </summary>
    /// <param name="budget">
    /// How long the whole operation may run. A non-positive budget - <see cref="TimeSpan.Zero"/>, a negative
    /// value, or <see cref="Timeout.InfiniteTimeSpan"/> - asks for no time limit at all, the same thing it
    /// means to <c>TimeoutInterval</c> and to the rest of .NET; the cancellation token is still observed, so
    /// that is the cancellation-only shape. A host that has computed "time left" and found none should decline
    /// to enter the engine rather than arm a zero budget. Very large budgets are clamped rather than allowed
    /// to overflow the timestamp arithmetic.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional token observed for the same window. The default token observes nothing.
    /// </param>
    /// <remarks>
    /// Calling <see cref="Begin"/> again while armed simply re-arms: the new budget is measured from that
    /// moment, and the previously observed token is replaced.
    /// </remarks>
    public void Begin(TimeSpan budget, CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;

        if (budget <= TimeSpan.Zero)
        {
            // A non-positive budget is how the rest of .NET, and this engine's own TimeoutInterval,
            // spell "no time limit" - Timeout.InfiniteTimeSpan is -1ms. Arming it arithmetically
            // would put the deadline at or before the operation's own start and end every operation
            // the moment it began, which is the opposite of what the caller asked for. The token is
            // still observed, so Begin(Timeout.InfiniteTimeSpan, token) is the cancellation-only shape.
            _deadline = Disarmed;
            return;
        }

        var deadline = GetTimestamp() + ToTimestampTicks(budget);

        // Disarmed is the not-armed sentinel, so never store it as a real deadline
        _deadline = deadline == Disarmed ? Disarmed + 1 : deadline;
    }

    /// <summary>
    /// Disarms the constraint, ending the window <see cref="Begin"/> opened. Both the deadline and the
    /// observed token are dropped, so a pooled engine carrying this constraint between operations is
    /// unbounded again — and stops holding a reference to the finished operation's token.
    /// </summary>
    /// <remarks>
    /// Safe to call when not armed, and safe to call twice; it is meant to live in a <c>finally</c>.
    /// </remarks>
    public void End()
    {
        _deadline = Disarmed;
        _cancellationToken = default;
    }

    internal bool TryBeginScope(object owner, TimeSpan budget, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _scopeOwner, owner, null) is not null)
        {
            return false;
        }

        Begin(budget, cancellationToken);
        return true;
    }

    internal void EndScope(object owner)
    {
        if (ReferenceEquals(Interlocked.CompareExchange(ref _scopeOwner, _endingScope, owner), owner))
        {
            End();
            Volatile.Write(ref _scopeOwner, null);
        }
    }

    /// <summary>
    /// Fails the execution when the operation's token has been cancelled, or its budget has elapsed.
    /// </summary>
    /// <exception cref="System.OperationCanceledException">
    /// The token handed to <see cref="Begin"/> has been cancelled. Deliberately a real
    /// <see cref="System.OperationCanceledException"/> carrying the token, and not Jint's own
    /// <c>ExecutionCanceledException</c>, which derives from <c>JintException</c> and is <em>not</em> an
    /// <see cref="System.OperationCanceledException"/>: a host whose outer layer is written as
    /// <c>catch (Exception e) when (e is not OperationCanceledException)</c> — the standard shape for
    /// "log every failure except the ones I asked for" — needs cancellation it requested itself to be
    /// distinguishable from a script failure, and this is the exception type the rest of its stack already
    /// filters on.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// The budget handed to <see cref="Begin"/> has elapsed. The same exception type the built-in
    /// <see cref="ConstraintsOptionsExtensions.LimitExecutionTime"/> throws, so a host that already handles a
    /// timeout does not need a second catch clause.
    /// </exception>
    public override void Check()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Throw.OperationCanceledException(_cancellationToken);
        }

        var deadline = _deadline;
        if (deadline != 0 && GetTimestamp() >= deadline)
        {
            Throw.TimeoutException("The operation's time budget elapsed.");
        }
    }

    /// <summary>
    /// Deliberately does nothing: surviving the engine's per-entry reset is the entire point of this
    /// constraint.
    /// </summary>
    /// <remarks>
    /// The engine invites ordinary resettable constraints to rewind at the start and end of each top-level
    /// entry, which is what refunds the built-in timeout's deadline to every host call in a loop. Here the window
    /// is the host's to define, and it defines it with <see cref="Begin"/> and <see cref="End"/>, so the
    /// invitation is declined and the budget keeps running across as many entries as the operation makes.
    /// </remarks>
    public override void Reset()
    {
    }

#if NET8_0_OR_GREATER
    // Converts a budget to ticks on this constraint's own clock, clamped so that adding it to a timestamp
    // cannot overflow. See ConstraintClock.ToTimestampTicks.
    private long ToTimestampTicks(TimeSpan budget) => ConstraintClock.ToTimestampTicks(budget, _frequency);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetTimestamp()
    {
        var provider = _timeProvider;
        return provider is null ? Stopwatch.GetTimestamp() : provider.GetTimestamp();
    }
#else
    // Converts a budget to ticks on this constraint's own clock, clamped so that adding it to a timestamp
    // cannot overflow. See ConstraintClock.ToTimestampTicks.
    private static long ToTimestampTicks(TimeSpan budget)
        => ConstraintClock.ToTimestampTicks(budget, Stopwatch.Frequency);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetTimestamp() => Stopwatch.GetTimestamp();
#endif
}
