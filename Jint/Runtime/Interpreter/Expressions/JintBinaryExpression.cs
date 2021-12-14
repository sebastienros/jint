using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract class JintBinaryExpression : JintExpression
    {
        private readonly record struct OperatorKey(string OperatorName, Type Left, Type Right);
        private static readonly ConcurrentDictionary<OperatorKey, MethodDescriptor> _knownOperators = new();

        private readonly JintExpression _left;
        private readonly JintExpression _right;

        private JintBinaryExpression(Engine engine, BinaryExpression expression) : base(expression)
        {
            _left = Build(engine, expression.Left);
            _right = Build(engine, expression.Right);
        }

        internal static bool TryOperatorOverloading(
            EvaluationContext context,
            JsValue leftValue,
            JsValue rightValue,
            string clrName,
            out object result)
        {
            var left = leftValue.ToObject();
            var right = rightValue.ToObject();

            if (left != null && right != null)
            {
                var leftType = left.GetType();
                var rightType = right.GetType();
                var arguments = new[] { leftValue, rightValue };

                var key = new OperatorKey(clrName, leftType, rightType);
                var method = _knownOperators.GetOrAdd(key, _ =>
                {
                    var leftMethods = leftType.GetOperatorOverloadMethods();
                    var rightMethods = rightType.GetOperatorOverloadMethods();

                    var methods = leftMethods.Concat(rightMethods).Where(x => x.Name == clrName && x.GetParameters().Length == 2);
                    var _methods = MethodDescriptor.Build(methods.ToArray());

                    return TypeConverter.FindBestMatch(context.Engine, _methods, _ => arguments).FirstOrDefault().Method;
                });

                if (method != null)
                {
                    try
                    {
                        result = method.Call(context.Engine, null, arguments);
                        return true;
                    }
                    catch
                    {
                        result = null;
                        return false;
                    }
                }
            }

            result = null;
            return false;
        }

        internal static JintExpression Build(Engine engine, BinaryExpression expression)
        {
            JintBinaryExpression result = null;
            switch (expression.Operator)
            {
                case BinaryOperator.StrictlyEqual:
                    result = new StrictlyEqualBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.StricltyNotEqual:
                    result = new StrictlyNotEqualBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Less:
                    result = new LessBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Greater:
                    result = new GreaterBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Plus:
                    result = new PlusBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Minus:
                    result = new MinusBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Times:
                    result = new TimesBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Divide:
                    result = new DivideBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Equal:
                    result = new EqualBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.NotEqual:
                    result = new EqualBinaryExpression(engine, expression, invert: true);
                    break;
                case BinaryOperator.GreaterOrEqual:
                    result = new CompareBinaryExpression(engine, expression, leftFirst: true);
                    break;
                case BinaryOperator.LessOrEqual:
                    result = new CompareBinaryExpression(engine, expression, leftFirst: false);
                    break;
                case BinaryOperator.BitwiseAnd:
                case BinaryOperator.BitwiseOr:
                case BinaryOperator.BitwiseXOr:
                case BinaryOperator.LeftShift:
                case BinaryOperator.RightShift:
                case BinaryOperator.UnsignedRightShift:
                    result = new BitwiseBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.InstanceOf:
                    result = new InstanceOfBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Exponentiation:
                    result = new ExponentiationBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Modulo:
                    result = new ModuloBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.In:
                    result = new InBinaryExpression(engine, expression);
                    break;
                default:
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(expression.Operator), "cannot handle operator");
                    break;
            }

            if (expression.Operator != BinaryOperator.InstanceOf
                && expression.Operator != BinaryOperator.In
                && expression.Left is Literal leftLiteral
                && expression.Right is Literal rightLiteral)
            {
                var lval = JintLiteralExpression.ConvertToJsValue(leftLiteral);
                var rval = JintLiteralExpression.ConvertToJsValue(rightLiteral);

                if (lval is not null && rval is not null)
                {
                    // we have fixed result
                    var context = new EvaluationContext(engine);
                    return new JintConstantExpression(expression, result.GetValue(context).Value);
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AreNonBigIntOperands(JsValue left, JsValue right)
        {
            return left._type != InternalTypes.BigInt && right._type != InternalTypes.BigInt;
        }

        internal static void AssertValidBigIntArithmeticOperands(EvaluationContext context, JsValue left, JsValue right)
        {
            if (left.Type != right.Type)
            {
                ExceptionHelper.ThrowTypeError(context.Engine.Realm, "Cannot mix BigInt and other types, use explicit conversions");
            }
        }

        private sealed class StrictlyEqualBinaryExpression : JintBinaryExpression
        {
            public StrictlyEqualBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;
                var equal = left == right;
                return NormalCompletion(equal ? JsBoolean.True : JsBoolean.False);
            }
        }

        private sealed class StrictlyNotEqualBinaryExpression : JintBinaryExpression
        {
            public StrictlyNotEqualBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;
                return NormalCompletion(left == right ? JsBoolean.False : JsBoolean.True);
            }
        }

        private sealed class LessBinaryExpression : JintBinaryExpression
        {
            public LessBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_LessThan", out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                var value = Compare(left, right);

                return NormalCompletion(value._type == InternalTypes.Undefined ? JsBoolean.False : value);
            }
        }

        private sealed class GreaterBinaryExpression : JintBinaryExpression
        {
            public GreaterBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_GreaterThan", out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                var value = Compare(right, left, false);

                return NormalCompletion(value._type == InternalTypes.Undefined ? JsBoolean.False : value);
            }
        }

        private sealed class PlusBinaryExpression : JintBinaryExpression
        {
            public PlusBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Addition", out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                if (AreIntegerOperands(left, right))
                {
                    return NormalCompletion(JsNumber.Create(left.AsInteger() + right.AsInteger()));
                }

                var lprim = TypeConverter.ToPrimitive(left);
                var rprim = TypeConverter.ToPrimitive(right);
                JsValue result;
                if (lprim.IsString() || rprim.IsString())
                {
                    result = JsString.Create(TypeConverter.ToString(lprim) + TypeConverter.ToString(rprim));
                }
                else if (AreNonBigIntOperands(left,right))
                {
                    result = JsNumber.Create(TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim));
                }
                else
                {
                    AssertValidBigIntArithmeticOperands(context, lprim, rprim);
                    result = JsBigInt.Create(TypeConverter.ToBigInt(lprim) + TypeConverter.ToBigInt(rprim));
                }

                return NormalCompletion(result);
            }
        }

        private sealed class MinusBinaryExpression : JintBinaryExpression
        {
            public MinusBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Subtraction", out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                JsValue number;
                if (AreIntegerOperands(left, right))
                {
                    number = JsNumber.Create(left.AsInteger() - right.AsInteger());
                }
                else if (left.Type != Types.BigInt && right.Type != Types.BigInt)
                {
                    number = JsNumber.Create(TypeConverter.ToNumber(left) - TypeConverter.ToNumber(right));
                }
                else
                {
                    number = JsBigInt.Create(TypeConverter.ToBigInt(left) - TypeConverter.ToBigInt(right));
                }

                return NormalCompletion(number);
            }
        }

        private sealed class TimesBinaryExpression : JintBinaryExpression
        {
            public TimesBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                JsValue result;
                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Multiply", out var opResult))
                {
                    result = JsValue.FromObject(context.Engine, opResult);
                }
                else if (AreIntegerOperands(left, right))
                {
                    result = JsNumber.Create((long) left.AsInteger() * right.AsInteger());
                }
                else if (left.IsUndefined() || right.IsUndefined())
                {
                    result = Undefined.Instance;
                }
                else if (AreNonBigIntOperands(left, right))
                {
                    result = JsNumber.Create(TypeConverter.ToNumber(left) * TypeConverter.ToNumber(right));
                }
                else
                {
                    AssertValidBigIntArithmeticOperands(context, left, right);
                    result = JsBigInt.Create(TypeConverter.ToBigInt(left) * TypeConverter.ToBigInt(right));
                }

                return NormalCompletion(result);
            }
        }

        private sealed class DivideBinaryExpression : JintBinaryExpression
        {
            public DivideBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Division", out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                return NormalCompletion(Divide(context, left, right));
            }
        }

        private sealed class EqualBinaryExpression : JintBinaryExpression
        {
            private readonly bool _invert;

            public EqualBinaryExpression(Engine engine, BinaryExpression expression, bool invert = false) : base(engine, expression)
            {
                _invert = invert;
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, _invert ? "op_Inequality" : "op_Equality", out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                // if types match, we can take faster strict equality
                var equality = left.Type == right.Type
                    ? left.Equals(right)
                    : left.NonStrictEquals(right);

                return NormalCompletion(equality == !_invert ? JsBoolean.True : JsBoolean.False);
            }
        }

        private sealed class CompareBinaryExpression : JintBinaryExpression
        {
            private readonly bool _leftFirst;

            public CompareBinaryExpression(Engine engine, BinaryExpression expression, bool leftFirst) : base(engine, expression)
            {
                _leftFirst = leftFirst;
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var leftValue = _left.GetValue(context).Value;
                var rightValue = _right.GetValue(context).Value;

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, leftValue, rightValue, _leftFirst ? "op_GreaterThanOrEqual" : "op_LessThanOrEqual", out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                var left = _leftFirst ? leftValue : rightValue;
                var right = _leftFirst ? rightValue : leftValue;

                var value = Compare(left, right, _leftFirst);
                return NormalCompletion(value.IsUndefined() || ((JsBoolean) value)._value ? JsBoolean.False : JsBoolean.True);
            }
        }

        private sealed class InstanceOfBinaryExpression : JintBinaryExpression
        {
            public InstanceOfBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var leftValue = _left.GetValue(context).Value;
                var rightValue = _right.GetValue(context).Value;
                return NormalCompletion(leftValue.InstanceofOperator(rightValue) ? JsBoolean.True : JsBoolean.False);
            }
        }

        private sealed class ExponentiationBinaryExpression : JintBinaryExpression
        {
            public ExponentiationBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                JsValue result;
                if (AreNonBigIntOperands(left,right))
                {
                    result = JsNumber.Create(Math.Pow(TypeConverter.ToNumber(left), TypeConverter.ToNumber(right)));
                }
                else
                {
                    AssertValidBigIntArithmeticOperands(context, left, right);

                    var exponent = TypeConverter.ToBigInt(right);
                    if (exponent > int.MaxValue || exponent < int.MinValue)
                    {
                        ExceptionHelper.ThrowTypeError(context.Engine.Realm, "Cannot do exponentation with exponent not fitting int32");
                    }
                    result = JsBigInt.Create(BigInteger.Pow(TypeConverter.ToBigInt(left), (int) exponent));
                }

                return NormalCompletion(result);
            }
        }

        private sealed class InBinaryExpression : JintBinaryExpression
        {
            public InBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                var oi = right as ObjectInstance;
                if (oi is null)
                {
                    ExceptionHelper.ThrowTypeError(context.Engine.Realm, "in can only be used with an object");
                }

                return NormalCompletion(oi.HasProperty(left) ? JsBoolean.True : JsBoolean.False);
            }
        }

        private sealed class ModuloBinaryExpression : JintBinaryExpression
        {
            public ModuloBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                var result = Undefined.Instance;
                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Modulus", out var opResult))
                {
                    result = JsValue.FromObject(context.Engine, opResult);
                }
                else if (AreIntegerOperands(left, right))
                {
                    var leftInteger = left.AsInteger();
                    var rightInteger = right.AsInteger();

                    if (rightInteger == 0)
                    {
                        result = JsNumber.DoubleNaN;
                    }
                    else
                    {
                        var modulo = leftInteger % rightInteger;
                        if (modulo == 0 && leftInteger < 0)
                        {
                            result = JsNumber.NegativeZero;
                        }
                        else
                        {
                            result = JsNumber.Create(modulo);
                        }
                    }
                }
                else if (left.IsUndefined() || right.IsUndefined())
                {
                    result = Undefined.Instance;
                }
                else if (AreNonBigIntOperands(left, right))
                {
                    result = JsNumber.Create(TypeConverter.ToNumber(left) % TypeConverter.ToNumber(right));
                }
                else
                {
                    AssertValidBigIntArithmeticOperands(context, left, right);
                    result = JsBigInt.Create(TypeConverter.ToBigInt(left) % TypeConverter.ToBigInt(right));
                }

                return NormalCompletion(result);
            }
        }

        private sealed class BitwiseBinaryExpression : JintBinaryExpression
        {
            private string OperatorClrName
            {
                get
                {
                    return _operator switch
                    {
                        BinaryOperator.BitwiseAnd => "op_BitwiseAnd",
                        BinaryOperator.BitwiseOr => "op_BitwiseOr",
                        BinaryOperator.BitwiseXOr => "op_ExclusiveOr",
                        BinaryOperator.LeftShift => "op_LeftShift",
                        BinaryOperator.RightShift => "op_RightShift",
                        BinaryOperator.UnsignedRightShift => "op_UnsignedRightShift",
                        _ => null
                    };
                }
            }

            private readonly BinaryOperator _operator;

            public BitwiseBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
                _operator = expression.Operator;
            }

            protected override ExpressionResult EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context).Value;
                var right = _right.GetValue(context).Value;

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, OperatorClrName, out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                if (AreIntegerOperands(left, right))
                {
                    int leftValue = left.AsInteger();
                    int rightValue = right.AsInteger();

                    JsValue result;
                    switch (_operator)
                    {
                        case BinaryOperator.BitwiseAnd:
                            result = JsNumber.Create(leftValue & rightValue);
                            break;
                        case BinaryOperator.BitwiseOr:
                            result = JsNumber.Create(leftValue | rightValue);
                            break;
                        case BinaryOperator.BitwiseXOr:
                            result = JsNumber.Create(leftValue ^ rightValue);
                            break;
                        case BinaryOperator.LeftShift:
                            result = JsNumber.Create(leftValue << (int) ((uint) rightValue & 0x1F));
                            break;
                        case BinaryOperator.RightShift:
                            result = JsNumber.Create(leftValue >> (int) ((uint) rightValue & 0x1F));
                            break;
                        case BinaryOperator.UnsignedRightShift:
                            result = JsNumber.Create((uint) leftValue >> (int) ((uint) rightValue & 0x1F));
                            break;
                        default:
                            ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(_operator), "unknown shift operator");
                            result = null;
                            break;
                    }

                    return NormalCompletion(result);
                }

                return NormalCompletion(EvaluateNonInteger(left, right));
            }

            private JsNumber EvaluateNonInteger(JsValue left, JsValue right)
            {
                switch (_operator)
                {
                    case BinaryOperator.BitwiseAnd:
                        return JsNumber.Create(TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right));

                    case BinaryOperator.BitwiseOr:
                        return JsNumber.Create(TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right));

                    case BinaryOperator.BitwiseXOr:
                        return JsNumber.Create(TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right));

                    case BinaryOperator.LeftShift:
                        return JsNumber.Create(TypeConverter.ToInt32(left) << (int) (TypeConverter.ToUint32(right) & 0x1F));

                    case BinaryOperator.RightShift:
                        return JsNumber.Create(TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));

                    case BinaryOperator.UnsignedRightShift:
                        return JsNumber.Create((uint) TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                    default:
                        ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(_operator), "unknown shift operator");
                        return null;
                }
            }
        }
    }
}