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
}
