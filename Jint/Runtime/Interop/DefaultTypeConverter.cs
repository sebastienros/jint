using System;
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

                    if (type == typeof(Func<string, bool>))
                    {
                        //return (Func<string, bool>)(x =>
                        //{
                        //    var arguments = new JsValue[1];
                        //    arguments[0] = JsValue.FromObject(_engine, x);
                        //    var o = function(JsValue.Undefined, arguments).ToObject();
                        //    return (bool)Convert(o, typeof(bool), formatProvider);
                        //});
                    }

                    // create the requested Delegate
                    if (genericType == typeof (Action<>))
                    {
                    }
                    else if (genericType == typeof (Func<>))
                    {
                    }
                    else if (genericType == typeof (Func<,>))
                    {
                        // return a function instance containing the following body

                        //var arguments = new JsValue[1];
                        //arguments[0] = JsValue.FromObject(_engine, x);
                        //var o = function(JsValue.Undefined, arguments).ToObject();
                        //return (bool) Convert(o, typeof (bool), formatProvider);

                        var genericArguments = type.GetGenericArguments();
                        var returnType = genericArguments.Last();

                        ParameterExpression xExpr = Expression.Parameter(genericArguments[0], "x");

                        // var arguments = new JsValue[1];
                        ParameterExpression argumentsVariable = Expression.Variable(typeof (JsValue[]), "arguments");
                        var argumentsExpression = Expression.Constant(new JsValue[genericArguments.Length - 1]);
                        Expression.Assign(argumentsVariable, argumentsExpression);

                        //arguments[0] = JsValue.FromObject(_engine, x);
                        IndexExpression arrayAccess = Expression.ArrayAccess(argumentsExpression, Expression.Constant(0, typeof (int)));
                        var convertExpression = Expression.Call(null,
                            typeof (JsValue).GetMethod("FromObject"),
                            Expression.Constant(_engine, typeof (Engine)),
                            xExpr);

                        Expression.Assign(arrayAccess, convertExpression);

                        //var o = function(JsValue.Undefined, arguments).ToObject();
                        ParameterExpression oVariable = Expression.Variable(typeof(object), "o");
                        Expression.Assign(oVariable,
                            Expression.Call(Expression.Constant(null),
                                function.Method, 
                                Expression.Constant(JsValue.Undefined, typeof (JsValue)),
                                argumentsExpression));

                        var castExpression = Expression.Convert(
                            Expression.Call(
                                Expression.Constant(this), 
                                this.GetType().GetMethod("Convert"), 
                                oVariable,
                                Expression.Constant(returnType), 
                                Expression.Constant(formatProvider)), 
                            returnType);

                        return Expression.Lambda(castExpression);
                    }
                }
                else
                {
                    if (type == typeof (Action))
                    {
                        return (Action)(() => function(JsValue.Undefined, new JsValue[0]));
                    }
                }

            }

            return System.Convert.ChangeType(value, type, formatProvider);
        }
    }
}
