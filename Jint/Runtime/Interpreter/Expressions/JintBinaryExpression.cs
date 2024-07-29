using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions;

internal abstract class JintBinaryExpression : JintExpression
{
    private readonly record struct OperatorKey(string? OperatorName, Type Left, Type Right);
    private static readonly ConcurrentDictionary<OperatorKey, MethodDescriptor> _knownOperators = new();

    private JintExpression _left = null!;
    private JintExpression _right = null!;
    private bool _initialized;

    private JintBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
    {
        // TODO check https://tc39.es/ecma262/#sec-applystringornumericbinaryoperator
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        var expression = (NonLogicalBinaryExpression) _expression;
        _left = Build(expression.Left);
        _right = Build(expression.Right);
        _initialized = true;
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

                var methods = leftMethods.Concat(rightMethods).Where(x => string.Equals(x.Name, clrName, StringComparison.Ordinal) && x.GetParameters().Length == 2);
                var methodDescriptors = MethodDescriptor.Build(methods.ToArray());

                return InteropHelper.FindBestMatch(context.Engine, methodDescriptors, _ => arguments).FirstOrDefault().Method;
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

    internal static JintExpression Build(NonLogicalBinaryExpression expression)
    {
        JintBinaryExpression? result = null;
        switch (expression.Operator)
        {
            case Operator.StrictEquality:
                result = new StrictlyEqualBinaryExpression(expression);
                break;
            case Operator.StrictInequality:
                result = new StrictlyNotEqualBinaryExpression(expression);
                break;
            case Operator.LessThan:
                result = new LessBinaryExpression(expression);
                break;
            case Operator.GreaterThan:
                result = new GreaterBinaryExpression(expression);
                break;
            case Operator.Addition:
                result = new PlusBinaryExpression(expression);
                break;
            case Operator.Subtraction:
                result = new MinusBinaryExpression(expression);
                break;
            case Operator.Multiplication:
                result = new TimesBinaryExpression(expression);
                break;
            case Operator.Division:
                result = new DivideBinaryExpression(expression);
                break;
            case Operator.Equality:
                result = new EqualBinaryExpression(expression);
                break;
            case Operator.Inequality:
                result = new EqualBinaryExpression(expression, invert: true);
                break;
            case Operator.GreaterThanOrEqual:
                result = new CompareBinaryExpression(expression, leftFirst: true);
                break;
            case Operator.LessThanOrEqual:
                result = new CompareBinaryExpression(expression, leftFirst: false);
                break;
            case Operator.BitwiseAnd:
            case Operator.BitwiseOr:
            case Operator.BitwiseXor:
            case Operator.LeftShift:
            case Operator.RightShift:
            case Operator.UnsignedRightShift:
                result = new BitwiseBinaryExpression(expression);
                break;
            case Operator.InstanceOf:
                result = new InstanceOfBinaryExpression(expression);
                break;
            case Operator.Exponentiation:
                result = new ExponentiationBinaryExpression(expression);
                break;
            case Operator.Remainder:
                result = new ModuloBinaryExpression(expression);
                break;
            case Operator.In:
                result = new InBinaryExpression(expression);
                break;
            default:
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(expression.Operator), "cannot handle operator");
                break;
        }

        if (expression.Operator != Operator.InstanceOf
            && expression.Operator != Operator.In
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
        public StrictlyEqualBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

            var left = _left.GetValue(context);
            var right = _right.GetValue(context);
            var equal = left == right;
            return equal ? JsBoolean.True : JsBoolean.False;
        }
    }

    private sealed class StrictlyNotEqualBinaryExpression : JintBinaryExpression
    {
        public StrictlyNotEqualBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

            var left = _left.GetValue(context);
            var right = _right.GetValue(context);
            return left == right ? JsBoolean.False : JsBoolean.True;
        }
    }

    private sealed class LessBinaryExpression : JintBinaryExpression
    {
        public LessBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
        public GreaterBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
        public PlusBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
        public MinusBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
                JintBinaryExpression.AssertValidBigIntArithmeticOperands(left, right);
                number = JsBigInt.Create(TypeConverter.ToBigInt(left) - TypeConverter.ToBigInt(right));
            }

            return number;
        }
    }

    private sealed class TimesBinaryExpression : JintBinaryExpression
    {
        public TimesBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
        public DivideBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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

        public EqualBinaryExpression(NonLogicalBinaryExpression expression, bool invert = false) : base(expression)
        {
            _invert = invert;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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

        public CompareBinaryExpression(NonLogicalBinaryExpression expression, bool leftFirst) : base(expression)
        {
            _leftFirst = leftFirst;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
        public InstanceOfBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

            var leftValue = _left.GetValue(context);
            var rightValue = _right.GetValue(context);
            return leftValue.InstanceofOperator(rightValue) ? JsBoolean.True : JsBoolean.False;
        }
    }

    private sealed class ExponentiationBinaryExpression : JintBinaryExpression
    {
        public ExponentiationBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
        public InBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
        public ModuloBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
                    Operator.BitwiseAnd => "op_BitwiseAnd",
                    Operator.BitwiseOr => "op_BitwiseOr",
                    Operator.BitwiseXor => "op_ExclusiveOr",
                    Operator.LeftShift => "op_LeftShift",
                    Operator.RightShift => "op_RightShift",
                    Operator.UnsignedRightShift => "op_UnsignedRightShift",
                    _ => null
                };
            }
        }

        private readonly Operator _operator;

        public BitwiseBinaryExpression(NonLogicalBinaryExpression expression) : base(expression)
        {
            _operator = expression.Operator;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            EnsureInitialized();

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
                    case Operator.BitwiseAnd:
                        result = JsNumber.Create(leftValue & rightValue);
                        break;
                    case Operator.BitwiseOr:
                        result = JsNumber.Create(leftValue | rightValue);
                        break;
                    case Operator.BitwiseXor:
                        result = JsNumber.Create(leftValue ^ rightValue);
                        break;
                    case Operator.LeftShift:
                        result = JsNumber.Create(leftValue << (int) ((uint) rightValue & 0x1F));
                        break;
                    case Operator.RightShift:
                        result = JsNumber.Create(leftValue >> (int) ((uint) rightValue & 0x1F));
                        break;
                    case Operator.UnsignedRightShift:
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
                case Operator.BitwiseAnd:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right));
                        }

                        return JsBigInt.Create(TypeConverter.ToBigInt(left) & TypeConverter.ToBigInt(right));
                    }

                case Operator.BitwiseOr:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right));
                        }
                        return JsBigInt.Create(TypeConverter.ToBigInt(left) | TypeConverter.ToBigInt(right));
                    }

                case Operator.BitwiseXor:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right));
                        }
                        return JsBigInt.Create(TypeConverter.ToBigInt(left) ^ TypeConverter.ToBigInt(right));
                    }

                case Operator.LeftShift:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) << (int) (TypeConverter.ToUint32(right) & 0x1F));
                        }
                        return JsBigInt.Create(TypeConverter.ToBigInt(left) << (int) TypeConverter.ToBigInt(right));
                    }

                case Operator.RightShift:
                    {
                        if (!left.IsBigInt())
                        {
                            return JsNumber.Create(TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                        }
                        return JsBigInt.Create(TypeConverter.ToBigInt(left) >> (int) TypeConverter.ToBigInt(right));
                    }

                case Operator.UnsignedRightShift:
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
