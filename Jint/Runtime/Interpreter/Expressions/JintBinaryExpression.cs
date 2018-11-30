using System;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract class JintBinaryExpression : JintExpression<BinaryExpression>
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;

        private JsValue _leftLiteral;
        private JsValue _rightLiteral;

        private JintBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
        {
            _left = Build(engine, _expression.Left);
            _right = Build(engine, _expression.Right);
        }

        protected override void Initialize()
        {
            if (_expression.Left.Type == Nodes.Literal)
            {
                _leftLiteral = (JsValue) _left.Evaluate();
            }

            if (_expression.Right.Type == Nodes.Literal)
            {
                _rightLiteral = (JsValue) _right.Evaluate();
            }
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
                result = new JintConstantExpression(engine, (JsValue) result.Evaluate());
            }

            return result;
        }

        public static bool StrictlyEqual(JsValue x, JsValue y)
        {
            if (x._type != y._type)
            {
                return false;
            }

            if (x._type == Types.Boolean || x._type == Types.String)
            {
                return x.Equals(y);
            }


            if (x._type >= Types.None && x._type <= Types.Null)
            {
                return true;
            }

            if (x is JsNumber jsNumber)
            {
                var nx = jsNumber._value;
                var ny = ((JsNumber) y)._value;
                return !double.IsNaN(nx) && !double.IsNaN(ny) && nx == ny;
            }

            if (x is IObjectWrapper xw)
            {
                if (!(y is IObjectWrapper yw))
                {
                    return false;
                }

                return Equals(xw.Target, yw.Target);
            }

            return x == y;
        }

        private sealed class JintGenericBinaryExpression : JintBinaryExpression
        {
            private readonly Func<JsValue, JsValue, JsValue> _operator;

            public JintGenericBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
                switch (_expression.Operator)
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

                    case BinaryOperator.Greater:
                        _operator = (left, right) =>
                        {
                            var value = Compare(right, left, false);
                            if (value.IsUndefined())
                            {
                                value = JsBoolean.False;
                            }

                            return value;
                        };
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

                    case BinaryOperator.Less:
                        _operator = (left, right) =>
                        {
                            var value = Compare(left, right);
                            if (value.IsUndefined())
                            {
                                value = JsBoolean.False;
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
                var left = _leftLiteral ?? _engine.GetValue(_left.Evaluate(), true);
                var right = _rightLiteral ?? _engine.GetValue(_right.Evaluate(), true);

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
                var left = _leftLiteral ?? _engine.GetValue(_left.Evaluate(), true);
                var right = _rightLiteral ?? _engine.GetValue(_right.Evaluate(), true);
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
                var left = _leftLiteral ?? _engine.GetValue(_left.Evaluate(), true);
                var right = _rightLiteral ?? _engine.GetValue(_right.Evaluate(), true);
                return StrictlyEqual(left, right) ? JsBoolean.False : JsBoolean.True;
            }
        }
    }
}