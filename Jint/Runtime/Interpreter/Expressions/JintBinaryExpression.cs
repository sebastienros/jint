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

        private sealed class JintGenericBinaryExpression : JintBinaryExpression
        {
            private readonly Func<JsValue, JsValue, bool, JsValue> _operator;

            public JintGenericBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
                switch (_operatorType)
                {
                    case BinaryOperator.Plus:
                        _operator = (left, right, integerOperation) =>
                        {
                            if (integerOperation)
                            {
                                return JsNumber.Create(left.AsInteger() + right.AsInteger());
                            }

                            var lprim = TypeConverter.ToPrimitive(left);
                            var rprim = TypeConverter.ToPrimitive(right);
                            return lprim.IsString() || rprim.IsString()
                                ? (JsValue) JsString.Create(TypeConverter.ToString(lprim) + TypeConverter.ToString(rprim))
                                : JsNumber.Create(TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim));
                        };
                        break;

                    case BinaryOperator.Minus:
                        _operator = (left, right, integerOperation) =>
                            integerOperation
                                ? JsNumber.Create(left.AsInteger() - right.AsInteger())
                                : JsNumber.Create(TypeConverter.ToNumber(left) - TypeConverter.ToNumber(right));
                        break;

                    case BinaryOperator.Times:
                        _operator = (left, right, integerOperation) =>
                        {
                            if (integerOperation)
                            {
                                return JsNumber.Create((long) left.AsInteger() * right.AsInteger());
                            }

                            if (left.IsUndefined() || right.IsUndefined())
                            {
                                return Undefined.Instance;
                            }

                            return JsNumber.Create(TypeConverter.ToNumber(left) * TypeConverter.ToNumber(right));
                        };
                        break;

                    case BinaryOperator.Divide:
                        _operator = Divide;
                        break;

                    case BinaryOperator.Modulo:
                        _operator = (left, right, integerOperation) =>
                        {
                            if (integerOperation)
                            {
                                var asInteger = left.AsInteger();
                                if (asInteger > 0)
                                {
                                    return asInteger % right.AsInteger();
                                }
                            }

                            if (left.IsUndefined() || right.IsUndefined())
                            {
                                return Undefined.Instance;
                            }

                            return TypeConverter.ToNumber(left) % TypeConverter.ToNumber(right);
                        };

                        break;

                    case BinaryOperator.Equal:
                        _operator = (left, right, integerOperation) => Equal(left, right)
                            ? JsBoolean.True
                            : JsBoolean.False;
                        break;

                    case BinaryOperator.NotEqual:
                        _operator = (left, right, integerOperation) => Equal(left, right)
                            ? JsBoolean.False
                            : JsBoolean.True;
                        break;

                    case BinaryOperator.GreaterOrEqual:
                        _operator = (left, right, integerOperation) =>
                        {
                            var value = Compare(left, right);
                            return value.IsUndefined() || ((JsBoolean) value)._value
                                ? JsBoolean.False
                                : JsBoolean.True;
                        };

                        break;

                    case BinaryOperator.LessOrEqual:
                        _operator = (left, right, integerOperation) =>
                        {
                            var value = Compare(right, left, false);
                            return value.IsUndefined() || ((JsBoolean) value)._value
                                ? JsBoolean.False
                                : JsBoolean.True;
                        };

                        break;

                    case BinaryOperator.BitwiseAnd:
                        _operator = (left, right, _) => JsNumber.Create(TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right));
                        break;

                    case BinaryOperator.BitwiseOr:
                        _operator = (left, right, _) => JsNumber.Create(TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right));
                        break;

                    case BinaryOperator.BitwiseXOr:
                        _operator = (left, right, _) => JsNumber.Create(TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right));
                        break;

                    case BinaryOperator.LeftShift:
                        _operator = (left, right, _) => JsNumber.Create(TypeConverter.ToInt32(left) << (int) (TypeConverter.ToUint32(right) & 0x1F));
                        break;

                    case BinaryOperator.RightShift:
                        _operator = (left, right, _) => JsNumber.Create(TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                        break;

                    case BinaryOperator.UnsignedRightShift:
                        _operator = (left, right, _) => JsNumber.Create((uint) TypeConverter.ToInt32(left) >> (int) (TypeConverter.ToUint32(right) & 0x1F));
                        break;

                    case BinaryOperator.Exponentiation:
                        _operator = (left, right, _) => JsNumber.Create(Math.Pow(TypeConverter.ToNumber(left), TypeConverter.ToNumber(right)));
                        break;

                    case BinaryOperator.InstanceOf:
                        _operator = (left, right, _) =>
                        {
                            if (!(right is FunctionInstance f))
                            {
                                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "instanceof can only be used with a function object");
                            }

                            return f.HasInstance(left) ? JsBoolean.True : JsBoolean.False;
                        };
                        break;

                    case BinaryOperator.In:
                        _operator = (left, right, integerOperation) =>
                        {
                            if (!(right is ObjectInstance oi))
                            {
                                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "in can only be used with an object");
                            }

                            return oi.HasProperty(TypeConverter.ToString(left)) ? JsBoolean.True : JsBoolean.False;
                        };

                        break;

                    default:
                        _operator = ExceptionHelper.ThrowNotImplementedException<Func<JsValue, JsValue, bool, JsValue>>();
                        break;
                }
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                var isIntegerComparison = left._type == right._type && left._type == InternalTypes.Integer;
                return _operator(left, right, isIntegerComparison);
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
    }
}