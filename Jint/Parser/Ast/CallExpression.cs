using Jint.Native;
using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class CallExpression : Expression
    {
        public Expression Callee;
        public IList<Expression> Arguments;

        public bool Cached;
        public bool CanBeCached = true;
        public JsValue[] CachedArguments;
    }
}