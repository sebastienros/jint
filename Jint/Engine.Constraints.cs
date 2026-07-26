using Jint.Constraints;
using Jint.Runtime;

namespace Jint;

public partial class Engine
{
    public ConstraintOperations Constraints { get; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct ConstraintPartition(Constraint[] Exact, Constraint[] Amortized);

    /// <summary>
    /// Materializes the constraint set for this engine.
    /// </summary>
    /// <remarks>
    /// Constraints hold per-execution state — a statement counter, a deadline, an observed token — and
    /// <see cref="ResetConstraints"/> rewinds that state at the start of every execution. Sharing one
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
    /// Splits the registered constraints by required check frequency. The built-in time and
    /// cancellation constraints only observe external state that a check reads without consuming
    /// (a timer, a token), so checking them every N statements is semantically equivalent to
    /// checking per statement — only the detection latency is bounded instead of immediate (the
    /// same reasoning bulk built-ins apply via <see cref="ConstraintCheckInterval"/>). Everything
    /// else stays exact:
    /// <list type="bullet">
    /// <item><see cref="MaxStatementsConstraint"/> counts statements, so its call frequency IS its
    /// semantics.</item>
    /// <item><see cref="MemoryLimitConstraint"/> reads an allocation counter, but unlike a clock
    /// that only advances, allocation between checks is irreversible and unbounded per statement
    /// (a single iteration can allocate arbitrarily much, e.g. exponential string growth), so
    /// amortizing it would let the process overshoot the configured cap — potentially to a real
    /// OutOfMemoryException — before the next check. Keeping it exact preserves the memory bound
    /// as a hard-ish guarantee for sandboxing untrusted code (matching pre-tight-lane behavior).</item>
    /// <item>User-derived constraints may depend on being called once per statement — silently
    /// amortizing them would be a breaking behavior change.</item>
    /// </list>
    /// Interop call sites additionally re-check on return from user CLR code — see
    /// <see cref="CheckAmortizedConstraintsAtHostBoundary"/> for that mechanism's rationale.
    /// </summary>
    private static ConstraintPartition PartitionConstraints(Constraint[] constraints)
    {
        if (constraints.Length == 0)
        {
            return new ConstraintPartition([], []);
        }

        var exact = new List<Constraint>(constraints.Length);
        var amortized = new List<Constraint>(constraints.Length);
        foreach (var constraint in constraints)
        {
            // Both types are sealed, so the check cannot match a user-derived subclass.
            if (constraint is TimeConstraint or CancellationConstraint)
            {
                amortized.Add(constraint);
            }
            else
            {
                exact.Add(constraint);
            }
        }

        return new ConstraintPartition(exact.ToArray(), amortized.ToArray());
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
            foreach (var constraint in _engine._constraints)
            {
                constraint.Check();
            }
        }

        /// <summary>
        /// Return the first constraint that matches the predicate.
        /// </summary>
        public T? Find<T>() where T : Constraint
        {
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
            foreach (var constraint in _engine._constraints)
            {
                constraint.Reset();
            }
        }
    }
}
