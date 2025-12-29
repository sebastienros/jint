using Jint.Native;
using Jint.Native.Iterator;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintSpreadExpression : JintExpression
{
    internal readonly JintExpression _argument;
    private readonly string? _argumentName;

    public JintSpreadExpression(SpreadElement expression) : base(expression)
    {
        _argument = Build(expression.Argument);
        _argumentName = (expression.Argument as Identifier)?.Name;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
        return objectInstance;
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        // need to notify correct node when taking shortcut
        context.LastSyntaxElement = _expression;

        GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
        return objectInstance;
    }

    internal void GetValueAndCheckIterator(EvaluationContext context, out JsValue instance, out IteratorInstance? iterator)
    {
        instance = _argument.GetValue(context);

        // If generator suspended during argument evaluation, don't try to get iterator
        if (context.Engine.ExecutionContext.Suspended)
        {
            iterator = null;
            return;
        }

        if (instance is null || !instance.TryGetIterator(context.Engine.Realm, out iterator))
        {
            iterator = null;
            Throw.TypeError(context.Engine.Realm, _argumentName + " is not iterable");
        }
    }
}