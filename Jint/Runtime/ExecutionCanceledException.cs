namespace Jint.Runtime
{
    public class ExecutionCanceledException : JintException
    {
        public ExecutionCanceledException() : base("The script execution was canceled.")
        {
        }
    }
}