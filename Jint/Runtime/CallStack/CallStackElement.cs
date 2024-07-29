using Jint.Native.Function;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.CallStack;

internal readonly struct CallStackElement : IEquatable<CallStackElement>
{
    public CallStackElement(
        Function function,
        JintExpression? expression,
        in CallStackExecutionContext callingExecutionContext)
    {
        Function = function;
        Expression = expression;
        CallingExecutionContext = callingExecutionContext;
    }

    public readonly Function Function;
    public readonly JintExpression? Expression;
    public readonly CallStackExecutionContext CallingExecutionContext;

    public ref readonly SourceLocation Location
    {
        get
        {
            ref readonly var expressionLocation = ref (Expression is not null ? ref Expression._expression.LocationRef : ref AstExtensions.DefaultLocation);
            if (expressionLocation != default)
            {
                return ref expressionLocation;
            }

            var function = (Node?) Function._functionDefinition?.Function;
            return ref (function is not null ? ref function.LocationRef : ref AstExtensions.DefaultLocation);
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
