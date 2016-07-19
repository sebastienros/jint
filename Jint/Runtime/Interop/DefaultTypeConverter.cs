using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using Jint.Native;
using System.Collections.Generic;
using System.Reflection;

namespace Jint.Runtime.Interop
{
    public class DefaultTypeConverter : ITypeConverter
    {
        private readonly Engine _engine;
        private static readonly Dictionary<Tuple<Type, Type>, Func<Engine, ITypeConverter, object, IFormatProvider, object>> _knownConversions = new Dictionary<Tuple<Type, Type>, Func<Engine, ITypeConverter, object, IFormatProvider, object>>();
        private static readonly object _lockObject = new object();

        private static MethodInfo convertChangeType = typeof(System.Convert).GetMethod("ChangeType", new Type[] { typeof(object), typeof(Type), typeof(IFormatProvider) } );
        private static MethodInfo jsValueFromObject = typeof(JsValue).GetMethod("FromObject");
        private static MethodInfo jsValueToObject = typeof(JsValue).GetMethod("ToObject");

        public DefaultTypeConverter(Engine engine)
        {
            _engine = engine;
        }

        public virtual object Convert(object value, Type type, IFormatProvider formatProvider)
        {
            if (value == null)
            {
                if (TypeConverter.TypeIsNullable(type))
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

            return LearnConversion(value.GetType(), type, formatProvider)(_engine, this, value, formatProvider);
        }

        public static Func<Engine, ITypeConverter, object, IFormatProvider, object> LearnConversion(Type valueType, Type type, IFormatProvider formatProvider)
        {
            // Assumes that by this time, value is not null and is not of the same type as 'type' argument.
            if (type.IsEnum)
            {
                // Enum.ToObject does it's own type code check, but we can eliminate reflection cost completely since we know source and target types.
                var typeCode = Type.GetTypeCode(valueType);
                if (typeCode == TypeCode.SByte ||
                    typeCode == TypeCode.Int16 ||
                    typeCode == TypeCode.Int32 ||
                    typeCode == TypeCode.Int64 ||
                    typeCode == TypeCode.Byte ||
                    typeCode == TypeCode.UInt16 ||
                    typeCode == TypeCode.UInt32 ||
                    typeCode == TypeCode.UInt64)
                {
                    return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) => Enum.ToObject(type, value);
                }
                if (typeCode == TypeCode.Single)
                {
                    return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) => Enum.ToObject(type, (int)(float)value);
                }
                if (typeCode == TypeCode.Double)
                {
                    return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) => Enum.ToObject(type, (int)(double)value);
                }
                else
                {                    
                    return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) =>
                    {
                        var integer = System.Convert.ChangeType(value, typeof(int), formatProvider);
                        if (integer == null)
                        {
                            throw new ArgumentOutOfRangeException();
                        }

                        return Enum.ToObject(type, integer);
                    };
                }                    
            }

            // is the javascript value an ICallable instance ?
            if (valueType == typeof(Func<JsValue, JsValue[], JsValue>))
            {
                if (type.IsGenericType)
                {
                    var genericType = type.GetGenericTypeDefinition();

                    // create the requested Delegate
                    if (genericType.Name.StartsWith("Action"))
                    {
                        return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) =>
                        {
                            var genericArguments = type.GetGenericArguments();

                            var @params = new ParameterExpression[genericArguments.Count()];
                            for (var i = 0; i < @params.Count(); i++)
                            {
                                @params[i] = Expression.Parameter(genericArguments[i], genericArguments[i].Name + i);
                            }
                            var tmpVars = new Expression[@params.Length];
                            for (var i = 0; i < @params.Count(); i++)
                            {
                                var param = @params[i];
                                if (param.Type.IsValueType)
                                {
                                    var boxing = Expression.Convert(param, typeof(object));
                                    tmpVars[i] = Expression.Call(null, jsValueFromObject, Expression.Constant(engine, typeof(Engine)), boxing);
                                }
                                else
                                {
                                    tmpVars[i] = Expression.Call(null, jsValueFromObject, Expression.Constant(engine, typeof(Engine)), param);
                                }
                            }
                            var @vars = Expression.NewArrayInit(typeof(JsValue), tmpVars);
                        
                            var function = (Func<JsValue, JsValue[], JsValue>)value;

                            var callExpresion =
                                Expression.Block(Expression.Call(
                                    Expression.Call(Expression.Constant(function.Target),
                                        function.Method,
                                        Expression.Constant(JsValue.Undefined, typeof(JsValue)),
                                        @vars),
                                    jsValueToObject), Expression.Empty());

                            return Expression.Lambda(callExpresion, new ReadOnlyCollection<ParameterExpression>(@params));
                        };
                    }
                    else if (genericType.Name.StartsWith("Func"))
                    {
                        return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) =>
                        {
                            var genericArguments = type.GetGenericArguments();
                            var returnType = genericArguments.Last();

                            var @params = new ParameterExpression[genericArguments.Count() - 1];
                            for (var i = 0; i < @params.Count(); i++)
                            {
                                @params[i] = Expression.Parameter(genericArguments[i], genericArguments[i].Name + i);
                            }

                            var @vars = 
                                Expression.NewArrayInit(typeof(JsValue), 
                                    @params.Select(p => {
                                        var boxingExpression = Expression.Convert(p, typeof(object));
                                        return Expression.Call(null, jsValueFromObject, Expression.Constant(engine, typeof(Engine)), boxingExpression);
                                    })
                                );

                            // the final result's type needs to be changed before casting,
                            // for instance when a function returns a number (double) but C# expects an integer
                        
                            var function = (Func<JsValue, JsValue[], JsValue>)value;

                            var callExpresion =
                                Expression.Convert(
                                    Expression.Call(null,
                                        convertChangeType,
                                        Expression.Call(
                                                Expression.Call(Expression.Constant(function.Target),
                                                        function.Method,
                                                        Expression.Constant(JsValue.Undefined, typeof(JsValue)),
                                                        @vars),
                                                jsValueToObject),
                                            Expression.Constant(returnType, typeof(Type)),
                                            Expression.Constant(System.Globalization.CultureInfo.InvariantCulture, typeof(IFormatProvider))
                                            ),
                                        returnType);

                            return Expression.Lambda(callExpresion, new ReadOnlyCollection<ParameterExpression>(@params));
                        };
                    }
                }
                else
                {
                    if (type == typeof(Action))
                    {
                        return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) =>
                        {
                            var function = (Func<JsValue, JsValue[], JsValue>)value;

                            return (Action)(() => function(JsValue.Undefined, new JsValue[0]));
                        };
                    }
                    else if (type.IsSubclassOf(typeof(System.MulticastDelegate)))
                    {
                        return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) =>
                        {
                            var method = type.GetMethod("Invoke");
                            var arguments = method.GetParameters();

                            var @params = new ParameterExpression[arguments.Count()];
                            for (var i = 0; i < @params.Count(); i++)
                            {
                                @params[i] = Expression.Parameter(typeof(object), arguments[i].Name);
                            }
                            var @vars = Expression.NewArrayInit(typeof(JsValue), @params.Select(p => Expression.Call(null, typeof(JsValue).GetMethod("FromObject"), Expression.Constant(engine, typeof(Engine)), p)));
                        
                            var function = (Func<JsValue, JsValue[], JsValue>)value;

                            var callExpression =
                                Expression.Block(
                                    Expression.Call(
                                        Expression.Call(Expression.Constant(function.Target),
                                            function.Method,
                                            Expression.Constant(JsValue.Undefined, typeof(JsValue)),
                                            @vars),
                                        typeof(JsValue).GetMethod("ToObject")),
                                    Expression.Empty());

                            var dynamicExpression = Expression.Invoke(Expression.Lambda(callExpression, new ReadOnlyCollection<ParameterExpression>(@params)), new ReadOnlyCollection<ParameterExpression>(@params));

                            return Expression.Lambda(type, dynamicExpression, new ReadOnlyCollection<ParameterExpression>(@params));
                        };
                    }
                }

            }

            if (type.IsArray)
            {
                return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) =>
                {
                    var source = value as object[];
                    if (source == null)
                        throw new ArgumentException(String.Format("Value of object[] type is expected, but actual type is {0}.", value.GetType()));

                    var targetElementType = type.GetElementType();
                    var itemsConverted = source.Select(o => typeConverter.Convert(o, targetElementType, formatProvider)).ToArray();
                    var result = Array.CreateInstance(targetElementType, source.Length);
                    itemsConverted.CopyTo(result, 0);
                    return result;
                };
            }

            return (Engine engine, ITypeConverter typeConverter, object value, IFormatProvider f) => System.Convert.ChangeType(value, type, formatProvider);
        }

        public virtual bool TryConvert(object value, Type type, IFormatProvider formatProvider, out object converted)
        {
            // Skip conversion mapping for trivial conversions.
            if (value == null)
            {
                if (TypeConverter.TypeIsNullable(type))
                {
                    converted = null;
                    return true;
                }

                converted = null;
                return false;
            }

            // Don't try to convert if value is derived from type
            if (type.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            Func<Engine, ITypeConverter, object, IFormatProvider, object> converter;
            var key = new Tuple<Type, Type>(value.GetType(), type);
            if (!_knownConversions.TryGetValue(key, out converter))
            {
                lock (_lockObject)
                {
                    if (!_knownConversions.TryGetValue(key, out converter))
                    {
                        try
                        {
                            converter = LearnConversion(value.GetType(), type, formatProvider);
                            _knownConversions.Add(key, converter);
                            if (converter != null)
                            {
                                converted = converter(_engine, this, value, formatProvider);
                            }
                            else
                            {
                                converted = null;
                            }
                            return true;
                        }
                        catch
                        {
                            converted = null;
                            _knownConversions.Add(key, null);
                            return false;
                        }
                    }
                }
            }

            if (converter != null)
            {
                converted = converter(_engine, this, value, formatProvider);
                return true;
            }

            converted = null;
            return false;
        }
    }
}
