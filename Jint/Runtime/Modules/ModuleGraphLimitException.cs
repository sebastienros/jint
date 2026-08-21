namespace Jint.Runtime.Modules;

/// <summary>
/// Thrown when a module graph exceeds one of the host-configured limits: module count, total source bytes,
/// graph depth, or resolution hops. Like other constraint exceptions (<see cref="ExecutionCanceledException"/>,
/// <see cref="TimeoutException"/>), it propagates through the module-load pipeline rather than becoming a
/// catchable import rejection, so script cannot catch-and-ignore it.
/// </summary>
public sealed class ModuleGraphLimitException : JintException
{
    public ModuleGraphLimitException(string message) : base(message)
    {
    }
}
