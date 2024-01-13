namespace Jint;

public partial class Engine
{
    public ConstraintOperations Constraints { get; }

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
