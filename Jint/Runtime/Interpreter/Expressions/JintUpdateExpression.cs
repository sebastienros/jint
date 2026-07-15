using Jint.Native;
using Jint.Runtime.Environments;

using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintUpdateExpression : JintExpression
{
    private readonly JintExpression _argument;
    private readonly int _change;
    private readonly bool _prefix;

    private readonly JintIdentifierExpression? _leftIdentifier;
    private readonly bool _evalOrArguments;

    // Slot-location cache for the discard-mode fast path; see SlotLocationCache for the
    // validity reasoning (with-statement shadowing, pooling, cross-engine sharing).
    private SlotLocationCache _slotCache;

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

    internal override bool HasDiscardFastPath => true;

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

        if (!_slotCache.TryResolve(engine, engine.ExecutionContext.LexicalEnvironment, _leftIdentifier.Identifier, out var environment, out var slotIndex)
            || !environment.TryGetNumberSlot(slotIndex, out var value))
        {
            return false;
        }

        environment.SetNumberSlot(slotIndex, value + _change);
        return true;
    }

    /// <summary>
    /// Value-producing twin of <see cref="TryUpdateUnboxed"/>: increments/decrements a slot-stored
    /// number binding without an environment-chain walk or a second SetMutableBinding lookup, and
    /// materializes the value the surrounding expression consumes — the OLD value for postfix
    /// (<c>i++</c>), the NEW value for prefix (<c>++i</c>). Everything the fast path cannot prove
    /// safe (operator overloading, generators/async, eval/arguments in strict mode, TDZ, const,
    /// non-number values, global/object/dictionary environments) declines so the full materialized
    /// path produces the exact errors and coercions.
    /// </summary>
    private bool TryUpdateIdentifierNumberSlot(EvaluationContext context, out JsValue? result)
    {
        var engine = context.Engine;
        if (context.OperatorOverloadingAllowed
            || engine.ExecutionContext.Suspendable is not null)
        {
            result = null;
            return false;
        }

        if (_evalOrArguments && StrictModeScope.IsStrictModeCode)
        {
            // full path raises the proper SyntaxError
            result = null;
            return false;
        }

        if (!_slotCache.TryResolve(engine, engine.ExecutionContext.LexicalEnvironment, _leftIdentifier!.Identifier, out var environment, out var slotIndex)
            || !environment.TryGetNumberSlot(slotIndex, out var value))
        {
            result = null;
            return false;
        }

        var updated = value + _change;
        environment.SetNumberSlot(slotIndex, updated);
        // postfix (i++) yields the old value, prefix (++i) the new value; both are numbers here
        result = JsNumber.Create(_prefix ? updated : value);
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
        var engine = context.Engine;

        // Local/block/function number-counter fast path (arr[i++], x = data[j++], while(n--)):
        // resolve the slot location from the per-node cache and read/write the raw double, exactly
        // as the discard-mode TryUpdateUnboxed does, but materialize the value the surrounding
        // expression needs. Anything the fast path cannot prove safe declines to the paths below.
        if (TryUpdateIdentifierNumberSlot(context, out var slotResult))
        {
            return slotResult;
        }

        // Global-binding fast path: when this identifier resolves to a cached plain writable global
        // data property, read and write its descriptor value directly — no environment chain walk and
        // no second SetMutableBinding lookup. Mirrors the read cache in JintIdentifierExpression and the
        // simple-assignment cache (AssignToCachedGlobalBinding). Operator-overloading-capable contexts
        // fall through so a CLR op_Increment/op_Decrement is still honored.
        if (_leftIdentifier!._cachedGlobalEnv is not null && !context.OperatorOverloadingAllowed)
        {
            var descriptor = _leftIdentifier.TryGetValidatedGlobalDescriptor(engine, engine.ExecutionContext.LexicalEnvironment);
            // Writable is re-checked through the cached reference: defineProperty flips the
            // flag in place without bumping the versions the validator checks.
            if (descriptor is not null && descriptor.Writable && descriptor._value is { } current)
            {
                if (_evalOrArguments && StrictModeScope.IsStrictModeCode)
                {
                    Throw.SyntaxError(engine.Realm);
                }

                JsValue newValue;
                if (current._type == InternalTypes.Integer)
                {
                    newValue = JsNumber.Create((long) current.AsInteger() + _change);
                }
                else if (current._type != InternalTypes.BigInt)
                {
                    newValue = JsNumber.Create(TypeConverter.ToNumber(current) + _change);
                }
                else
                {
                    newValue = JsBigInt.Create(TypeConverter.ToBigInt(current) + _change);
                }

                descriptor._value = newValue;

                if (_prefix)
                {
                    return newValue;
                }

                if (!current.IsBigInt() && !current.IsNumber())
                {
                    return JsNumber.Create(TypeConverter.ToNumber(current));
                }

                return current;
            }
        }

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

            // Remember the binding so subsequent updates of this global take the fast path above,
            // even when the identifier is never read elsewhere (e.g. a write-only counter).
            if (environmentRecord is GlobalEnvironment globalEnv)
            {
                _leftIdentifier.TryRememberGlobalBinding(globalEnv);
            }

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
