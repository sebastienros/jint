using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintIdentifierExpression : JintExpression
{
    private readonly Environment.BindingName _identifier;

    public JintIdentifierExpression(Identifier expression) : this(expression, new Environment.BindingName(expression.Name))
    {
        _identifier = new Environment.BindingName(((Identifier) _expression).Name);
    }

    public JintIdentifierExpression(Identifier identifier, Environment.BindingName bindingName) : base(identifier)
    {
        _identifier = bindingName;
    }

    public Environment.BindingName Identifier => _identifier;

    public bool HasEvalOrArguments
    {
        get
        {
            var key = _identifier.Key;
            return key == KnownKeys.Eval || key == KnownKeys.Arguments;
        }
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var env = engine.ExecutionContext.LexicalEnvironment;
        var strict = StrictModeScope.IsStrictModeCode;
        var identifierEnvironment = JintEnvironment.TryGetIdentifierEnvironmentWithBinding(env, _identifier, out var temp)
            ? temp
            : JsValue.Undefined;

        return engine._referencePool.Rent(identifierEnvironment, _identifier.Value, strict, thisValue: null);
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        // need to notify correct node when taking shortcut
        context.LastSyntaxElement = _expression;

        var identifier = Identifier;
        if (identifier.CalculatedValue is not null)
        {
            return identifier.CalculatedValue;
        }

        var engine = context.Engine;
        var env = engine.ExecutionContext.LexicalEnvironment;
        var strict = StrictModeScope.IsStrictModeCode;

        if (JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                env,
                identifier,
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
            var reference = engine._referencePool.Rent(JsValue.Undefined, identifier.Value, strict, thisValue: null);
            value = engine.GetValue(reference, returnReferenceToPool: true);
        }

        // make sure arguments access freezes state
        if (value is JsArguments argumentsInstance)
        {
            argumentsInstance.Materialize();
        }

        return value;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowNotInitialized(Engine engine)
    {
        ExceptionHelper.ThrowReferenceError(engine.Realm, $"{_identifier.Key.Name} has not been initialized");
    }
}
