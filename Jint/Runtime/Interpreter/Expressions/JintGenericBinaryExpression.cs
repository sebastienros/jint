using System;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintGenericBinaryExpression : JintBinaryExpression
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

        public override object Evaluate()
        {
            var left = _leftLiteral ?? _engine.GetValue(_left.Evaluate(), true);
            var right = _rightLiteral ?? _engine.GetValue(_right.Evaluate(), true);

            return _operator(left, right);
        }
    }
}