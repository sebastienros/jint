using Jint.Native;

namespace Jint;

/// <summary>
/// https://tc39.es/ecma262/#sec-agents , still a work in progress, mostly placeholder
/// </summary>
internal sealed class Agent
{
    private readonly List<JsValue> _keptAlive = new();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-IncrementModuleAsyncEvaluationCount
    /// Per spec, this is a field of the Agent Record that tracks the relative
    /// evaluation order between pending async modules. Persists across Evaluate() calls.
    /// </summary>
    internal int ModuleAsyncEvaluationCount;

    public void AddToKeptObjects(JsValue target)
    {
        _keptAlive.Add(target);
    }

    public void ClearKeptObjects()
    {
        _keptAlive.Clear();
    }
}
