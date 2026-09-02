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

    /// <summary>
    /// What the frame is called in a stack trace and in the debugger's call stack: the function's own
    /// <c>name</c>, the call-site expression when it has none, and <c>(anonymous)</c> when neither says
    /// anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name is read as a descriptor rather than through <c>[[Get]]</c>. <c>name</c> is configurable on
    /// every function, so a script may replace it with an accessor — and naming a frame happens while an
    /// error's stack trace is being built, where running script would let that accessor hijack the error.
    /// A name that cannot be read without running something is treated as absent.
    /// </para>
    /// <para>
    /// A frame the engine was <i>entered</i> at — a host <c>Invoke</c>, a timer callback — has no call-site
    /// expression, because nothing in script called it. Falling out of both sources therefore has to answer
    /// <c>(anonymous)</c>, the word an immediately invoked function expression already got, rather than the
    /// empty string, which renders as a frame with no name at all.
    /// </para>
    /// </remarks>
    public override string ToString()
    {
        var name = Function.GetOwnFunctionNameForDisplay();

        if (string.IsNullOrWhiteSpace(name) && Expression is not null)
        {
            name = JintExpression.ToString(Expression._expression);
        }

        return string.IsNullOrWhiteSpace(name) ? "(anonymous)" : name!;
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
