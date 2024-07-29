using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintSuperExpression : JintExpression
{
    public JintSuperExpression(Super expression) : base(expression)
    {
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var envRec = (FunctionEnvironment) context.Engine.ExecutionContext.GetThisEnvironment();
        var activeFunction = envRec._functionObject;
        var superConstructor = activeFunction.GetPrototypeOf();
        return superConstructor!;
    }
}