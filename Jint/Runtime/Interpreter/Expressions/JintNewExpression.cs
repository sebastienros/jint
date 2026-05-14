namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintNewExpression : JintExpression
{
    private readonly ExpressionCache _arguments = new();
    private readonly JintExpression _calleeExpression;

    public JintNewExpression(NewExpression expression) : base(expression)
    {
        _arguments.Initialize(expression.Arguments.AsSpan());
        _calleeExpression = Build(expression.Callee);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;

        // todo: optimize by defining a common abstract class or interface
        var jsValue = _calleeExpression.GetValue(context);

        if (context.IsSuspended())
        {
            return jsValue;
        }

        var arguments = _arguments.ArgumentListEvaluation(context, this, out var rented);

        // Reset the location to the "new" keyword so that if an Error object is
        // constructed below, the stack trace will capture the correct location.
        context.LastSyntaxElement = _expression;

        if (context.IsSuspended())
        {
            // Argument list suspended mid-evaluation. ExpressionCache keeps the buffer
            // alive in suspend data and returns rented=false.
            return jsValue;
        }

        if (!jsValue.IsConstructor)
        {
            Throw.TypeError(engine.Realm, $"{_calleeExpression.SourceText} is not a constructor");
        }

        // construct the new instance using the Function's constructor method
        var instance = engine.Construct(jsValue, arguments, jsValue, _calleeExpression);

        if (rented)
        {
            engine._jsValueArrayPool.ReturnArray(arguments);
        }

        return instance;
    }
}
