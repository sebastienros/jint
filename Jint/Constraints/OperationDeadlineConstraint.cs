using System.Diagnostics;
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
/// usable, but it also means <see cref="ConstraintsOptionsExtensions.TimeoutInterval"/> arms a
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
/// var engine = new Engine(options => options.Constraint(deadline));
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
/// <see cref="OptionsExtensions.Constraint(Options, Constraint)"/> and keep the reference, or — when one
/// <see cref="Options"/> is shared by several engines — register a factory
/// (<see cref="OptionsExtensions.Constraint(Options, System.Func{Constraint})"/>) and reach each engine's
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
/// The deadline is compared inline against <see cref="Stopwatch.GetTimestamp"/> rather than observed
/// through a timer, for the same reason <c>TimeConstraint</c> is: a timer only makes elapsed time visible
/// once its callback has been scheduled, so detection would be bounded by the thread pool rather than by
/// the budget.
/// </remarks>
public sealed class OperationDeadlineConstraint : Constraint
{
    // Stopwatch timestamp the current operation must not pass; 0 means "no operation is in flight", in
    // which case Check never fails. Mirrors TimeConstraint's not-started sentinel.
    private long _deadline;

    private CancellationToken _cancellationToken;

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
    /// How long the whole operation may run. A budget that has already elapsed by the time it is handed
    /// over (<see cref="TimeSpan.Zero"/> or negative, which is what a host computing "time left" produces
    /// when there is none) arms a deadline that has passed, so the next check fails. Very large budgets are
    /// clamped rather than allowed to overflow the timestamp arithmetic.
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
        var deadline = Stopwatch.GetTimestamp() + ToStopwatchTicks(budget);

        // 0 is the not-armed sentinel, so never store it as a real deadline
        _deadline = deadline == 0 ? 1 : deadline;
        _cancellationToken = cancellationToken;
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
        _deadline = 0;
        _cancellationToken = default;
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
    /// <see cref="ConstraintsOptionsExtensions.TimeoutInterval"/> throws, so a host that already handles a
    /// timeout does not need a second catch clause.
    /// </exception>
    public override void Check()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Throw.OperationCanceledException(_cancellationToken);
        }

        var deadline = _deadline;
        if (deadline != 0 && Stopwatch.GetTimestamp() >= deadline)
        {
            Throw.TimeoutException("The operation's time budget elapsed.");
        }
    }

    /// <summary>
    /// Deliberately does nothing: surviving the engine's per-entry reset is the entire point of this
    /// constraint.
    /// </summary>
    /// <remarks>
    /// The engine invites every constraint to rewind itself at the start and end of each top-level entry,
    /// which is what refunds the built-in timeout's deadline to every host call in a loop. Here the window
    /// is the host's to define, and it defines it with <see cref="Begin"/> and <see cref="End"/>, so the
    /// invitation is declined and the budget keeps running across as many entries as the operation makes.
    /// </remarks>
    public override void Reset()
    {
    }

    /// <summary>
    /// Converts a budget to <see cref="Stopwatch"/> ticks, clamped so that adding it to a timestamp cannot
    /// overflow. Without the clamp a large budget wraps <see cref="long"/> and lands the deadline in the
    /// <em>past</em>, failing the operation immediately — the opposite of what was asked for.
    /// </summary>
    private static long ToStopwatchTicks(TimeSpan budget)
    {
        if (budget <= TimeSpan.Zero)
        {
            // no time left: the deadline is now, and the next check fails
            return 0;
        }

        var ticks = budget.Ticks * ((double) Stopwatch.Frequency / TimeSpan.TicksPerSecond);
        return ticks >= long.MaxValue / 2.0 ? long.MaxValue / 2 : (long) ticks;
    }
}
