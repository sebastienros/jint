namespace Jint.Runtime;

public sealed class MemoryLimitExceededException : JintException
{
    public MemoryLimitExceededException(string message) : base(message)
    {
    }
}