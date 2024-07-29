using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintThisExpression : JintExpression
{
    public JintThisExpression(ThisExpression expression) : base(expression)
    {
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        return context.Engine.ResolveThisBinding();
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        // need to notify correct node when taking shortcut
        context.LastSyntaxElement = _expression;
        return context.Engine.ResolveThisBinding();
    }
}