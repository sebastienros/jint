using Jint.Constraints;
using Jint.Runtime;

namespace Jint;

public partial class Engine
{
    public ConstraintOperations Constraints { get; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct ConstraintPartition(
        Constraint[] Exact,
        Constraint[] Amortized,
        MaxStatementsConstraint? InlineStatementCounter);

    /// <summary>
    /// Materializes the constraint set for this engine.
    /// </summary>
    /// <remarks>
    /// Constraints hold per-execution state — a statement counter, a deadline, an observed token — and
    /// <see cref="ResetConstraints"/> rewinds the ordinary resettable state at the start of every execution.
    /// The memory constraint establishes its thread-local segments through the entry lifecycle instead, so a
    /// completed operation remains observable through <see cref="MemoryLimitConstraint.AllocatedBytes"/>. Sharing one
    /// <see cref="Options"/> instance across engines is a supported (and recommended) pattern, so the
    /// engine cannot simply take a reference to whatever the options hold: two engines would then share
    /// one budget and one engine's reset would rewind another engine's in-flight execution. Factory
    /// registrations therefore produce a fresh instance per engine here. Instances registered directly
    /// through <see cref="OptionsExtensions.Constraint(Options, Constraint)"/> are used as given — that
    /// overload is documented as single-engine-only, and nothing can be cloned out of an arbitrary
    /// user-derived <see cref="Constraint"/>.
    /// </remarks>
    private static Constraint[] BuildConstraints(Options.ConstraintOptions options)
    {
        var factories = options.ConstraintFactories;
        var instances = options.Constraints;

        if (factories.Count == 0)
        {
            return instances.Count == 0 ? [] : instances.ToArray();
        }

        var constraints = new Constraint[factories.Count + instances.Count];
        var index = 0;
        foreach (var factory in factories)
        {
            var constraint = factory();
            if (constraint is null)
            {
                Throw.InvalidOperationException("A registered constraint factory returned null.");
            }

            constraints[index++] = constraint;
        }

        foreach (var instance in instances)
        {
            constraints[index++] = instance;
        }

        return constraints;
    }

    /// <summary>
    /// Splits the registered constraints by required check frequency, which each constraint declares for
    /// itself through <see cref="Constraint.IsAmortizable"/>. An amortizable constraint only observes
    /// external state that a check reads without consuming, so checking it every N statements is
    /// semantically equivalent to checking per statement — only the detection latency is bounded instead
    /// of immediate (the same reasoning bulk built-ins apply via <see cref="ConstraintCheckInterval"/>).
    /// Everything else stays exact, which is the default and therefore what a user-derived constraint gets
    /// unless it opts in. See <see cref="Constraint.IsAmortizable"/> for when opting in is sound, and each
    /// built-in constraint for why it answers the way it does.
    /// <para>
    /// <see cref="MaxStatementsConstraint"/> additionally gets a dedicated lane: when it is the
    /// <em>only</em> exact constraint it is also reported as
    /// <see cref="ConstraintPartition.InlineStatementCounter"/>, so the interpreter can charge it directly
    /// (a devirtualized call on a sealed type) at exactly the same points <see cref="RunPerStatementChecks"/>
    /// would, and the tight-loop lanes — which cannot run the exact list — stay armed.
    /// </para>
    /// <para>
    /// Interop call sites additionally re-check on return from user CLR code — see
    /// <see cref="CheckAmortizedConstraintsAtHostBoundary"/> for that mechanism's rationale.
    /// </para>
    /// </summary>
    private static ConstraintPartition PartitionConstraints(Constraint[] constraints)
    {
        if (constraints.Length == 0)
        {
            return new ConstraintPartition([], [], null);
        }

        var exact = new List<Constraint>(constraints.Length);
        var amortized = new List<Constraint>(constraints.Length);
        foreach (var constraint in constraints)
        {
            if (constraint.IsAmortizable)
            {
                amortized.Add(constraint);
            }
            else
            {
                exact.Add(constraint);
            }
        }

        // A lone statement counter is the one exact constraint the interpreter can charge itself, so
        // report it separately and let the per-statement path skip the exact-list walk entirely.
        // MaxStatementsConstraint is sealed, hence the exact type match.
        var inlineStatementCounter = exact.Count == 1 ? exact[0] as MaxStatementsConstraint : null;

        return new ConstraintPartition(exact.ToArray(), amortized.ToArray(), inlineStatementCounter);
    }

    public class ConstraintOperations
    {
        private readonly Engine _engine;

        internal ConstraintOperations(Engine engine)
        {
            _engine = engine;
        }

        /// <summary>
        /// Checks engine's active constraints. Propagates exceptions from constraints.
        /// </summary>
        public void Check()
        {
            using var ownership = _engine.EnterHostCall();
            foreach (var constraint in _engine._constraints)
            {
                _engine.CheckConstraint(constraint);
            }
        }

        /// <summary>
        /// Return the first constraint that matches the predicate.
        /// </summary>
        public T? Find<T>() where T : Constraint
        {
            using var ownership = _engine.EnterHostCall();
            foreach (var constraint in _engine._constraints)
            {
                if (constraint.GetType() == typeof(T))
                {
                    return (T) constraint;
                }
            }

            return null;
        }

        /// <summary>
        /// Resets all execution constraints back to their initial state.
        /// </summary>
        public void Reset()
        {
            using var ownership = _engine.EnterHostCall();
            foreach (var constraint in _engine._constraints)
            {
                constraint.Reset();
            }
        }
    }
}
