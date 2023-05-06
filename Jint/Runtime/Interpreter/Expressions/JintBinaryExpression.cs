using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract class JintBinaryExpression : JintExpression
    {
        private readonly record struct OperatorKey(string? OperatorName, Type Left, Type Right);
        private static readonly ConcurrentDictionary<OperatorKey, MethodDescriptor> _knownOperators = new();

        private JintExpression _left = null!;
        private JintExpression _right = null!;

        private JintBinaryExpression(BinaryExpression expression) : base(expression)
        {
            // TODO check https://tc39.es/ecma262/#sec-applystringornumericbinaryoperator
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var expression = (BinaryExpression) _expression;
            _left = Build(expression.Left);
            _right = Build(expression.Right);
        }

        internal static bool TryOperatorOverloading(
            EvaluationContext context,
            JsValue leftValue,
            JsValue rightValue,
            string? clrName,
            [NotNullWhen(true)] out object? result)
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
                    var methodDescriptors = MethodDescriptor.Build(methods.ToArray());

                    return TypeConverter.FindBestMatch(context.Engine, methodDescriptors, _ => arguments).FirstOrDefault().Method;
                });

                if (method != null)
                {
                    try
                    {
                        result = method.Call(context.Engine, null, arguments);
                        return true;
                    }
                    catch (Exception e)
                    {
                        ExceptionHelper.ThrowMeaningfulException(context.Engine, new TargetInvocationException(e.InnerException));
                        result = null;
                        return false;
                    }
                }
            }

            result = null;
            return false;
        }

        internal static JintExpression Build(BinaryExpression expression)
        {
            JintBinaryExpression? result = null;
            switch (expression.Operator)
            {
                case BinaryOperator.StrictlyEqual:
                    result = new StrictlyEqualBinaryExpression(expression);
                    break;
                case BinaryOperator.StrictlyNotEqual:
                    result = new StrictlyNotEqualBinaryExpression(expression);
                    break;
                case BinaryOperator.Less:
                    result = new LessBinaryExpression(expression);
                    break;
                case BinaryOperator.Greater:
                    result = new GreaterBinaryExpression(expression);
                    break;
                case BinaryOperator.Plus:
                    result = new PlusBinaryExpression(expression);
                    break;
                case BinaryOperator.Minus:
                    result = new MinusBinaryExpression(expression);
                    break;
                case BinaryOperator.Times:
                    result = new TimesBinaryExpression(expression);
                    break;
                case BinaryOperator.Divide:
                    result = new DivideBinaryExpression(expression);
                    break;
                case BinaryOperator.Equal:
                    result = new EqualBinaryExpression(expression);
                    break;
                case BinaryOperator.NotEqual:
                    result = new EqualBinaryExpression(expression, invert: true);
                    break;
                case BinaryOperator.GreaterOrEqual:
                    result = new CompareBinaryExpression(expression, leftFirst: true);
                    break;
                case BinaryOperator.LessOrEqual:
                    result = new CompareBinaryExpression(expression, leftFirst: false);
                    break;
                case BinaryOperator.BitwiseAnd:
                case BinaryOperator.BitwiseOr:
                case BinaryOperator.BitwiseXor:
                case BinaryOperator.LeftShift:
                case BinaryOperator.RightShift:
                case BinaryOperator.UnsignedRightShift:
                    result = new BitwiseBinaryExpression(expression);
                    break;
                case BinaryOperator.InstanceOf:
                    result = new InstanceOfBinaryExpression(expression);
                    break;
                case BinaryOperator.Exponentiation:
                    result = new ExponentiationBinaryExpression(expression);
                    break;
                case BinaryOperator.Modulo:
                    result = new ModuloBinaryExpression(expression);
                    break;
                case BinaryOperator.In:
                    result = new InBinaryExpression(expression);
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
                    try
                    {
                        var context = new EvaluationContext();
                        return new JintConstantExpression(expression, (JsValue) result.EvaluateWithoutNodeTracking(context));
                    }
                    catch
                    {
                        // probably caused an error and error reporting doesn't work without engine
                    }
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AreNonBigIntOperands(JsValue left, JsValue right)
        {
            return left._type != InternalTypes.BigInt && right._type != InternalTypes.BigInt;
        }

        internal static void AssertValidBigIntArithmeticOperands(JsValue left, JsValue right)
        {
            if (left.Type != right.Type)
            {
                ExceptionHelper.ThrowTypeErrorNoEngine("Cannot mix BigInt and other types, use explicit conversions");
            }
        }

        private sealed class StrictlyEqualBinaryExpression : JintBinaryExpression
        {
            public StrictlyEqualBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);
                var equal = left == right;
                return equal ? JsBoolean.True : JsBoolean.False;
            }
        }

        private sealed class StrictlyNotEqualBinaryExpression : JintBinaryExpression
        {
            public StrictlyNotEqualBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);
                return left == right ? JsBoolean.False : JsBoolean.True;
            }
        }

        private sealed class LessBinaryExpression : JintBinaryExpression
        {
            public LessBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_LessThan", out var opResult))
                {
                    return JsValue.FromObject(context.Engine, opResult);
                }

                var value = Compare(left, right);

                return value._type == InternalTypes.Undefined ? JsBoolean.False : value;
            }
        }

        private sealed class GreaterBinaryExpression : JintBinaryExpression
        {
            public GreaterBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_GreaterThan", out var opResult))
                {
                    return JsValue.FromObject(context.Engine, opResult);
                }

                var value = Compare(right, left, false);

                return value._type == InternalTypes.Undefined ? JsBoolean.False : value;
            }
        }

        private sealed class PlusBinaryExpression : JintBinaryExpression
        {
            public PlusBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Addition", out var opResult))
                {
                    return JsValue.FromObject(context.Engine, opResult);
                }

                if (AreIntegerOperands(left, right))
                {
                    return JsNumber.Create((long)left.AsInteger() + right.AsInteger());
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
                    AssertValidBigIntArithmeticOperands(lprim, rprim);
                    result = JsBigInt.Create(TypeConverter.ToBigInt(lprim) + TypeConverter.ToBigInt(rprim));
                }

                return result;
            }
        }

        private sealed class MinusBinaryExpression : JintBinaryExpression
        {
            public MinusBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Subtraction", out var opResult))
                {
                    return JsValue.FromObject(context.Engine, opResult);
                }

                JsValue number;
                left = TypeConverter.ToNumeric(left);
                right = TypeConverter.ToNumeric(right);

                if (AreIntegerOperands(left, right))
                {
                    number = JsNumber.Create((long)left.AsInteger() - right.AsInteger());
                }
                else if (AreNonBigIntOperands(left, right))
                {
                    number = JsNumber.Create(left.AsNumber() - right.AsNumber());
                }
                else
                {
                    number = JsBigInt.Create(TypeConverter.ToBigInt(left) - TypeConverter.ToBigInt(right));
                }

                return number;
            }
        }

        private sealed class TimesBinaryExpression : JintBinaryExpression
        {
            public TimesBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);

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
                else
                {
                    var leftNumeric = TypeConverter.ToNumeric(left);
                    var rightNumeric = TypeConverter.ToNumeric(right);

                    if (leftNumeric.IsNumber() && rightNumeric.IsNumber())
                    {
                        result = JsNumber.Create(leftNumeric.AsNumber() * rightNumeric.AsNumber());
                    }
                    else
                    {
                        AssertValidBigIntArithmeticOperands(leftNumeric, rightNumeric);
                        result = JsBigInt.Create(leftNumeric.AsBigInt() * rightNumeric.AsBigInt());
                    }
                }

                return result;
            }
        }

        private sealed class DivideBinaryExpression : JintBinaryExpression
        {
            public DivideBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Division", out var opResult))
                {
                    return JsValue.FromObject(context.Engine, opResult);
                }

                left = TypeConverter.ToNumeric(left);
                right = TypeConverter.ToNumeric(right);
                return Divide(context, left, right);
            }
        }

        private sealed class EqualBinaryExpression : JintBinaryExpression
        {
            private readonly bool _invert;

            public EqualBinaryExpression(BinaryExpression expression, bool invert = false) : base(expression)
            {
                _invert = invert;
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, _invert ? "op_Inequality" : "op_Equality", out var opResult))
                {
                    return JsValue.FromObject(context.Engine, opResult);
                }

                // if types match, we can take faster strict equality
                var equality = left.Type == right.Type
                    ? left.Equals(right)
                    : left.IsLooselyEqual(right);

                return equality == !_invert ? JsBoolean.True : JsBoolean.False;
            }
        }

        private sealed class CompareBinaryExpression : JintBinaryExpression
        {
            private readonly bool _leftFirst;

            public CompareBinaryExpression(BinaryExpression expression, bool leftFirst) : base(expression)
            {
                _leftFirst = leftFirst;
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var leftValue = _left.GetValue(context);
                var rightValue = _right.GetValue(context);

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, leftValue, rightValue, _leftFirst ? "op_GreaterThanOrEqual" : "op_LessThanOrEqual", out var opResult))
                {
                    return JsValue.FromObject(context.Engine, opResult);
                }

                var left = _leftFirst ? leftValue : rightValue;
                var right = _leftFirst ? rightValue : leftValue;

                var value = Compare(left, right, _leftFirst);
                return value.IsUndefined() || ((JsBoolean) value)._value ? JsBoolean.False : JsBoolean.True;
            }
        }

        private sealed class InstanceOfBinaryExpression : JintBinaryExpression
        {
            public InstanceOfBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var leftValue = _left.GetValue(context);
                var rightValue = _right.GetValue(context);
                return leftValue.InstanceofOperator(rightValue) ? JsBoolean.True : JsBoolean.False;
            }
        }

        private sealed class ExponentiationBinaryExpression : JintBinaryExpression
        {
            public ExponentiationBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var leftReference = _left.GetValue(context);
                var rightReference = _right.GetValue(context);

                var left = TypeConverter.ToNumeric(leftReference);
                var right = TypeConverter.ToNumeric(rightReference);

                JsValue result;
                if (AreNonBigIntOperands(left,right))
                {
                    // validation
                    var baseNumber = (JsNumber) left;
                    var exponentNumber = (JsNumber) right;

                    if (exponentNumber.IsNaN())
                    {
                        return JsNumber.DoubleNaN;
                    }

                    if (exponentNumber.IsZero())
                    {
                        return JsNumber.PositiveOne;
                    }

                    if (baseNumber.IsNaN())
                    {
                        return JsNumber.DoubleNaN;
                    }

                    var exponentValue = exponentNumber._value;
                    if (baseNumber.IsPositiveInfinity())
                    {
                        return exponentValue > 0 ? JsNumber.DoublePositiveInfinity : JsNumber.PositiveZero;
                    }

                    static bool IsOddIntegral(double value) => TypeConverter.IsIntegralNumber(value) && value % 2 != 0;

                    if (baseNumber.IsNegativeInfinity())
                    {
                        if (exponentValue > 0)
                        {
                            return IsOddIntegral(exponentValue) ? JsNumber.DoubleNegativeInfinity : JsNumber.DoublePositiveInfinity;
                        }

                        return IsOddIntegral(exponentValue) ? JsNumber.NegativeZero : JsNumber.PositiveZero;
                    }

                    if (baseNumber.IsPositiveZero())
                    {
                        return exponentValue > 0 ? JsNumber.PositiveZero : JsNumber.DoublePositiveInfinity;
                    }

                    if (baseNumber.IsNegativeZero())
                    {
                        if (exponentValue > 0)
                        {
                            return IsOddIntegral(exponentValue) ? JsNumber.NegativeZero : JsNumber.PositiveZero;
                        }
                        return IsOddIntegral(exponentValue) ? JsNumber.DoubleNegativeInfinity : JsNumber.DoublePositiveInfinity;
                    }

                    var baseValue = baseNumber._value;
                    if (exponentNumber.IsPositiveInfinity())
                    {
                        if (Math.Abs(baseValue) > 1)
                        {
                            return JsNumber.DoublePositiveInfinity;
                        }
                        if (Math.Abs(baseValue) == 1)
                        {
                            return JsNumber.DoubleNaN;
                        }

                        return JsNumber.PositiveZero;
                    }

                    if (exponentNumber.IsNegativeInfinity())
                    {
                        if (Math.Abs(baseValue) > 1)
                        {
                            return JsNumber.PositiveZero;
                        }
                        if (Math.Abs(baseValue) == 1)
                        {
                            return JsNumber.DoubleNaN;
                        }

                        return JsNumber.DoublePositiveInfinity;
                    }

                    if (baseValue < 0 && !TypeConverter.IsIntegralNumber(exponentValue))
                    {
                        return JsNumber.DoubleNaN;
                    }

                    result = JsNumber.Create(Math.Pow(baseNumber._value, exponentValue));
                }
                else
                {
                    AssertValidBigIntArithmeticOperands(left, right);

                    var exponent = right.AsBigInt();
                    if (exponent < 0)
                    {
                        ExceptionHelper.ThrowRangeError(context.Engine.Realm, "Exponent must be positive");
                    }

                    if (exponent > int.MaxValue || exponent < int.MinValue)
                    {
                        ExceptionHelper.ThrowTypeError(context.Engine.Realm, "Exponent does not fit 32bit range");
                    }
                    result = JsBigInt.Create(BigInteger.Pow(left.AsBigInt(), (int) exponent));
                }

                return result;
            }
        }

        private sealed class InBinaryExpression : JintBinaryExpression
        {
            public InBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);

                var oi = right as ObjectInstance;
                if (oi is null)
                {
                    ExceptionHelper.ThrowTypeError(context.Engine.Realm, "in can only be used with an object");
                }

                if (left.IsPrivateName())
                {
                    var privateEnv = context.Engine.ExecutionContext.PrivateEnvironment!;
                    var privateName = privateEnv.ResolvePrivateIdentifier(((PrivateName) left).ToString());
                    return privateName is not null && oi.PrivateElementFind(privateName) is not null ? JsBoolean.True : JsBoolean.False;
                }

                return oi.HasProperty(left) ? JsBoolean.True : JsBoolean.False;
            }
        }

        private sealed class ModuloBinaryExpression : JintBinaryExpression
        {
            public ModuloBinaryExpression(BinaryExpression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var left = _left.GetValue(context);
                var right = _right.GetValue(context);

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Modulus", out var opResult))
                {
                    return JsValue.FromObject(context.Engine, opResult);
                }

                var result = JsValue.Undefined;
                left = TypeConverter.ToNumeric(left);
                right = TypeConverter.ToNumeric(right);

                if (AreIntegerOperands(left, right))
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
                else if (AreNonBigIntOperands(left, right))
                {
                    var n = left.AsNumber();
                    var d = right.AsNumber();

                    if (double.IsNaN(n) || double.IsNaN(d) || double.IsInfinity(n))
                    {
                        result = JsNumber.DoubleNaN;
                    }
                    else if (double.IsInfinity(d))
                    {
                        result = n;
                    }
                    else if (NumberInstance.IsPositiveZero(d) || NumberInstance.IsNegativeZero(d))
                    {
                        result = JsNumber.DoubleNaN;
                    }
                    else if (NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
                    {
                        result = n;
                    }
                    else
                    {
                        result = JsNumber.Create(n % d);
                    }
                }
                else
                {
                    AssertValidBigIntArithmeticOperands(left, right);

                    var n = TypeConverter.ToBigInt(left);
                    var d = TypeConverter.ToBigInt(right);

                    if (d == 0)
                    {
                        ExceptionHelper.ThrowRangeError(context.Engine.Realm, "Division by zero");
                    }
                    else if (n == 0)
                    {
                        result = JsBigInt.Zero;
                    }
                    else
                    {
                        result = JsBigInt.Create(n % d);
                    }
                }

                return result;
            }
        }

        private sealed class BitwiseBinaryExpression : JintBinaryExpression
        {
            private string? OperatorClrName
            {
                get
                {
                    return _operator switch
                    {
                        BinaryOperator.BitwiseAnd => "op_BitwiseAnd",
                        BinaryOperator.BitwiseOr => "op_BitwiseOr",
                        BinaryOperator.BitwiseXor => "op_ExclusiveOr",
                        BinaryOperator.LeftShift => "op_LeftShift",
                        BinaryOperator.RightShift => "op_RightShift",
                        BinaryOperator.UnsignedRightShift => "op_UnsignedRightShift",
                        _ => null
                    };
                }
            }

            private readonly BinaryOperator _operator;

            public BitwiseBinaryExpression(BinaryExpression expression) : base(expression)
            {
                _operator = expression.Operator;
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                var lval = _left.GetValue(context);
                var rval = _right.GetValue(context);

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, lval, rval, OperatorClrName, out var opResult))
                {
                    return JsValue.FromObject(context.Engine, opResult);
                }

                var lnum = TypeConverter.ToNumeric(lval);
                var rnum = TypeConverter.ToNumeric(rval);

                if (lnum.Type != rnum.Type)
                {
                    ExceptionHelper.ThrowTypeErrorNoEngine("Cannot mix BigInt and other types, use explicit conversions", _left._expression);
                }

                if (AreIntegerOperands(lnum, rnum))
                {
                    int leftValue = lnum.AsInteger();
                    int rightValue = rnum.AsInteger();

                    JsValue? result = null;
                    switch (_operator)
                    {
                        case BinaryOperator.BitwiseAnd:
                            result = JsNumber.Create(leftValue & rightValue);
                            break;
                        case BinaryOperator.BitwiseOr:
                            result = JsNumber.Create(leftValue | rightValue);
                            break;
                        case BinaryOperator.BitwiseXor:
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
                            break;
                    }

                    return result;
                }

                return EvaluateNonInteger(lnum, rnum);
            }

            private JsValue EvaluateNonInteger(JsValue left, JsValue right)
            {
                switch (_operator)
                {
                    case BinaryOperator.BitwiseAnd:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right));
                        }

                        return JsBigInt.Create(TypeConverter.ToBigInt(left) & TypeConverter.ToBigInt(right));
                    }

                    case BinaryOperator.BitwiseOr:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right));
                        }
                        return JsBigInt.Create(TypeConverter.ToBigInt(left) | TypeConverter.ToBigInt(right));
                    }

                    case BinaryOperator.BitwiseXor:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right));
                        }
                        return JsBigInt.Create(TypeConverter.ToBigInt(left) ^ TypeConverter.ToBigInt(right));
                    }

                    case BinaryOperator.LeftShift:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) << (int) (TypeConverter.ToUint32(right) & 0x1F));
                        }
                        return JsBigInt.Create(TypeConverter.ToBigInt(left) << (int) TypeConverter.ToBigInt(right));
                    }

                    case BinaryOperator.RightShift:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                        }
                        return JsBigInt.Create(TypeConverter.ToBigInt(left) >> (int) TypeConverter.ToBigInt(right));
                    }

                    case BinaryOperator.UnsignedRightShift:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create((uint) TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                        }
                        ExceptionHelper.ThrowTypeErrorNoEngine("Cannot mix BigInt and other types, use explicit conversions", _left._expression);
                        return null;
                    }

                    default:
                    {
                        ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(_operator), "unknown shift operator");
                        return null;
                    }
                }
            }
        }
    }
}
