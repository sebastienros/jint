using System;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract class JintBinaryExpression : JintExpression
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;
        private readonly BinaryOperator _operatorType;

        private JintBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
        {
            _left = Build(engine, expression.Left);
            _right = Build(engine, expression.Right);
            _operatorType = expression.Operator;
        }

        internal static JintExpression Build(Engine engine, BinaryExpression expression)
        {
            JintBinaryExpression result;
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
                    result = ExceptionHelper.ThrowArgumentOutOfRangeException<JintBinaryExpression>(nameof(_operatorType), "cannot handle operator");
                    break;
            }

            if (expression.Operator != BinaryOperator.InstanceOf
                && expression.Operator != BinaryOperator.In
                && expression.Left is Literal leftLiteral
                && expression.Right is Literal rightLiteral)
            {
                var lval = JintLiteralExpression.ConvertToJsValue(leftLiteral);
                var rval = JintLiteralExpression.ConvertToJsValue(rightLiteral);

                if (!(lval is null) && !(rval is null))
                {
                    // we have fixed result
                    return new JintConstantExpression(engine, expression, result.GetValue());
                }
            }

            return result;
        }

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            // we always create a JsValue
            return (JsValue) EvaluateInternal();
        }

        public static bool StrictlyEqual(JsValue x, JsValue y)
        {
            var typeX = x._type;
            var typeY = y._type;

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

            switch (typeX)
            {
                case InternalTypes.Undefined:
                case InternalTypes.Null:
                    return true;
                case InternalTypes.Integer:
                    return x.AsInteger() == y.AsInteger();
                case InternalTypes.Number:
                    var nx = ((JsNumber) x)._value;
                    var ny = ((JsNumber) y)._value;
                    return !double.IsNaN(nx) && !double.IsNaN(ny) && nx == ny;
                case InternalTypes.String:
                    return x.AsStringWithoutTypeCheck() == y.AsStringWithoutTypeCheck();
                case InternalTypes.Boolean:
                    return ((JsBoolean) x)._value == ((JsBoolean) y)._value;
                case InternalTypes.Object when x.AsObject() is IObjectWrapper xw:
                    var yw = y.AsObject() as IObjectWrapper;
                    if (yw == null)
                        return false;
                    return Equals(xw.Target, yw.Target);
                case InternalTypes.None:
                    return true;
                default:
                    return x == y;
            }
        }

        private sealed class StrictlyEqualBinaryExpression : JintBinaryExpression
        {
            public StrictlyEqualBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                return StrictlyEqual(left, right)
                    ? JsBoolean.True
                    : JsBoolean.False;
            }
        }

        private sealed class StrictlyNotEqualBinaryExpression : JintBinaryExpression
        {
            public StrictlyNotEqualBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                return StrictlyEqual(left, right)
                    ? JsBoolean.False
                    : JsBoolean.True;
            }
        }

        private sealed class LessBinaryExpression : JintBinaryExpression
        {
            public LessBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                var value = Compare(left, right);

                return value._type == InternalTypes.Undefined
                    ? JsBoolean.False
                    : value;
            }
        }

        private sealed class GreaterBinaryExpression : JintBinaryExpression
        {
            public GreaterBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                var value = Compare(right, left, false);

                return value._type == InternalTypes.Undefined
                    ? JsBoolean.False
                    : value;
            }
        }
        
        private sealed class PlusBinaryExpression : JintBinaryExpression
        {
            public PlusBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                
                if (AreIntegerOperands(left, right))
                {
                    return JsNumber.Create(left.AsInteger() + right.AsInteger());
                }

                var lprim = TypeConverter.ToPrimitive(left);
                var rprim = TypeConverter.ToPrimitive(right);
                return lprim.IsString() || rprim.IsString()
                    ? (JsValue) JsString.Create(TypeConverter.ToString(lprim) + TypeConverter.ToString(rprim))
                    : JsNumber.Create(TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim));
            }
        }
        private sealed class MinusBinaryExpression : JintBinaryExpression
        {
            public MinusBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                
                return AreIntegerOperands(left, right)
                    ? JsNumber.Create(left.AsInteger() - right.AsInteger())
                    : JsNumber.Create(TypeConverter.ToNumber(left) - TypeConverter.ToNumber(right));
            }
        }

        private sealed class TimesBinaryExpression : JintBinaryExpression
        {
            public TimesBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                
                if (AreIntegerOperands(left, right))
                {
                    return JsNumber.Create((long) left.AsInteger() * right.AsInteger());
                }

                if (left.IsUndefined() || right.IsUndefined())
                {
                    return Undefined.Instance;
                }

                return JsNumber.Create(TypeConverter.ToNumber(left) * TypeConverter.ToNumber(right));
            }
        }

        private sealed class DivideBinaryExpression : JintBinaryExpression
        {
            public DivideBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();

                return Divide(left, right);
            }
        }

        private sealed class EqualBinaryExpression : JintBinaryExpression
        {
            private readonly bool _invert;

            public EqualBinaryExpression(Engine engine, BinaryExpression expression, bool invert = false) : base(engine, expression)
            {
                _invert = invert;
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();

                return Equal(left, right) == !_invert
                    ? JsBoolean.True
                    : JsBoolean.False;
            }
        }

        private sealed class CompareBinaryExpression : JintBinaryExpression
        {
            private readonly bool _leftFirst;

            public CompareBinaryExpression(Engine engine, BinaryExpression expression, bool leftFirst) : base(engine, expression)
            {
                _leftFirst = leftFirst;
            }

            protected override object EvaluateInternal()
            {
                var leftValue = _left.GetValue();
                var rightValue = _right.GetValue();
                
                var left = _leftFirst ? leftValue : rightValue;
                var right = _leftFirst ? rightValue : leftValue;

                var value = Compare(left, right, _leftFirst);
                return value.IsUndefined() || ((JsBoolean) value)._value
                    ? JsBoolean.False
                    : JsBoolean.True;
            }
        }

        private sealed class InstanceOfBinaryExpression : JintBinaryExpression
        {
            public InstanceOfBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();

                if (!(right is FunctionInstance f))
                {
                    return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "instanceof can only be used with a function object");
                }

                return f.HasInstance(left) ? JsBoolean.True : JsBoolean.False;
            }
        }

        private sealed class ExponentiationBinaryExpression : JintBinaryExpression
        {
            public ExponentiationBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();

                return JsNumber.Create(Math.Pow(TypeConverter.ToNumber(left), TypeConverter.ToNumber(right)));
            }
        }
        private sealed class InBinaryExpression : JintBinaryExpression
        {
            public InBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();

                if (!(right is ObjectInstance oi))
                {
                    return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "in can only be used with an object");
                }

                return oi.HasProperty(TypeConverter.ToString(left)) ? JsBoolean.True : JsBoolean.False;
            }
        }

        private sealed class ModuloBinaryExpression : JintBinaryExpression
        {
            public ModuloBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();

                if (AreIntegerOperands(left, right))
                {
                    var leftInteger = left.AsInteger();
                    var rightInteger = right.AsInteger();
                    if (leftInteger > 0 && rightInteger != 0)
                    {
                        return JsNumber.Create(leftInteger % rightInteger);
                    }
                }

                if (left.IsUndefined() || right.IsUndefined())
                {
                    return Undefined.Instance;
                }

                return JsNumber.Create(TypeConverter.ToNumber(left) % TypeConverter.ToNumber(right));            }
        }

        private sealed class BitwiseBinaryExpression : JintBinaryExpression
        {
            private readonly BinaryOperator _operator;

            public BitwiseBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
                _operator = expression.Operator;
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();

                if (AreIntegerOperands(left, right))
                {
                    int leftValue = left.AsInteger();
                    int rightValue = right.AsInteger();
                    
                    switch (_operator)
                    {
                        case BinaryOperator.BitwiseAnd:
                            return JsNumber.Create(leftValue & rightValue);

                        case BinaryOperator.BitwiseOr:
                            return
                                JsNumber.Create(leftValue | rightValue);

                        case BinaryOperator.BitwiseXOr:
                            return
                                JsNumber.Create(leftValue ^ rightValue);

                        case BinaryOperator.LeftShift:
                            return JsNumber.Create(leftValue << (int) ((uint) rightValue & 0x1F));

                        case BinaryOperator.RightShift:
                            return JsNumber.Create(leftValue >> (int) ((uint) rightValue & 0x1F));

                        case BinaryOperator.UnsignedRightShift:
                            return JsNumber.Create((uint) leftValue >> (int) ((uint) rightValue & 0x1F));
                        default:
                            return ExceptionHelper.ThrowArgumentOutOfRangeException<object>(nameof(_operator),
                                "unknown shift operator");
                    }
  
                }
                
                return EvaluateNonInteger(left, right);
            }

            private object EvaluateNonInteger(JsValue left, JsValue right)
            {
                switch (_operator)
                {
                    case BinaryOperator.BitwiseAnd:
                        return JsNumber.Create(TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right));

                    case BinaryOperator.BitwiseOr:
                        return
                            JsNumber.Create(TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right));

                    case BinaryOperator.BitwiseXOr:
                        return
                            JsNumber.Create(TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right));

                    case BinaryOperator.LeftShift:
                        return JsNumber.Create(TypeConverter.ToInt32(left) <<
                                               (int) (TypeConverter.ToUint32(right) & 0x1F));

                    case BinaryOperator.RightShift:
                        return JsNumber.Create(TypeConverter.ToInt32(left) >>
                                               (int) (TypeConverter.ToUint32(right) & 0x1F));

                    case BinaryOperator.UnsignedRightShift:
                        return JsNumber.Create((uint) TypeConverter.ToInt32(left) >>
                                               (int) (TypeConverter.ToUint32(right) & 0x1F));
                    default:
                        return ExceptionHelper.ThrowArgumentOutOfRangeException<object>(nameof(_operator),
                            "unknown shift operator");
                }
            }
        }
    }
}