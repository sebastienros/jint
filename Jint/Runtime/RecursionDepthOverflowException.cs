using Jint.Runtime.CallStack;

namespace Jint.Runtime;

public sealed class RecursionDepthOverflowException : JintException
{
    public string CallChain { get; }

    public string CallExpressionReference { get; }

    internal RecursionDepthOverflowException(JintCallStack currentStack, string currentExpressionReference)
        : base("The recursion is forbidden by script host.")
    {
        CallExpressionReference = currentExpressionReference;

        CallChain = currentStack.ToString();
    }
}