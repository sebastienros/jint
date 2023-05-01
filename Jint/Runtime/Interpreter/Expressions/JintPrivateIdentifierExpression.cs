using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Argument;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintPrivateIdentifierExpression : JintExpression
{
    private readonly PrivateName _privateName;

    public JintPrivateIdentifierExpression(PrivateIdentifier expression) : base(expression)
    {
        _privateName = new PrivateName(expression.Name);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var env = engine.ExecutionContext.LexicalEnvironment;
        var strict = StrictModeScope.IsStrictModeCode;
        var identifierEnvironment = JintEnvironment.TryGetIdentifierEnvironmentWithBinding(env, new EnvironmentRecord.BindingName("tODO"), out var temp)
            ? temp
            : JsValue.Undefined;

        return engine._referencePool.Rent(identifierEnvironment, _privateName, strict, thisValue: null);
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        // need to notify correct node when taking shortcut
        context.LastSyntaxElement = _expression;

        var strict = StrictModeScope.IsStrictModeCode;
        var engine = context.Engine;
        var env = engine.ExecutionContext.LexicalEnvironment;

        if (JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                env,
                new EnvironmentRecord.BindingName("TODO"),
                strict,
                out _,
                out var value))
        {
            if (value is null)
            {
                ThrowNotInitialized(engine);
            }
        }
        else
        {
            var reference = engine._referencePool.Rent(JsValue.Undefined, _privateName, strict, thisValue: null);
            value = engine.GetValue(reference, true);
        }

        // make sure arguments access freezes state
        if (value is ArgumentsInstance argumentsInstance)
        {
            argumentsInstance.Materialize();
        }

        return value;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowNotInitialized(Engine engine)
    {
        ExceptionHelper.ThrowReferenceError(engine.Realm, _privateName + " has not been initialized");
    }
}
