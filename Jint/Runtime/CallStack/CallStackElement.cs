using Esprima;
using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.CallStack
{
    internal readonly struct CallStackElement : IEquatable<CallStackElement>
    {
        public CallStackElement(
            FunctionInstance function,
            JintExpression? expression,
            in CallStackExecutionContext callingExecutionContext)
        {
            Function = function;
            Expression = expression;
            CallingExecutionContext = callingExecutionContext;
        }

        public readonly FunctionInstance Function;
        public readonly JintExpression? Expression;
        public readonly CallStackExecutionContext CallingExecutionContext;

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

        public NodeList<Node>? Arguments => Function._functionDefinition?.Function.Params;

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

        public bool Equals(CallStackElement other)
        {
            return Function.Equals(other.Function) && Equals(Expression, other.Expression);
        }

        public override bool Equals(object? obj)
        {
            return obj is CallStackElement other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Function.GetHashCode() * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);
            }
        }
    }
}
