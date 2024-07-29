using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintClassExpression : JintExpression
{
    private readonly ClassDefinition _classDefinition;

    public JintClassExpression(ClassExpression expression) : base(expression)
    {
        _classDefinition = new ClassDefinition(expression.Id?.Name, expression.SuperClass, expression.Body);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var env = context.Engine.ExecutionContext.LexicalEnvironment;
        return _classDefinition.BuildConstructor(context, env);
    }
}
