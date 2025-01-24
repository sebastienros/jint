namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintNewExpression : JintExpression
{
    private readonly ExpressionCache _arguments = new();
    private JintExpression _calleeExpression = null!;
    private bool _initialized;

    public JintNewExpression(NewExpression expression) : base(expression)
    {
    }

    private void Initialize(EvaluationContext context)
    {
        var expression = (NewExpression) _expression;
        _arguments.Initialize(context, expression.Arguments.AsSpan());
        _calleeExpression = Build(expression.Callee);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            Initialize(context);
            _initialized = true;
        }

        var engine = context.Engine;

        // todo: optimize by defining a common abstract class or interface
        var jsValue = _calleeExpression.GetValue(context);

        var arguments = _arguments.ArgumentListEvaluation(context, out var rented);

        // Reset the location to the "new" keyword so that if an Error object is
        // constructed below, the stack trace will capture the correct location.
        context.LastSyntaxElement = _expression;

        if (!jsValue.IsConstructor)
        {
            ExceptionHelper.ThrowTypeError(engine.Realm, $"{_calleeExpression.SourceText} is not a constructor");
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
