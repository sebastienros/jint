using System.Threading;
using Jint.Constraints;

// ReSharper disable once CheckNamespace
namespace Jint;

/// <summary>
/// Registers the built-in execution constraints.
/// <para>
/// Each method here <b>replaces</b> the constraint of its own kind, so calling one twice leaves a
/// single constraint registered, and each treats a value that cannot express a real limit as
/// "remove the constraint" rather than registering one. That is deliberate: such a constraint could
/// never fail, so registering it would only add per-check work to every evaluation — and an exact
/// constraint (<see cref="MaxStatementsConstraint"/>, <see cref="MemoryLimitConstraint"/>)
/// additionally disables the interpreter's tight-loop lane, which costs every loop in the program.
/// </para>
/// <para>
/// The consequence is worth stating plainly, because it is easy to configure by accident: spelling
/// "effectively unlimited" as a saturated value produces exactly the same engine as never calling
/// the method, not an engine carrying a very large limit. A host that sets one believing it has a
/// limit has none, and a comparison of "with a limit" against "without" written that way compares
/// an engine against itself.
/// </para>
/// </summary>
public static class ConstraintsOptionsExtensions
{
    /// <summary>
    /// Limits the allowed statement count that can be run as part of the program, replacing any
    /// limit set by an earlier call.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <param name="maxStatements">
    /// The statement budget. Only <c>1</c> to <see cref="int.MaxValue"/> - 1 registers a constraint;
    /// every other value removes it and leaves the statement count unlimited. A non-positive budget
    /// (including the parameter's own default) is removed because the constraint treats it as "no
    /// limit" internally, and <see cref="int.MaxValue"/> because the constraint counts statements in
    /// an <see cref="int"/> and so can never reach that many.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options MaxStatements(this Options options, int maxStatements = 0)
    {
        options.WithoutConstraint(x => x is MaxStatementsConstraint);

        if (maxStatements > 0 && maxStatements < int.MaxValue)
        {
            options.Constraint(new MaxStatementsConstraint(maxStatements));
        }
        return options;
    }

    /// <summary>
    /// Sets constraint based on memory usage in bytes, replacing any limit set by an earlier call.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <param name="memoryLimit">
    /// The allowed allocation in bytes. Only <c>1</c> to <see cref="long.MaxValue"/> - 1 registers a
    /// constraint; every other value removes it and leaves allocation unlimited. A non-positive limit
    /// is removed because the constraint treats it as "no limit" internally, and
    /// <see cref="long.MaxValue"/> because the measured allocation is itself a <see cref="long"/> and
    /// so can never exceed it.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options LimitMemory(this Options options, long memoryLimit)
    {
        options.WithoutConstraint(x => x is MemoryLimitConstraint);

        if (memoryLimit > 0 && memoryLimit < long.MaxValue)
        {
            options.Constraint(new MemoryLimitConstraint(memoryLimit));
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
    public static Options TimeoutInterval(this Options options, TimeSpan timeoutInterval)
    {
        options.WithoutConstraint(x => x is TimeConstraint);

        if (timeoutInterval > TimeSpan.Zero && timeoutInterval < TimeSpan.MaxValue)
        {
            options.Constraint(new TimeConstraint(timeoutInterval));
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
    public static Options CancellationToken(this Options options, CancellationToken cancellationToken)
    {
        options.WithoutConstraint(x => x is CancellationConstraint);

        if (cancellationToken != default)
        {
            options.Constraint(new CancellationConstraint(cancellationToken));
        }
        return options;
    }
}
