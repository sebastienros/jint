namespace Jint;

/// <summary>
/// Base class for exceptions thrown by Jint.
/// </summary>
public abstract class JintException : Exception
{
    internal JintException(string? message) : base(message)
    {
    }

    internal JintException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
