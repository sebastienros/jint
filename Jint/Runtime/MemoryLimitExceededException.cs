namespace Jint.Runtime;

public sealed class MemoryLimitExceededException : JintException
{
    public MemoryLimitExceededException(string message) : base(message)
    {
    }

    internal MemoryLimitExceededException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}