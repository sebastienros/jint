using Jint.Constraints;

namespace Jint;

public partial class Engine
{
    public ConstraintOperations Constraints { get; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct ConstraintPartition(Constraint[] Exact, Constraint[] Amortized);

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
