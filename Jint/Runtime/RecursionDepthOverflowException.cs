using Jint.Runtime.CallStack;

namespace Jint.Runtime
{
    public class RecursionDepthOverflowException : JintException
    {
        public string CallChain { get; private set; }

        public string CallExpressionReference { get; private set; }

        public RecursionDepthOverflowException(JintCallStack currentStack, string currentExpressionReference)
            : base("The recursion is forbidden by script host.")
        {
            CallExpressionReference = currentExpressionReference;

            CallChain = currentStack.ToString();
        }
    }
}
