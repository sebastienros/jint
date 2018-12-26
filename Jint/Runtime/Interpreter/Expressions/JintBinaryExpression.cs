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
            JintExpression result;
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
                default:
                    result = new JintGenericBinaryExpression(engine, expression);
                    break;
            }

            if (expression.Left.Type == Nodes.Literal
                && expression.Right.Type == Nodes.Literal
                && expression.Operator != BinaryOperator.InstanceOf
                && expression.Operator != BinaryOperator.In)
            {
                // calculate eagerly
                // TODO result = new JintConstantExpression(engine, (JsValue) result.Evaluate());
            }

            return result;
        }

        public static bool StrictlyEqual(JsValue x, JsValue y)
        {
            var typeX = x.Type;
            var typeY = y.Type;

            if (typeX != typeY)
            {
                return false;
            }

            switch (typeX)
            {
                case Types.Undefined:
                case Types.Null:
                    return true;
                case Types.Number:
                    var nx = ((JsNumber) x)._value;
                    var ny = ((JsNumber) y)._value;
                    return !double.IsNaN(nx) && !double.IsNaN(ny) && nx == ny;
                case Types.String:
                    return x.AsStringWithoutTypeCheck() == y.AsStringWithoutTypeCheck();
                case Types.Boolean:
                    return ((JsBoolean) x)._value == ((JsBoolean) y)._value;
                case Types.Object when x.AsObject() is IObjectWrapper xw:
                    var yw = y.AsObject() as IObjectWrapper;
                    if (yw == null)
                        return false;
                    return Equals(xw.Target, yw.Target);
                case Types.None:
                    return true;
                default:
                    return x == y;
            }
        }

        private sealed class JintGenericBinaryExpression : JintBinaryExpression
        {
            private readonly Func<JsValue, JsValue, JsValue> _operator;

            public JintGenericBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
                switch (_operatorType)
                {
                    case BinaryOperator.Plus:
                        _operator = (left, right) =>
                        {
                            var lprim = TypeConverter.ToPrimitive(left);
                            var rprim = TypeConverter.ToPrimitive(right);
                            return lprim.IsString() || rprim.IsString()
                                ? (JsValue) JsString.Create(TypeConverter.ToString(lprim) + TypeConverter.ToString(rprim))
                                : JsNumber.Create(TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim));
                        };
                        break;

                    case BinaryOperator.Minus:
                        _operator = (left, right) => JsNumber.Create(TypeConverter.ToNumber(left) - TypeConverter.ToNumber(right));
                        break;

                    case BinaryOperator.Times:
                        _operator = (left, right) => left.IsUndefined() || right.IsUndefined()
                            ? Undefined.Instance
                            : JsNumber.Create(TypeConverter.ToNumber(left) * TypeConverter.ToNumber(right));
                        break;

                    case BinaryOperator.Divide:
                        _operator = Divide;
                        break;

                    case BinaryOperator.Modulo:
                        _operator = (left, right) => left.IsUndefined() || right.IsUndefined()
                            ? Undefined.Instance
                            : TypeConverter.ToNumber(left) % TypeConverter.ToNumber(right);

                        break;

                    case BinaryOperator.Equal:
                        _operator = (left, right) => Equal(left, right)
                            ? JsBoolean.True
                            : JsBoolean.False;
                        break;

                    case BinaryOperator.NotEqual:
                        _operator = (left, right) => Equal(left, right) ? JsBoolean.False : JsBoolean.True;
                        break;

                    case BinaryOperator.GreaterOrEqual:
                        _operator = (left, right) =>
                        {
                            var value = Compare(left, right);
                            if (value.IsUndefined() || ((JsBoolean) value)._value)
                            {
                                value = JsBoolean.False;
                            }
                            else
                            {
                                value = JsBoolean.True;
                            }

                            return value;
                        };

                        break;

                    case BinaryOperator.LessOrEqual:
                        _operator = (left, right) =>
                        {
                            var value = Compare(right, left, false);
                            if (value.IsUndefined() || ((JsBoolean) value)._value)
                            {
                                value = JsBoolean.False;
                            }
                            else
                            {
                                value = JsBoolean.True;
                            }

                            return value;
                        };

                        break;

                    case BinaryOperator.BitwiseAnd:
                        _operator = (left, right) => JsNumber.Create(TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right));
                        break;

                    case BinaryOperator.BitwiseOr:
                        _operator = (left, right) => JsNumber.Create(TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right));
                        break;

                    case BinaryOperator.BitwiseXOr:
                        _operator = (left, right) => JsNumber.Create(TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right));
                        break;

                    case BinaryOperator.LeftShift:
                        _operator = (left, right) => JsNumber.Create(TypeConverter.ToInt32(left) << (int) (TypeConverter.ToUint32(right) & 0x1F));
                        break;

                    case BinaryOperator.RightShift:
                        _operator = (left, right) => JsNumber.Create(TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                        break;

                    case BinaryOperator.UnsignedRightShift:
                        _operator = (left, right) => JsNumber.Create((uint) TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                        break;

                    case BinaryOperator.Exponentiation:
                        _operator = (left, right) => JsNumber.Create(Math.Pow(TypeConverter.ToNumber(left), TypeConverter.ToNumber(right)));
                        break;

                    case BinaryOperator.InstanceOf:
                        _operator = (left, right) =>
                        {
                            if (!(right is FunctionInstance f))
                            {
                                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "instanceof can only be used with a function object");
                            }

                            return f.HasInstance(left) ? JsBoolean.True : JsBoolean.False;
                        };
                        break;

                    case BinaryOperator.In:
                        _operator = (left, right) =>
                        {
                            if (!(right is ObjectInstance oi))
                            {
                                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "in can only be used with an object");
                            }

                            return oi.HasProperty(TypeConverter.ToString(left)) ? JsBoolean.True : JsBoolean.False;
                        };

                        break;

                    default:
                        _operator = ExceptionHelper.ThrowNotImplementedException<Func<JsValue, JsValue, JsValue>>();
                        break;
                }
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                return _operator(left, right);
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
                return StrictlyEqual(left, right) ? JsBoolean.True : JsBoolean.False;
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
                return StrictlyEqual(left, right) ? JsBoolean.False : JsBoolean.True;
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
                if (value._type == Types.Undefined)
                {
                    value = JsBoolean.False;
                }

                return value;
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
                if (value._type == Types.Undefined)
                {
                    value = JsBoolean.False;
                }

                return value;
            }
        }
    }
}