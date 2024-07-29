namespace Jint.Tests.Runtime.Domain;

public class Thrower
{
    public void ThrowArgumentNullException()
    {
        throw new ArgumentNullException();
    }

    public void ThrowExceptionWithMessage(string message)
    {
        throw new Exception(message);
    }

    public void ThrowNotSupportedException()
    {
        throw new NotSupportedException();
    }

    public void ThrowNotSupportedExceptionWithMessage(string message)
    {
        throw new NotSupportedException(message);
    }
}