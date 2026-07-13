using Jint.Constraints;

namespace Jint;

public partial class Engine
{
    public ConstraintOperations Constraints { get; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct ConstraintPartition(Constraint[] Exact, Constraint[] Amortized);

    /// <summary>
    /// Splits the registered constraints by required check frequency. The built-in time,
    /// cancellation and memory-limit constraints only observe external state (a timer, a token,
    /// an allocation counter), so checking them every N statements is semantically equivalent to
    /// checking per statement — only the detection latency is bounded instead of immediate (the
    /// same reasoning bulk built-ins apply via <see cref="ConstraintCheckInterval"/>). Everything
    /// else stays exact: <see cref="MaxStatementsConstraint"/> counts statements, so its call
    /// frequency IS its semantics, and user-derived constraints may depend on being called once
    /// per statement — silently amortizing them would be a breaking behavior change.
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
            // All three types are sealed, so the checks cannot match a user-derived subclass.
            if (constraint is TimeConstraint or CancellationConstraint or MemoryLimitConstraint)
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
