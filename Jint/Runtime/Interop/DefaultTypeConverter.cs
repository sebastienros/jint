using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using Jint.Native;

namespace Jint.Runtime.Interop
{
    public class DefaultTypeConverter : ITypeConverter
    {
        private readonly Engine _engine;

        public DefaultTypeConverter(Engine engine)
        {
            _engine = engine;
        }

        public object Convert(object value, Type type, IFormatProvider formatProvider)
        {
            if (value == null)
            {
                if (TypeIsNullable(type))
                {
                    return null;
                }

                throw new NotSupportedException(string.Format("Unable to convert null to '{0}'", type.FullName));
            }

            // don't try to convert if value is derived from type
            if (type.IsInstanceOfType(value))
            {
                return value;
            }

            if (type.IsEnum)
            {
                var integer = System.Convert.ChangeType(value, typeof (int), formatProvider);
                if (integer == null)
                {
                    throw new ArgumentOutOfRangeException();
                }

                return Enum.ToObject(type, integer);
            }

            var valueType = value.GetType();
            // is the javascript value an ICallable instance ?
            if (valueType == typeof (Func<JsValue, JsValue[], JsValue>))
            {
                var function = (Func<JsValue, JsValue[], JsValue>) value;

                if (type.IsGenericType)
                {
                    var genericType = type.GetGenericTypeDefinition();

                    // create the requested Delegate
                    if (genericType.Name.StartsWith("Action"))
                    {
                        var genericArguments = type.GetGenericArguments();

                        var @params = new ParameterExpression[genericArguments.Count()];
                        for (var i = 0; i < @params.Count(); i++)
                        {
                            @params[i] = Expression.Parameter(genericArguments[i], genericArguments[i].Name + i);
                        }
                        var @vars = Expression.NewArrayInit(typeof(JsValue), @params.Select(p => Expression.Call(null, typeof(JsValue).GetMethod("FromObject"), Expression.Constant(_engine, typeof(Engine)), p)));

                        var callExpresion = Expression.Block(Expression.Call(
                                                Expression.Call(Expression.Constant(function.Target),
                                                    function.Method,
                                                    Expression.Constant(JsValue.Undefined, typeof(JsValue)),
                                                    @vars),
                                                typeof(JsValue).GetMethod("ToObject")), Expression.Empty());

                        return Expression.Lambda(callExpresion, new ReadOnlyCollection<ParameterExpression>(@params));
                    }
                    else if (genericType.Name.StartsWith("Func"))
                    {
                        var genericArguments = type.GetGenericArguments();
                        var returnType = genericArguments.Last();
                        
                        var @params = new ParameterExpression[genericArguments.Count() - 1];
                        for (var i = 0; i < @params.Count(); i++)
                        {
                            @params[i] = Expression.Parameter(genericArguments[i], genericArguments[i].Name + i);
                        }
                        var @vars = Expression.NewArrayInit(typeof(JsValue), @params.Select(p => Expression.Call(null, typeof(JsValue).GetMethod("FromObject"), Expression.Constant(_engine, typeof(Engine)), p)));

                        var callExpresion = Expression.Convert(
                                                Expression.Call(
                                                    Expression.Call(Expression.Constant(function.Target),
                                                            function.Method,
                                                            Expression.Constant(JsValue.Undefined, typeof(JsValue)),
                                                            @vars),
                                                    typeof(JsValue).GetMethod("ToObject")),
                                                returnType);

                        return Expression.Lambda(callExpresion, new ReadOnlyCollection<ParameterExpression>(@params));
                    }
                }
                else
                {
                    if (type == typeof (Action))
                    {
                        return (Action)(() => function(JsValue.Undefined, new JsValue[0]));
                    }
                    else if (type.IsSubclassOf(typeof(System.MulticastDelegate)))
                    {
                        var method = type.GetMethod("Invoke");
                        var arguments = method.GetParameters();

                        var @params = new ParameterExpression[arguments.Count()];
                        for (var i = 0; i < @params.Count(); i++)
                        {
                            @params[i] = Expression.Parameter(typeof(object), arguments[i].Name);
                        }
                        var @vars = Expression.NewArrayInit(typeof(JsValue), @params.Select(p => Expression.Call(null, typeof(JsValue).GetMethod("FromObject"), Expression.Constant(_engine, typeof(Engine)), p)));

                        var callExpression = Expression.Block(
                                                Expression.Call(
                                                    Expression.Call(Expression.Constant(function.Target),
                                                        function.Method,
                                                        Expression.Constant(JsValue.Undefined, typeof(JsValue)),
                                                        @vars),
                                                    typeof(JsValue).GetMethod("ToObject")),
                                                Expression.Empty());

                        var dynamicExpression = Expression.Invoke(Expression.Lambda(callExpression, new ReadOnlyCollection<ParameterExpression>(@params)), new ReadOnlyCollection<ParameterExpression>(@params));

                        return Expression.Lambda(type, dynamicExpression, new ReadOnlyCollection<ParameterExpression>(@params));
                    }
                }

            }

            return System.Convert.ChangeType(value, type, formatProvider);
        }

        private static bool TypeIsNullable(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
    }
}
