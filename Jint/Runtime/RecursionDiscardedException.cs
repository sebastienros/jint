using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Runtime
{
    using Jint.Native;
    using Jint.Parser.Ast;
    using Jint.Runtime.References;

    public class RecursionDiscardedException : Exception 
    {
        public string CallChain { get; private set; }

        public string CallExpressionReference { get; private set; }

        public RecursionDiscardedException(Stack<Tuple<CallExpression, JsValue, string>> currentStack, Tuple<CallExpression, JsValue, string> currentExpression)
            : base("The recursion is forbidden by script host.")
        {
            CallExpressionReference = currentExpression.Item3;

            CallChain = string.Join("->", currentStack.Select(t => t.Item3).ToArray().Reverse());
        }
    }
}
