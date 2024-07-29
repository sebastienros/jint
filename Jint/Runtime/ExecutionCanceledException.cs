namespace Jint.Runtime;

public sealed class ExecutionCanceledException : JintException
{
    public ExecutionCanceledException() : base("The script execution was canceled.")
    {
    }
}