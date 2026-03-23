namespace Jint.Runtime;

/// <summary>
/// Workaround for situation where engine is not easily accessible.
/// </summary>
internal sealed class SyntaxErrorException : JintException
{
    public SyntaxErrorException(string? message) : base(message)
    {
    }
}
