namespace Jint.Runtime.RegExp;

/// <summary>
/// Thrown when a regex pattern has a syntax error during custom engine compilation.
/// </summary>
internal sealed class RegExpSyntaxException : Exception
{
    public RegExpSyntaxException(string message) : base(message)
    {
    }
}
