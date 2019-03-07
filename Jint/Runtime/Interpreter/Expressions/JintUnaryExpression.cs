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

        public JintUnaryExpression(Engine engine, UnaryExpression expression) : base(engine, expression)
        {
            _argument = Build(engine, expression.Argument);
            _operator = expression.Operator;
        }

        protected override object EvaluateInternal()
        {
            switch (_operator)
            {
                case UnaryOperator.Plus:
                    return JsNumber.Create(TypeConverter.ToNumber(_argument.GetValue()));

                case UnaryOperator.Minus:
                    var n = TypeConverter.ToNumber(_argument.GetValue());
                    return JsNumber.Create(double.IsNaN(n) ? double.NaN : n * -1);

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
                        var jsValue = o.Delete(r._name, r._strict);
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
    }
}