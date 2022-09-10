using Jint.Runtime.Environments;

namespace Jint;

/// <summary>
/// Contains helpers and compatibility shims.
/// </summary>
public partial class Engine
{
    /// <summary>
    /// Creates a new declarative environment that has current lexical environment as outer scope.
    /// </summary>
    public EnvironmentRecord CreateNewDeclarativeEnvironment()
    {
        return JintEnvironment.NewDeclarativeEnvironment(this, ExecutionContext.LexicalEnvironment);
    }

    /// <summary>
    /// Return the first constraint that matches the predicate.
    /// </summary>
    public T? FindConstraint<T>() where T : Constraint
    {
        foreach (var constraint in _constraints)
        {
            if (constraint.GetType() == typeof(T))
            {
                return (T) constraint;
            }
        }

        return null;
    }
}
