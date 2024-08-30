using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintUpdateExpression : JintExpression
{
    private JintExpression _argument = null!;
    private int _change;
    private bool _prefix;

    private JintIdentifierExpression? _leftIdentifier;
    private bool _evalOrArguments;
    private bool _initialized;

    public JintUpdateExpression(UpdateExpression expression) : base(expression)
    {
    }

    private void Initialize()
    {
        var expression = (UpdateExpression) _expression;
        _prefix = expression.Prefix;
        _argument = Build(expression.Argument);
        if (expression.Operator == Operator.Increment)
        {
            _change = 1;
        }
        else if (expression.Operator == Operator.Decrement)
        {
            _change = -1;
        }
        else
        {
            ExceptionHelper.ThrowArgumentException();
        }

        _leftIdentifier = _argument as JintIdentifierExpression;
        _evalOrArguments = _leftIdentifier?.HasEvalOrArguments == true;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            Initialize();
            _initialized = true;
        }

        var fastResult = _leftIdentifier != null
            ? UpdateIdentifier(context)
            : null;

        return fastResult ?? UpdateNonIdentifier(context);
    }

    private JsValue UpdateNonIdentifier(EvaluationContext context)
    {
        var engine = context.Engine;
        var reference = _argument.Evaluate(context) as Reference;
        if (reference is null)
        {
            ExceptionHelper.ThrowTypeError(engine.Realm, "Invalid left-hand side expression");
        }

        reference.AssertValid(engine.Realm);

        var value = engine.GetValue(reference, false);
        var isInteger = value._type == InternalTypes.Integer;

        JsValue? newValue = null;

        var operatorOverloaded = false;
        if (context.OperatorOverloadingAllowed)
        {
            if (JintUnaryExpression.TryOperatorOverloading(context, _argument.GetValue(context), _change > 0 ? "op_Increment" : "op_Decrement", out var result))
            {
                operatorOverloaded = true;
                newValue = result;
            }
        }

        if (!operatorOverloaded)
        {
            if (isInteger)
            {
                newValue = JsNumber.Create(value.AsInteger() + _change);
            }
            else if (!value.IsBigInt())
            {
                newValue = JsNumber.Create(TypeConverter.ToNumber(value) + _change);
            }
            else
            {
                newValue = JsBigInt.Create(TypeConverter.ToBigInt(value) + _change);
            }
        }

        engine.PutValue(reference, newValue!);
        engine._referencePool.Return(reference);

        if (_prefix)
        {
            return newValue!;
        }
        else
        {
            if (isInteger || operatorOverloaded)
            {
                return value;
            }

            if (!value.IsBigInt())
            {
                return JsNumber.Create(TypeConverter.ToNumber(value));
            }

            return JsBigInt.Create(value);
        }
    }

    private JsValue? UpdateIdentifier(EvaluationContext context)
    {
        var name = _leftIdentifier!.Identifier;
        var strict = StrictModeScope.IsStrictModeCode;

        if (JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                context.Engine.ExecutionContext.LexicalEnvironment,
                name,
                strict,
                out var environmentRecord,
                out var value))
        {
            if (_evalOrArguments && strict)
            {
                ExceptionHelper.ThrowSyntaxError(context.Engine.Realm);
            }

            var isInteger = value._type == InternalTypes.Integer;

            JsValue? newValue = null;

            var operatorOverloaded = false;
            if (context.OperatorOverloadingAllowed)
            {
                if (JintUnaryExpression.TryOperatorOverloading(context, _argument.GetValue(context), _change > 0 ? "op_Increment" : "op_Decrement", out var result))
                {
                    operatorOverloaded = true;
                    newValue = result;
                }
            }

            if (!operatorOverloaded)
            {
                if (isInteger)
                {
                    newValue = JsNumber.Create(value.AsInteger() + _change);
                }
                else if (value._type != InternalTypes.BigInt)
                {
                    newValue = JsNumber.Create(TypeConverter.ToNumber(value) + _change);
                }
                else
                {
                    newValue = JsBigInt.Create(TypeConverter.ToBigInt(value) + _change);
                }
            }

            environmentRecord.SetMutableBinding(name.Key, newValue!, strict);
            if (_prefix)
            {
                return newValue;
            }

            if (!value.IsBigInt() && !value.IsNumber() && !operatorOverloaded)
            {
                return JsNumber.Create(TypeConverter.ToNumber(value));
            }

            return value;
        }

        return null;
    }
}
