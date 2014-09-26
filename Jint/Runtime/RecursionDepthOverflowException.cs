using System;
using System.Collections.Generic;
using System.Linq;

using Jint.Native;
using Jint.Parser.Ast;

namespace Jint.Runtime
{
    using Jint.Runtime.CallStack;

    public class RecursionDepthOverflowException : Exception
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
