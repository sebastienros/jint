namespace Jint.Runtime
{
    /// <summary>
    /// Base class for exceptions thrown by Jint.
    /// </summary>
    public abstract class JintException : Exception
    {
        protected JintException()
        {
        }

        protected JintException(string? message) : base(message)
        {
        }

        protected JintException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
