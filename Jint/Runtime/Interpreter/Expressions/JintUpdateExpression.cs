using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime.Descriptors;
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

        if (_evalOrArguments && engine.ExecutionContext.Strict)
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

        if (_evalOrArguments && engine.ExecutionContext.Strict)
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
        var target = _argument.Evaluate(context);

        // `(await p).x++`: the target suspended and this is the suspension sentinel, not a reference to
        // read and write back (sebastienros/jint#4086). Bail before the "invalid left-hand side" test as
        // well, since a suspended pass is not an invalid target. The resume re-evaluates the whole update.
        if (context.IsSuspended())
        {
            engine._referencePool.Return(target as Reference);
            return JsValue.Undefined;
        }

        var reference = target as Reference;
        if (reference is null)
        {
            Throw.ReferenceError(engine.Realm, "Invalid left-hand side in assignment");
        }

        reference.AssertValid(engine.Realm);

        // From here on `value` is the operand as read, and once a lane below has converted it, the old numeric value
        // a postfix update returns.
        var value = engine.GetValue(reference, false);

        JsValue? newValue = null;

        var operatorOverloaded = false;
        if (context.OperatorOverloadingAllowed && TryOperatorOverloading(context, value, out var result))
        {
            operatorOverloaded = true;
            newValue = result;
        }

        if (!operatorOverloaded)
        {
            if (value._type == InternalTypes.Integer)
            {
                newValue = JsNumber.Create((long) value.AsInteger() + _change);
            }
            else if (value._type == InternalTypes.Number)
            {
                newValue = JsNumber.Create(((JsNumber) value)._value + _change);
            }
            else if (value._type == InternalTypes.BigInt)
            {
                newValue = JsBigInt.Create(((JsBigInt) value)._value + _change);
            }
            else
            {
                value = TypeConverter.ToNumeric(value);
                newValue = AddChange(value);
            }
        }

        engine.PutValue(reference, newValue!);
        engine._referencePool.Return(reference);

        return _prefix ? newValue! : value;
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

        // Global-binding fast path: when this identifier resolves to a cached plain global data
        // property that is writable, read and write its descriptor value directly — no environment chain
        // walk and no second SetMutableBinding lookup. Mirrors the read cache in JintIdentifierExpression and
        // the simple-assignment cache (AssignToCachedGlobalBinding). Operator-overloading-capable contexts
        // fall through so a CLR op_Increment/op_Decrement is still honored.
        if (_leftIdentifier!._cachedGlobalEnv is not null && !context.OperatorOverloadingAllowed)
        {
            var descriptor = _leftIdentifier.TryGetValidatedGlobalDescriptor(engine, engine.ExecutionContext.LexicalEnvironment);
            // Writable is re-checked through the cached reference: the cache holds read-only
            // globals too, for reads, and defineProperty flips the flag in place without bumping
            // the versions the validator checks. A non-writable hit takes the full path below.
            if (descriptor is not null && descriptor.Writable && descriptor._value is { } current)
            {
                if (_evalOrArguments && engine.ExecutionContext.Strict)
                {
                    Throw.SyntaxError(engine.Realm);
                }

                JsValue newValue;
                if (current._type == InternalTypes.Integer)
                {
                    newValue = JsNumber.Create((long) current.AsInteger() + _change);
                }
                else if (current._type == InternalTypes.Number)
                {
                    newValue = JsNumber.Create(((JsNumber) current)._value + _change);
                }
                else if (current._type == InternalTypes.BigInt)
                {
                    newValue = JsBigInt.Create(((JsBigInt) current)._value + _change);
                }
                else if (current.IsObject())
                {
                    // the only operand whose conversion runs user code
                    return UpdateCachedGlobalObject(engine, descriptor, current);
                }
                else
                {
                    // a primitive, which ToNumeric converts without running user code; `current` becomes the old
                    // numeric value a postfix update returns
                    current = TypeConverter.ToNumeric(current);
                    newValue = AddChange(current);
                }

                descriptor._value = newValue;

                return _prefix ? newValue : current;
            }
        }

        var name = _leftIdentifier!.Identifier;
        var strict = engine.ExecutionContext.Strict;

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

            JsValue? newValue = null;

            var operatorOverloaded = false;
            if (context.OperatorOverloadingAllowed && TryOperatorOverloading(context, value, out var result))
            {
                operatorOverloaded = true;
                newValue = result;
            }

            if (!operatorOverloaded)
            {
                if (value._type == InternalTypes.Integer)
                {
                    newValue = JsNumber.Create((long) value.AsInteger() + _change);
                }
                else if (value._type == InternalTypes.Number)
                {
                    newValue = JsNumber.Create(((JsNumber) value)._value + _change);
                }
                else if (value._type == InternalTypes.BigInt)
                {
                    newValue = JsBigInt.Create(((JsBigInt) value)._value + _change);
                }
                else
                {
                    // `value` becomes the old numeric value a postfix update returns
                    value = TypeConverter.ToNumeric(value);
                    newValue = AddChange(value);
                }
            }

            environmentRecord.SetMutableBinding(name.Key, newValue!, strict);

            // Remember the binding so subsequent updates of this global take the fast path above,
            // even when the identifier is never read elsewhere (e.g. a write-only counter).
            if (environmentRecord is GlobalEnvironment globalEnv)
            {
                _leftIdentifier.TryRememberGlobalBinding(globalEnv);
            }

            return _prefix ? newValue : value;
        }

        return null;
    }

    /// <summary>
    /// Number::add or BigInt::add of the change to an operand ToNumeric has already converted. Every lane above
    /// converts the operand once, before PutValue, and a postfix update returns that converted value rather than the
    /// operand (https://tc39.es/ecma262/#sec-postfix-increment-operator-runtime-semantics-evaluation,
    /// https://tc39.es/ecma262/#sec-postfix-decrement-operator-runtime-semantics-evaluation,
    /// https://tc39.es/ecma262/#sec-prefix-increment-operator-runtime-semantics-evaluation,
    /// https://tc39.es/ecma262/#sec-prefix-decrement-operator-runtime-semantics-evaluation). Only an operand that is
    /// neither a Number nor a BigInt reaches it, so it stays out of line.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue AddChange(JsValue numeric)
    {
        return numeric.IsBigInt()
            ? JsBigInt.Create(numeric.AsBigInt() + _change)
            : JsNumber.Create(TypeConverter.ToNumber(numeric) + _change);
    }

    /// <summary>
    /// A CLR <c>op_Increment</c> / <c>op_Decrement</c> of a host object, given the value the update has already read
    /// rather than reading the operand a second time. Only a <see cref="JintBinaryExpression.IsHostOperand">host
    /// operand</see> is offered, so a BigInt is not treated as the <see cref="System.Numerics.BigInteger"/> it is stored as.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryOperatorOverloading(EvaluationContext context, JsValue value, [NotNullWhen(true)] out JsValue? result) =>
        JintUnaryExpression.TryOperatorOverloading(context, value, _change > 0 ? "op_Increment" : "op_Decrement", out result);

    /// <summary>
    /// The cached-global arm of <see cref="UpdateIdentifier"/> for an object operand. Converting it with ToNumeric runs
    /// its <c>valueOf</c>, <c>toString</c> or <c>@@toPrimitive</c> once, before PutValue
    /// (https://tc39.es/ecma262/#sec-postfix-increment-operator-runtime-semantics-evaluation,
    /// https://tc39.es/ecma262/#sec-prefix-increment-operator-runtime-semantics-evaluation), and that code may change
    /// the global, so the store goes through <see cref="CachedGlobalWrite"/>. A postfix update returns the converted
    /// value, as the slow path below does.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue UpdateCachedGlobalObject(Engine engine, PropertyDescriptor descriptor, JsValue current)
    {
        var identifier = _leftIdentifier!;
        var write = new CachedGlobalWrite(identifier._cachedGlobalEnv!, descriptor);

        var oldValue = TypeConverter.ToNumeric(current);
        var newValue = AddChange(oldValue);
        write.Complete(identifier.Identifier, newValue, engine.ExecutionContext.Strict);

        return _prefix ? newValue : oldValue;
    }
}
