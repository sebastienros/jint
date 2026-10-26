namespace Jint;

public sealed class ScriptPreparationException : JintException
{
    public ScriptPreparationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
