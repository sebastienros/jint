using System;
using System.Collections.Concurrent;
using System.Linq;
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

        public static bool SameValueZero(JsValue x, JsValue y)
        {
            return x == y || (x is JsNumber xNum && y is JsNumber yNum && double.IsNaN(xNum._value) && double.IsNaN(yNum._value));
        }

        public static bool StrictlyEqual(JsValue x, JsValue y)
        {
            var typeX = x._type & ~InternalTypes.InternalFlags;
            var typeY = y._type & ~InternalTypes.InternalFlags;

            if (typeX != typeY)
            {
                if (typeX == InternalTypes.Integer)
                {
                    typeX = InternalTypes.Number;
                }

                if (typeY == InternalTypes.Integer)
                {
                    typeY = InternalTypes.Number;
                }

                if (typeX != typeY)
                {
                    return false;
                }
            }

            if (typeX == InternalTypes.Undefined || typeX == InternalTypes.Null)
            {
                return true;
            }

            if (typeX == InternalTypes.Integer)
            {
                return x.AsInteger() == y.AsInteger();
            }

            if (typeX == InternalTypes.Number)
            {
                var nx = ((JsNumber) x)._value;
                var ny = ((JsNumber) y)._value;
                return !double.IsNaN(nx) && !double.IsNaN(ny) && nx == ny;
            }

            if ((typeX & InternalTypes.String) != 0)
            {
                return x.ToString() == y.ToString();
            }

            if (typeX == InternalTypes.Boolean)
            {
                return ((JsBoolean) x)._value == ((JsBoolean) y)._value;
            }

            if ((typeX & InternalTypes.Object) != 0 && x.AsObject() is IObjectWrapper xw)
            {
                var yw = y.AsObject() as IObjectWrapper;
                if (yw == null)
                    return false;
                return Equals(xw.Target, yw.Target);
            }

            return x == y;
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
                var equal = StrictlyEqual(left, right);
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
                return NormalCompletion(StrictlyEqual(left, right) ? JsBoolean.False : JsBoolean.True);
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
                JsValue result = lprim.IsString() || rprim.IsString()
                    ? JsString.Create(TypeConverter.ToString(lprim) + TypeConverter.ToString(rprim))
                    : JsNumber.Create(TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim));

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

                var number = AreIntegerOperands(left, right)
                    ? JsNumber.Create(left.AsInteger() - right.AsInteger())
                    : JsNumber.Create(TypeConverter.ToNumber(left) - TypeConverter.ToNumber(right));

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

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Multiply", out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                if (AreIntegerOperands(left, right))
                {
                    return NormalCompletion(JsNumber.Create((long) left.AsInteger() * right.AsInteger()));
                }

                if (left.IsUndefined() || right.IsUndefined())
                {
                    return NormalCompletion(Undefined.Instance);
                }

                return NormalCompletion(JsNumber.Create(TypeConverter.ToNumber(left) * TypeConverter.ToNumber(right)));
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

                return NormalCompletion(Divide(left, right));
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

                return NormalCompletion(Equal(left, right) == !_invert ? JsBoolean.True : JsBoolean.False);
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

                return NormalCompletion(JsNumber.Create(Math.Pow(TypeConverter.ToNumber(left), TypeConverter.ToNumber(right))));
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

                if (context.OperatorOverloadingAllowed
                    && TryOperatorOverloading(context, left, right, "op_Modulus", out var opResult))
                {
                    return NormalCompletion(JsValue.FromObject(context.Engine, opResult));
                }

                if (AreIntegerOperands(left, right))
                {
                    var leftInteger = left.AsInteger();
                    var rightInteger = right.AsInteger();
                    if (leftInteger > 0 && rightInteger != 0)
                    {
                        return NormalCompletion(JsNumber.Create(leftInteger % rightInteger));
                    }
                }

                if (left.IsUndefined() || right.IsUndefined())
                {
                    return NormalCompletion(Undefined.Instance);
                }

                return NormalCompletion(JsNumber.Create(TypeConverter.ToNumber(left) % TypeConverter.ToNumber(right)));
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