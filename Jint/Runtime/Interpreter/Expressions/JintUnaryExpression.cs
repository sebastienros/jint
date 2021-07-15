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
            if (_engine.Options.Interop.OperatorOverloadingAllowed)
            {
                string operatorClrName = null;
                switch (_operator)
                {
                    case UnaryOperator.Plus:
                        operatorClrName = "op_UnaryPlus";
                        break;
                    case UnaryOperator.Minus:
                        operatorClrName = "op_UnaryNegation";
                        break;
                    case UnaryOperator.BitwiseNot:
                        operatorClrName = "op_OnesComplement";
                        break;
                    case UnaryOperator.LogicalNot:
                        operatorClrName = "op_LogicalNot";
                        break;
                    default:
                        break;
                }

                if (operatorClrName != null &&
                    TryOperatorOverloading(_engine, _argument.GetValue(), operatorClrName, out var result))
                {
                    return result;
                }
            }

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
                        if (r.IsStrictReference())
                        {
                            ExceptionHelper.ThrowSyntaxError(_engine.Realm);
                        }

                        _engine._referencePool.Return(r);
                        return JsBoolean.True;
                    }

                    if (r.IsPropertyReference())
                    {
                        if (r.IsSuperReference())
                        {
                            ExceptionHelper.ThrowReferenceError(_engine.Realm, r);
                        }

                        var o = TypeConverter.ToObject(_engine.Realm, r.GetBase());
                        var deleteStatus = o.Delete(r.GetReferencedName());
                        if (!deleteStatus && r.IsStrictReference())
                        {
                            ExceptionHelper.ThrowTypeError(_engine.Realm);
                        }

                        _engine._referencePool.Return(r);
                        return deleteStatus ? JsBoolean.True : JsBoolean.False;
                    }

                    if (r.IsStrictReference())
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine.Realm);
                    }

                    var bindings = r.GetBase().TryCast<EnvironmentRecord>();
                    var property = r.GetReferencedName();
                    _engine._referencePool.Return(r);

                    return bindings.DeleteBinding(property.ToString()) ? JsBoolean.True : JsBoolean.False;

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
                        case Types.Symbol: return JsString.SymbolString;
                    }

                    if (v.IsCallable)
                    {
                        return JsString.FunctionString;
                    }

                    return JsString.ObjectString;

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

        internal static bool TryOperatorOverloading(Engine _engine, JsValue value, string clrName, out JsValue result)
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
                    result = method.Call(_engine, null, arguments);
                    return true;
                }
            }
            result = null;
            return false;
        }
    }
}