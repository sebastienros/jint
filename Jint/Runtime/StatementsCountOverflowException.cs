namespace Jint.Runtime;

public sealed class StatementsCountOverflowException : JintException
{
    public StatementsCountOverflowException() : base("The maximum number of statements executed have been reached.")
    {
    }
}