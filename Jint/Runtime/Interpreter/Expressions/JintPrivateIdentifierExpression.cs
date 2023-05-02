using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintPrivateIdentifierExpression : JintExpression
{
    private readonly PrivateName _privateName;

    public JintPrivateIdentifierExpression(PrivateIdentifier expression) : base(expression)
    {
        _privateName = new PrivateName(expression);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var strict = StrictModeScope.IsStrictModeCode;
        var env = engine.ExecutionContext.LexicalEnvironment;

        if (JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                env,
                new EnvironmentRecord.BindingName(_privateName),
                strict,
                out _,
                out var value))
        {
        }
        else
        {
            var reference = engine._referencePool.Rent(JsValue.Undefined, _privateName, strict, thisValue: null);
            value = engine.GetValue(reference, true);
        }


        return value;
    }
}
