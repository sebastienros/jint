using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.References;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintUnaryExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            switch (_operator)
            {
                case UnaryOperator.Plus:
                    var plusValue = await _argument.GetValueAsync();
                    return plusValue.IsInteger() && plusValue.AsInteger() != 0
                        ? plusValue
                        : JsNumber.Create(TypeConverter.ToNumber(plusValue));

                case UnaryOperator.Minus:
                    return EvaluateMinus(await _argument.GetValueAsync());

                case UnaryOperator.BitwiseNot:
                    return JsNumber.Create(~TypeConverter.ToInt32(await _argument.GetValueAsync()));

                case UnaryOperator.LogicalNot:
                    return !TypeConverter.ToBoolean(await _argument.GetValueAsync()) ? JsBoolean.True : JsBoolean.False;

                case UnaryOperator.Delete:
                    var r = await _argument.EvaluateAsync() as Reference;
                    if (r == null)
                    {
                        return JsBoolean.True;
                    }

                    if (r.IsUnresolvableReference())
                    {
                        if (r.IsStrictReference())
                        {
                            ExceptionHelper.ThrowSyntaxError(_engine);
                        }

                        _engine._referencePool.Return(r);
                        return JsBoolean.True;
                    }

                    if (r.IsPropertyReference())
                    {
                        if (r.IsSuperReference())
                        {
                            ExceptionHelper.ThrowReferenceError(_engine, r);
                        }

                        var o = TypeConverter.ToObject(_engine, r.GetBase());
                        var deleteStatus = o.Delete(r.GetReferencedName());
                        if (!deleteStatus && r.IsStrictReference())
                        {
                            ExceptionHelper.ThrowTypeError(_engine);
                        }

                        _engine._referencePool.Return(r);
                        return deleteStatus ? JsBoolean.True : JsBoolean.False;
                    }

                    if (r.IsStrictReference())
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    var bindings = r.GetBase().TryCast<EnvironmentRecord>();
                    var property = r.GetReferencedName();
                    _engine._referencePool.Return(r);

                    return bindings.DeleteBinding(property.ToString()) ? JsBoolean.True : JsBoolean.False;

                case UnaryOperator.Void:
                    await _argument.GetValueAsync();
                    return Undefined.Instance;

                case UnaryOperator.TypeOf:
                    var value = await _argument.EvaluateAsync();
                    r = value as Reference;
                    if (r != null)
                    {
                        if (r.IsUnresolvableReference())
                        {
                            _engine._referencePool.Return(r);
                            return JsString.UndefinedString;
                        }
                    }

                    var v = await _argument.GetValueAsync();

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
                        case Types.Symbol: return JsString.SymbolString;
                    }

                    if (v.IsCallable)
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