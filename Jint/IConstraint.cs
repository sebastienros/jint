namespace Jint;

/// <summary>
/// A constraint that engine can check for validate during statement execution.
/// </summary>
public abstract class Constraint
{
    /// <summary>
    /// Called before each statement to check if your requirements are met; if not - throws an exception.
    /// </summary>
    public abstract void Check();

    /// <summary>
    /// Called before script is run. Useful when you use an engine object for multiple executions.
    /// </summary>
    public abstract void Reset();
}
