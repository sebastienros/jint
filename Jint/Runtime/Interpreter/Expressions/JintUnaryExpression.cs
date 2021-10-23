using Esprima.Ast;
using Jint.Extensions;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.References;
using System.Collections.Concurrent;
using System.Linq;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintUnaryExpression : JintExpression
    {
#if NETSTANDARD
        private static readonly ConcurrentDictionary<(string OperatorName, System.Type Operand), MethodDescriptor> _knownOperators =
            new ConcurrentDictionary<(string OperatorName, System.Type Operand), MethodDescriptor>();
#else
        private static readonly ConcurrentDictionary<string, MethodDescriptor> _knownOperators = new ConcurrentDictionary<string, MethodDescriptor>();
#endif

        private readonly JintExpression _argument;
        private readonly UnaryOperator _operator;

        private JintUnaryExpression(Engine engine, UnaryExpression expression) : base(expression)
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
                    return new JintConstantExpression(expression, EvaluateMinus(value));
                }
            }

            return new JintUnaryExpression(engine, expression);
        }

        public override Completion GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxNode = _expression;

            return Completion.Normal(EvaluateJsValue(context), _expression.Location);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            return NormalCompletion(EvaluateJsValue(context));
        }

        private JsValue EvaluateJsValue(EvaluationContext context)
        {
            var engine = context.Engine;
            switch (_operator)
            {
                case UnaryOperator.Plus:
                {
                    var v = _argument.GetValue(context).Value;
                    if (context.OperatorOverloadingAllowed &&
                        TryOperatorOverloading(context, v, "op_UnaryPlus", out var result))
                    {
                        return result;
                    }

                    return v.IsInteger() && v.AsInteger() != 0
                        ? v
                        : JsNumber.Create(TypeConverter.ToNumber(v));
                }
                case UnaryOperator.Minus:
                {
                    var v = _argument.GetValue(context).Value;
                    if (context.OperatorOverloadingAllowed &&
                        TryOperatorOverloading(context, v, "op_UnaryNegation", out var result))
                    {
                        return result;
                    }

                    return EvaluateMinus(v);
                }
                case UnaryOperator.BitwiseNot:
                {
                    var v = _argument.GetValue(context).Value;
                    if (context.OperatorOverloadingAllowed &&
                        TryOperatorOverloading(context, v, "op_OnesComplement", out var result))
                    {
                        return result;
                    }

                    return JsNumber.Create(~TypeConverter.ToInt32(v));
                }
                case UnaryOperator.LogicalNot:
                {
                    var v = _argument.GetValue(context).Value;
                    if (context.OperatorOverloadingAllowed &&
                        TryOperatorOverloading(context, v, "op_LogicalNot", out var result))
                    {
                        return result;
                    }

                    return !TypeConverter.ToBoolean(v) ? JsBoolean.True : JsBoolean.False;
                }

                case UnaryOperator.Delete:
                    var r = _argument.Evaluate(context).Value as Reference;
                    if (r == null)
                    {
                        return JsBoolean.True;
                    }

                    if (r.IsUnresolvableReference())
                    {
                        if (r.IsStrictReference())
                        {
                            ExceptionHelper.ThrowSyntaxError(engine.Realm);
                        }

                        engine._referencePool.Return(r);
                        return JsBoolean.True;
                    }

                    if (r.IsPropertyReference())
                    {
                        if (r.IsSuperReference())
                        {
                            ExceptionHelper.ThrowReferenceError(engine.Realm, r);
                        }

                        var o = TypeConverter.ToObject(engine.Realm, r.GetBase());
                        var deleteStatus = o.Delete(r.GetReferencedName());
                        if (!deleteStatus && r.IsStrictReference())
                        {
                            ExceptionHelper.ThrowTypeError(engine.Realm);
                        }

                        engine._referencePool.Return(r);
                        return deleteStatus ? JsBoolean.True : JsBoolean.False;
                    }

                    if (r.IsStrictReference())
                    {
                        ExceptionHelper.ThrowSyntaxError(engine.Realm);
                    }

                    var bindings = r.GetBase().TryCast<EnvironmentRecord>();
                    var property = r.GetReferencedName();
                    engine._referencePool.Return(r);

                    return bindings.DeleteBinding(property.ToString()) ? JsBoolean.True : JsBoolean.False;

                case UnaryOperator.Void:
                    _argument.GetValue(context);
                    return Undefined.Instance;

                case UnaryOperator.TypeOf:
                {
                    var result = _argument.Evaluate(context);
                    JsValue v;

                    if (result.Value is Reference rf)
                    {
                        if (rf.IsUnresolvableReference())
                        {
                            engine._referencePool.Return(rf);
                            return JsString.UndefinedString;
                        }

                        v = engine.GetValue(rf, true);
                    }
                    else
                    { 
                        v = (JsValue) result.Value;
                    }

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
                }
                default:
                    ExceptionHelper.ThrowArgumentException();
                    return null;
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

        internal static bool TryOperatorOverloading(EvaluationContext context, JsValue value, string clrName, out JsValue result)
        {
            var operand = value.ToObject();

            if (operand != null)
            {
                var operandType = operand.GetType();
                var arguments = new[] { value };

#if NETSTANDARD
                var key = (clrName, operandType);
#else
                var key = $"{clrName}->{operandType}";
#endif
                var method = _knownOperators.GetOrAdd(key, _ =>
                {
                    var foundMethod = operandType.GetOperatorOverloadMethods()
                        .FirstOrDefault(x => x.Name == clrName && x.GetParameters().Length == 1);

                    if (foundMethod != null)
                    {
                        return new MethodDescriptor(foundMethod);
                    }
                    return null;
                });

                if (method != null)
                {
                    result = method.Call(context.Engine, null, arguments);
                    return true;
                }
            }
            result = null;
            return false;
        }
    }
}