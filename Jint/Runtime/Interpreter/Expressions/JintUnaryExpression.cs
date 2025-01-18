using Jint.Extensions;
using Jint.Native;
using Jint.Runtime.Interop;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;

using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintUnaryExpression : JintExpression
{
    private readonly record struct OperatorKey(string OperatorName, Type Operand);
    private static readonly ConcurrentDictionary<OperatorKey, MethodDescriptor?> _knownOperators = new();

    private readonly JintExpression _argument;
    private readonly Operator _operator;

    private JintUnaryExpression(NonUpdateUnaryExpression expression) : base(expression)
    {
        _argument = Build(expression.Argument);
        _operator = expression.Operator;
    }

    internal static JintExpression Build(NonUpdateUnaryExpression expression)
    {
        if (expression.Operator == Operator.TypeOf)
        {
            return new JintTypeOfExpression(expression);
        }

        return BuildConstantExpression(expression) ?? new JintUnaryExpression(expression);
    }

    internal static JintExpression? BuildConstantExpression(NonUpdateUnaryExpression expression)
    {
        if (expression is { Operator: Operator.UnaryNegation, Argument: Literal literal })
        {
            var value = JintLiteralExpression.ConvertToJsValue(literal);
            if (value is not null)
            {
                // valid for caching
                var evaluatedValue = EvaluateMinus(value);
                return new JintConstantExpression(expression, evaluatedValue);
            }
        }

        return null;
    }

    private sealed class JintTypeOfExpression : JintExpression
    {
        private readonly JintExpression _argument;

        public JintTypeOfExpression(NonUpdateUnaryExpression expression) : base(expression)
        {
            _argument = Build(expression.Argument);
        }

        public override JsValue GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxElement = _expression;
            return (JsValue) EvaluateInternal(context);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            var result = _argument.Evaluate(context);
            JsValue v;

            if (result is Reference rf)
            {
                if (rf.IsUnresolvableReference)
                {
                    engine._referencePool.Return(rf);
                    return JsString.UndefinedString;
                }

                v = engine.GetValue(rf, returnReferenceToPool: true);
            }
            else
            {
                v = (JsValue) result;
            }

            return GetTypeOfString(v);
        }

        private static JsString GetTypeOfString(JsValue v)
        {
            if (v.IsUndefined())
            {
                return JsString.UndefinedString;
            }

            if (v.IsNull())
            {
                return JsString.ObjectString;
            }

            switch (v.Type)
            {
                case Types.Boolean: return JsString.BooleanString;
                case Types.Number: return JsString.NumberString;
                case Types.BigInt: return JsString.BigIntString;
                case Types.String: return JsString.StringString;
                case Types.Symbol: return JsString.SymbolString;
            }

            if (v.IsCallable)
            {
                return JsString.FunctionString;
            }

            return JsString.ObjectString;
        }
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        // need to notify correct node when taking shortcut
        context.LastSyntaxElement = _expression;
        return EvaluateJsValue(context);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        return EvaluateJsValue(context);
    }

    private JsValue EvaluateJsValue(EvaluationContext context)
    {
        var engine = context.Engine;
        switch (_operator)
        {
            case Operator.UnaryPlus:
                {
                    var v = _argument.GetValue(context);
                    if (context.OperatorOverloadingAllowed &&
                        TryOperatorOverloading(context, v, "op_UnaryPlus", out var result))
                    {
                        return result;
                    }

                    return TypeConverter.ToNumber(v);
                }
            case Operator.UnaryNegation:
                {
                    var v = _argument.GetValue(context);
                    if (context.OperatorOverloadingAllowed &&
                        TryOperatorOverloading(context, v, "op_UnaryNegation", out var result))
                    {
                        return result;
                    }

                    return EvaluateMinus(v);
                }
            case Operator.BitwiseNot:
                {
                    var v = _argument.GetValue(context);
                    if (context.OperatorOverloadingAllowed &&
                        TryOperatorOverloading(context, v, "op_OnesComplement", out var result))
                    {
                        return result;
                    }

                    var value = TypeConverter.ToNumeric(v);
                    if (value.IsNumber())
                    {
                        return JsNumber.Create(~TypeConverter.ToInt32(value));
                    }

                    return JsBigInt.Create(~value.AsBigInt());
                }
            case Operator.LogicalNot:
                {
                    var v = _argument.GetValue(context);
                    if (context.OperatorOverloadingAllowed &&
                        TryOperatorOverloading(context, v, "op_LogicalNot", out var result))
                    {
                        return result;
                    }

                    return !TypeConverter.ToBoolean(v) ? JsBoolean.True : JsBoolean.False;
                }

            case Operator.Delete:
                // https://262.ecma-international.org/5.1/#sec-11.4.1
                if (_argument.Evaluate(context) is not Reference r)
                {
                    return JsBoolean.True;
                }

                if (r.IsUnresolvableReference)
                {
                    if (r.Strict)
                    {
                        ExceptionHelper.ThrowSyntaxError(engine.Realm, "Delete of an unqualified identifier in strict mode.");
                    }

                    engine._referencePool.Return(r);
                    return JsBoolean.True;
                }

                if (r.IsPropertyReference)
                {
                    if (r.IsSuperReference)
                    {
                        ExceptionHelper.ThrowReferenceError(engine.Realm, "Unsupported reference to 'super'");
                    }

                    var o = TypeConverter.ToObject(engine.Realm, r.Base);

                    r.EvaluateAndCachePropertyKey();
                    var deleteStatus = o.Delete(r.ReferencedName);

                    if (!deleteStatus)
                    {
                        if (r.Strict)
                        {
                            ExceptionHelper.ThrowTypeError(engine.Realm, $"Cannot delete property '{r.ReferencedName}' of {o}");
                        }

                        if (StrictModeScope.IsStrictModeCode && !r.Base.AsObject().GetOwnProperty(r.ReferencedName).Configurable)
                        {
                            ExceptionHelper.ThrowTypeError(engine.Realm, $"Cannot delete property '{r.ReferencedName}' of {o}");
                        }
                    }

                    engine._referencePool.Return(r);
                    return deleteStatus ? JsBoolean.True : JsBoolean.False;
                }

                if (r.Strict)
                {
                    ExceptionHelper.ThrowSyntaxError(engine.Realm);
                }

                var bindings = (Environment) r.Base;
                engine._referencePool.Return(r);

                return bindings.DeleteBinding(r.ReferencedName.ToString()) ? JsBoolean.True : JsBoolean.False;

            case Operator.Void:
                _argument.GetValue(context);
                return JsValue.Undefined;

            default:
                ExceptionHelper.ThrowArgumentException();
                return null;
        }
    }

    internal static JsValue EvaluateMinus(JsValue value)
    {
        if (value.IsInteger())
        {
            var asInteger = value.AsInteger();
            if (asInteger != 0)
            {
                return JsNumber.Create(asInteger * -1);
            }
        }

        value = TypeConverter.ToNumeric(value);
        if (value.IsNumber())
        {
            var n = ((JsNumber) value)._value;
            return double.IsNaN(n) ? JsNumber.DoubleNaN : JsNumber.Create(n * -1);
        }

        var bigInt = value.AsBigInt();
        return JsBigInt.Create(BigInteger.Negate(bigInt));
    }

    internal static bool TryOperatorOverloading(EvaluationContext context, JsValue value, string clrName, [NotNullWhen(true)] out JsValue? result)
    {
        var operand = value.ToObject();

        if (operand != null)
        {
            var operandType = operand.GetType();
            var arguments = new[] { value };

            var key = new OperatorKey(clrName, operandType);
            var method = _knownOperators.GetOrAdd(key, _ =>
            {
                MethodInfo? foundMethod = null;
                foreach (var x in operandType.GetOperatorOverloadMethods())
                {
                    if (string.Equals(x.Name, clrName, StringComparison.Ordinal) && x.GetParameters().Length == 1)
                    {
                        foundMethod = x;
                        break;
                    }
                }

                if (foundMethod != null)
                {
                    return new MethodDescriptor(foundMethod);
                }
                return null;
            });

            if (method != null)
            {
                result = method.Call(context.Engine, null, arguments);
                return true;
            }
        }
        result = null;
        return false;
    }
}
