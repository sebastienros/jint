namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintPrivateIdentifierExpression : JintExpression
{
    private readonly string _privateIdentifierString;

    public JintPrivateIdentifierExpression(PrivateIdentifier expression) : base(expression)
    {
        _privateIdentifierString = expression.Name;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var env = engine.ExecutionContext.PrivateEnvironment;

        var privateIdentifier = env!.ResolvePrivateIdentifier(_privateIdentifierString);
        if (privateIdentifier is not null)
        {
            return privateIdentifier;
        }

        ExceptionHelper.ThrowReferenceError(engine.Realm, "TODO Not found!!");
        return null;
    }
}
