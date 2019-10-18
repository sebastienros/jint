using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintUnaryExpression : JintExpression
    {
        private readonly JintExpression _argument;
        private readonly UnaryOperator _operator;

        private JintUnaryExpression(Engine engine, UnaryExpression expression) : base(engine, expression)
        {
            _argument = Build(engine, expression.Argument);
            _operator = expression.Operator;
        }

        internal static JintExpression Build(Engine engine, UnaryExpression expression)
        {
            if (expression.Operator == UnaryOperator.Minus
                && expression.Argument is Literal literal)
            {
                var value = JintLiteralExpression.ConvertToJsValue(literal);
                if (!(value is null))
                {
                    // valid for caching
                    return new JintConstantExpression(engine, expression, EvaluateMinus(value));
                }
            }

            return new JintUnaryExpression(engine, expression);
        }

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            return (JsValue) EvaluateInternal();
        }

        protected override object EvaluateInternal()
        {
            switch (_operator)
            {
                case UnaryOperator.Plus:
                    var plusValue = _argument.GetValue();
                    return plusValue.IsInteger() && plusValue.AsInteger() != 0
                        ? plusValue
                        : JsNumber.Create(TypeConverter.ToNumber(plusValue));

                case UnaryOperator.Minus:
                    return EvaluateMinus(_argument.GetValue());

                case UnaryOperator.BitwiseNot:
                    return JsNumber.Create(~TypeConverter.ToInt32(_argument.GetValue()));

                case UnaryOperator.LogicalNot:
                    return !TypeConverter.ToBoolean(_argument.GetValue()) ? JsBoolean.True : JsBoolean.False;

                case UnaryOperator.Delete:
                    var r = _argument.Evaluate() as Reference;
                    if (r == null)
                    {
                        return JsBoolean.True;
                    }

                    if (r.IsUnresolvableReference())
                    {
                        if (r._strict)
                        {
                            ExceptionHelper.ThrowSyntaxError(_engine);
                        }

                        _engine._referencePool.Return(r);
                        return JsBoolean.True;
                    }

                    if (r.IsPropertyReference())
                    {
                        var o = TypeConverter.ToObject(_engine, r.GetBase());
                        var jsValue = o.Delete(r.GetReferencedName(), r._strict);
                        _engine._referencePool.Return(r);
                        return jsValue ? JsBoolean.True : JsBoolean.False;
                    }

                    if (r._strict)
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    var bindings = r.GetBase().TryCast<EnvironmentRecord>();
                    var referencedName = r.GetReferencedName();
                    _engine._referencePool.Return(r);

                    return bindings.DeleteBinding(referencedName) ? JsBoolean.True : JsBoolean.False;

                case UnaryOperator.Void:
                    _argument.GetValue();
                    return Undefined.Instance;

                case UnaryOperator.TypeOf:
                    var value = _argument.Evaluate();
                    r = value as Reference;
                    if (r != null)
                    {
                        if (r.IsUnresolvableReference())
                        {
                            _engine._referencePool.Return(r);
                            return JsString.UndefinedString;
                        }
                    }

                    var v = _argument.GetValue();

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
                        case Types.String: return JsString.StringString;
                    }

                    if (v.TryCast<ICallable>() != null)
                    {
                        return JsString.FunctionString;
                    }

                    return JsString.ObjectString;

                default:
                    return ExceptionHelper.ThrowArgumentException<object>();
            }
        }

        private static JsNumber EvaluateMinus(JsValue value)
        {
            var minusValue = value;
            if (minusValue.IsInteger())
            {
                var asInteger = minusValue.AsInteger();
                if (asInteger != 0)
                {
                    return JsNumber.Create(asInteger * -1);
                }
            }

            var n = TypeConverter.ToNumber(minusValue);
            return JsNumber.Create(double.IsNaN(n) ? double.NaN : n * -1);
        }
    }
}