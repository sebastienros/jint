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
    /// Forcefully processes the current task queues (micro and regular), this API may break and change behavior!
    /// </summary>
    public void ProcessTasks()
    {
        _engine.RunAvailableContinuations();
    }
}
