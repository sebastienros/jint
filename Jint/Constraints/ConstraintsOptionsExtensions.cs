using System.Threading;
using Jint.Constraints;

// ReSharper disable once CheckNamespace
namespace Jint;

/// <summary>
/// Registers the built-in execution constraints.
/// <para>
/// <b>A limit that cannot be reached is not a limit.</b> That one rule governs every built-in bound in
/// Jint, and each kind of bound obeys it in the way its own kind can: a <i>constraint</i> that could never
/// fail is not registered, and a <i>value</i> setting on <see cref="Options.ConstraintOptions"/> that could
/// never be reached does not arm its check. So the two spellings of "no limit" — the explicit sentinel
/// (<c>MaxRecursionDepth = -1</c>, <c>MaxArraySize = uint.MaxValue</c>) and the saturated value
/// (<see cref="int.MaxValue"/>, <see cref="long.MaxValue"/>, <see cref="TimeSpan.MaxValue"/>) — produce one
/// and the same engine.
/// </para>
/// <para>
/// Each method here additionally <b>replaces</b> the constraint of its own kind, so calling one twice leaves
/// a single constraint registered, and a value that cannot express a limit removes any earlier registration
/// of that kind rather than adding one. Not registering it is the point: such a constraint could never fail,
/// so registering it would only add per-check work to every evaluation — and an exact constraint can
/// additionally disable the interpreter's tight-loop lane, which costs every loop in the program.
/// (<see cref="MemoryLimitConstraint"/> always does; a lone <see cref="MaxStatementsConstraint"/> is charged
/// inline and keeps the lane, but disarms it as soon as a second exact constraint joins it.)
/// </para>
/// <para>
/// The consequence is worth stating plainly, because it is easy to configure by accident: spelling
/// "effectively unlimited" as a saturated value produces exactly the same engine as never calling
/// the method, not an engine carrying a very large limit. A host that sets one believing it has a
/// limit has none, and a comparison of "with a limit" against "without" written that way compares
/// an engine against itself. What tells a host it did that is
/// <see cref="OptionsSecurityExtensions.ValidateSecurityConfiguration(Options, SecurityConfigurationPolicy)"/>,
/// which reports an unlimited bound and a saturated one under their own diagnostic codes.
/// </para>
/// </summary>
/// <remarks>
/// Every method here registers a <i>factory</i> rather than a constraint instance, so each engine
/// built from the options gets its own constraint and its own per-execution state (statement
/// counter, deadline). That is what keeps a single <see cref="Options"/> instance safe to reuse for
/// many engines, including engines running concurrently.
/// </remarks>
public static class ConstraintsOptionsExtensions
{
    /// <summary>
    /// Limits the allowed statement count that can be run as part of the program, replacing any
    /// limit set by an earlier call.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <param name="maxStatements">
    /// The statement budget. Only <c>1</c> to <see cref="int.MaxValue"/> - 1 registers a constraint;
    /// every other value removes it and leaves the statement count unlimited. A non-positive budget is
    /// removed because the constraint treats it as "no limit" internally, and <see cref="int.MaxValue"/>
    /// because the constraint counts statements in an <see cref="int"/> and so can never reach that many.
    /// Through 4.16.x this parameter defaulted to <c>0</c>, so <c>MaxStatements()</c> spelled "no limit";
    /// it has no default now, because that is not what it looked like it meant.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options LimitStatements(this Options options, int maxStatements)
    {
        options.RemoveConstraints(x => x is MaxStatementsConstraint);
        options.Constraints.RequestedMaxStatements = maxStatements;

        if (maxStatements > 0 && maxStatements < int.MaxValue)
        {
            options.AddConstraint(() => new MaxStatementsConstraint(maxStatements));
        }
        return options;
    }

    /// <summary>
    /// Sets a managed-allocation budget in bytes, replacing any limit set by an earlier call.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <param name="memoryLimit">
    /// The allowed managed allocation in bytes for one top-level engine operation, including asynchronous
    /// continuations and synchronous host callbacks it triggers. Only <c>1</c> to
    /// <see cref="long.MaxValue"/> - 1 registers a constraint; every other value removes it and leaves
    /// allocation unlimited. A non-positive limit is removed because the constraint treats it as "no limit"
    /// internally, and <see cref="long.MaxValue"/> because the measured allocation is itself a
    /// <see cref="long"/> and so can never exceed it. See <see cref="MemoryLimitConstraint"/> for the exact
    /// accounting boundary and its multi-entry <c>Begin</c>/<c>End</c> mode.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options LimitMemory(this Options options, long memoryLimit)
    {
        options.RemoveConstraints(x => x is MemoryLimitConstraint);
        options.Constraints.RequestedMemoryLimit = memoryLimit;

        if (memoryLimit > 0 && memoryLimit < long.MaxValue)
        {
            options.AddConstraint(() => new MemoryLimitConstraint(memoryLimit));
        }
        return options;
    }

    /// <summary>
    /// Sets constraint based on fixed time interval, replacing any timeout set by an earlier call.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <param name="timeoutInterval">
    /// The allowed execution time. Only an interval strictly between <see cref="TimeSpan.Zero"/> and
    /// <see cref="TimeSpan.MaxValue"/> registers a constraint; every other value removes it and
    /// leaves execution untimed. A non-positive interval is removed because it expresses no waiting
    /// time, and <see cref="TimeSpan.MaxValue"/> because a deadline that far out can never be
    /// reached.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options LimitExecutionTime(this Options options, TimeSpan timeoutInterval)
    {
        options.RemoveConstraints(x => x is TimeConstraint);
        options.Constraints.RequestedTimeoutInterval = timeoutInterval;

        if (timeoutInterval > TimeSpan.Zero && timeoutInterval < TimeSpan.MaxValue)
        {
            // The interval and nothing else. The clock is bound by the Engine constructor from the options
            // that engine is actually built from, so configuration order still does not matter — a
            // TimeProvider assigned after this call reaches the constraint exactly as it did when this
            // closure captured the group. What changes is the case a capture answered wrongly: a factory
            // replayed onto a DIFFERENT Options instance, which is what a worker is
            // (sebastienros/jint#3481). Nothing here is conditionally compiled any more, because there is
            // no longer a .NET 8 type in the expression.
            options.AddConstraint(() => new TimeConstraint(timeoutInterval));
        }
        return options;
    }

    /// <summary>
    /// Sets cancellation token to be observed, replacing any token set by an earlier call.
    /// NOTE that this can be unreliable/imprecise on full framework due to timer logic.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <param name="cancellationToken">
    /// The token to observe. The default token removes the constraint and leaves execution
    /// unobserved, because a token that cannot be cancelled has nothing to report.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options ObserveCancellation(this Options options, CancellationToken cancellationToken)
    {
        options.RemoveConstraints(x => x is CancellationConstraint);
        options.Constraints.CancellationConstraintRequested = cancellationToken != default;

        if (cancellationToken != default)
        {
            options.AddConstraint(() => new CancellationConstraint(cancellationToken));
        }
        return options;
    }
}
