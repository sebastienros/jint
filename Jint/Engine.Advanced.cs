using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint;

public partial class Engine
{
    public AdvancedOperations Advanced { get; }
}

public class AdvancedOperations
{
    private readonly Engine _engine;

    public AdvancedOperations(Engine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Gets current stack trace that is active in engine.
    /// </summary>
    public string StackTrace
    {
        get
        {
            var lastSyntaxElement = _engine._lastSyntaxElement;
            if (lastSyntaxElement is null)
            {
                return string.Empty;
            }

            return _engine.CallStack.BuildCallStackString(lastSyntaxElement.Location);
        }
    }

    /// <summary>
    /// Forcefully processes the current task queues (micro and regular), this API may break and change behavior!
    /// </summary>
    public void ProcessTasks()
    {
        _engine.RunAvailableContinuations();
    }

    /// <summary>
    /// Creates a new declarative environment that has current lexical environment as outer scope.
    /// </summary>
    public Environment CreateDeclarativeEnvironment()
    {
        return JintEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
    }

    /// <summary>
    /// Return the first constraint that matches the predicate.
    /// </summary>
    public T? FindConstraint<T>() where T : Constraint
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
}
