using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintUpdateExpression : JintExpression
{
    private readonly JintExpression _argument;
    private readonly int _change;
    private readonly bool _prefix;

    private readonly JintIdentifierExpression? _leftIdentifier;
    private readonly bool _evalOrArguments;

    public JintUpdateExpression(UpdateExpression expression) : base(expression)
    {
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
            Throw.ArgumentException();
        }

        _leftIdentifier = _argument as JintIdentifierExpression;
        _evalOrArguments = _leftIdentifier?.HasEvalOrArguments == true;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var fastResult = _leftIdentifier != null
            ? UpdateIdentifier(context)
            : null;

        return fastResult ?? UpdateNonIdentifier(context);
    }

    internal override void EvaluateAndDiscard(EvaluationContext context)
    {
        var oldSyntaxElement = context.LastSyntaxElement;
        context.PrepareFor(_expression);

        if (!TryUpdateUnboxed(context))
        {
            EvaluateInternal(context);
        }

        context.LastSyntaxElement = oldSyntaxElement;
    }

    /// <summary>
    /// Discard-mode fast path: increments a slot-stored number binding without materializing
    /// the old or new value. Anything that needs the full semantics (operator overloading,
    /// generators/async, TDZ, const, non-number values, dictionary/global/object environments)
    /// falls back to the materialized path which produces the exact errors and coercions.
    /// </summary>
    private bool TryUpdateUnboxed(EvaluationContext context)
    {
        var engine = context.Engine;
        if (_leftIdentifier is null
            || context.OperatorOverloadingAllowed
            || engine.ExecutionContext.Suspendable is not null)
        {
            return false;
        }

        if (_evalOrArguments && StrictModeScope.IsStrictModeCode)
        {
            // full path raises the proper SyntaxError
            return false;
        }

        var name = _leftIdentifier.Identifier;
        if (!JintEnvironment.TryGetIdentifierEnvironmentWithBinding(
                engine.ExecutionContext.LexicalEnvironment,
                name,
                out var record)
            || record is not DeclarativeEnvironment declarativeEnvironment
            || !declarativeEnvironment.TryGetNumberSlot(name.Key, out var slotIndex, out var value))
        {
            return false;
        }

        declarativeEnvironment.SetNumberSlot(slotIndex, value + _change);
        return true;
    }

    private JsValue UpdateNonIdentifier(EvaluationContext context)
    {
        var engine = context.Engine;
        var reference = _argument.Evaluate(context) as Reference;
        if (reference is null)
        {
            Throw.ReferenceError(engine.Realm, "Invalid left-hand side in assignment");
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
                newValue = JsNumber.Create((long) value.AsInteger() + _change);
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
                out var value)
            && value is not null) // an uninitialized (TDZ) binding reports null; the Reference path produces the proper ReferenceError
        {
            if (_evalOrArguments && strict)
            {
                Throw.SyntaxError(context.Engine.Realm);
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
                    newValue = JsNumber.Create((long) value.AsInteger() + _change);
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
