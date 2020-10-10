#nullable enable

using Esprima;
using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.CallStack
{
    internal readonly struct CallStackElement
    {
        public CallStackElement(
            FunctionInstance function,
            JintExpression? expression,
            ExecutionContext callingExecutionContext)
        {
            Function = function;
            Expression = expression;
            CallingExecutionContext = callingExecutionContext;
        }

        public readonly FunctionInstance Function;
        public readonly JintExpression? Expression;
        public readonly ExecutionContext CallingExecutionContext;

        public Location Location
        {
            get
            {
                var expressionLocation = Expression?._expression.Location;
                if (expressionLocation != null && expressionLocation.Value != default)
                {
                    return expressionLocation.Value;
                }

                return ((Node?) Function._functionDefinition?.Function)?.Location ?? default;
            }
        }

        public NodeList<Expression>? Arguments =>
            Function._functionDefinition?.Function.Params;

        public override string ToString()
        {
            var name = TypeConverter.ToString(Function.Get(CommonProperties.Name));

            if (string.IsNullOrWhiteSpace(name))
            {
                if (Expression is not null)
                {
                    name = JintExpression.ToString(Expression._expression);
                }
            }

            return name ?? "(anonymous)";
        }
    }
}
