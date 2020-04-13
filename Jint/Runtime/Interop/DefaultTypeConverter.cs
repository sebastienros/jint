using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;

namespace Jint.Runtime.Interop
{
    public class DefaultTypeConverter : ITypeConverter
    {
        private readonly Engine _engine;

#if NETSTANDARD
        private static readonly ConcurrentDictionary<(Type Source, Type Target), bool> _knownConversions = new ConcurrentDictionary<(Type Source, Type Target), bool>();
#else
        private static readonly ConcurrentDictionary<string, bool> _knownConversions = new ConcurrentDictionary<string, bool>();
#endif

        private static readonly Type nullableType = typeof(Nullable<>);
        private static readonly Type intType = typeof(int);
        private static readonly Type iCallableType = typeof(Func<JsValue, JsValue[], JsValue>);
        private static readonly Type jsValueType = typeof(JsValue);
        private static readonly Type objectType = typeof(object);
        private static readonly Type engineType = typeof(Engine);
        private static readonly Type typeType = typeof(Type);

        private static readonly MethodInfo convertChangeType = typeof(Convert).GetMethod("ChangeType", new [] { objectType, typeType, typeof(IFormatProvider) });
        private static readonly MethodInfo jsValueFromObject = jsValueType.GetMethod(nameof(JsValue.FromObject));
        private static readonly MethodInfo jsValueToObject = jsValueType.GetMethod(nameof(JsValue.ToObject));


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

                ExceptionHelper.ThrowNotSupportedException($"Unable to convert null to '{type.FullName}'");
            }

            // don't try to convert if value is derived from type
            if (type.IsInstanceOfType(value))
            {
                return value;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == nullableType)
            {
                type = Nullable.GetUnderlyingType(type);
            }

            if (type.IsEnum)
            {
                var integer = System.Convert.ChangeType(value, intType, formatProvider);
                if (integer == null)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException();
                }

                return Enum.ToObject(type, integer);
            }

            var valueType = value.GetType();
            // is the javascript value an ICallable instance ?
            if (valueType == iCallableType)
            {
                var function = (Func<JsValue, JsValue[], JsValue>)value;

                if (type.IsGenericType)
                {
                    var genericType = type.GetGenericTypeDefinition();

                    // create the requested Delegate
                    if (genericType.Name.StartsWith("Action"))
                    {
                        var genericArguments = type.GetGenericArguments();

                        var @params = new ParameterExpression[genericArguments.Length];
                        for (var i = 0; i < @params.Length; i++)
                        {
                            @params[i] = Expression.Parameter(genericArguments[i], genericArguments[i].Name + i);
                        }
                        var tmpVars = new Expression[@params.Length];
                        for (var i = 0; i < @params.Length; i++)
                        {
                            var param = @params[i];
                            if (param.Type.IsValueType)
                            {
                                var boxing = Expression.Convert(param, objectType);
                                tmpVars[i] = Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), boxing);
                            }
                            else
                            {
                                tmpVars[i] = Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), param);
                            }
                        }
                        var @vars = Expression.NewArrayInit(jsValueType, tmpVars);

                        var callExpresion = Expression.Block(Expression.Call(
                                                Expression.Call(Expression.Constant(function.Target),
                                                    function.Method,
                                                    Expression.Constant(JsValue.Undefined, jsValueType),
                                                    @vars),
                                                jsValueToObject), Expression.Empty());

                        return Expression.Lambda(callExpresion, new ReadOnlyCollection<ParameterExpression>(@params)).Compile();
                    }
                    else if (genericType.Name.StartsWith("Func"))
                    {
                        var genericArguments = type.GetGenericArguments();
                        var returnType = genericArguments[genericArguments.Length - 1];

                        var @params = new ParameterExpression[genericArguments.Length - 1];
                        for (var i = 0; i < @params.Length; i++)
                        {
                            @params[i] = Expression.Parameter(genericArguments[i], genericArguments[i].Name + i);
                        }

                        var initializers = new MethodCallExpression[@params.Length];
                        for (int i = 0; i < @params.Length; i++)
                        {
                            var boxingExpression = Expression.Convert(@params[i], objectType);
                            initializers[i]= Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), boxingExpression);
                        }
                        var @vars = Expression.NewArrayInit(jsValueType, initializers);

                        // the final result's type needs to be changed before casting,
                        // for instance when a function returns a number (double) but C# expects an integer

                        var callExpresion = Expression.Convert(
                                                Expression.Call(null,
                                                    convertChangeType,
                                                    Expression.Call(
                                                            Expression.Call(Expression.Constant(function.Target),
                                                                    function.Method,
                                                                    Expression.Constant(JsValue.Undefined, jsValueType),
                                                                    @vars),
                                                            jsValueToObject),
                                                        Expression.Constant(returnType, typeType),
                                                        Expression.Constant(System.Globalization.CultureInfo.InvariantCulture, typeof(IFormatProvider))
                                                        ),
                                                    returnType);

                        return Expression.Lambda(callExpresion, new ReadOnlyCollection<ParameterExpression>(@params)).Compile();
                    }
                }
                else
                {
                    if (type == typeof(Action))
                    {
                        return (Action)(() => function(JsValue.Undefined, System.Array.Empty<JsValue>()));
                    }
                    else if (typeof(MulticastDelegate).IsAssignableFrom(type))
                    {
                        var method = type.GetMethod("Invoke");
                        var arguments = method.GetParameters();

                        var @params = new ParameterExpression[arguments.Length];
                        for (var i = 0; i < @params.Length; i++)
                        {
                            @params[i] = Expression.Parameter(objectType, arguments[i].Name);
                        }

                        var initializers = new MethodCallExpression[@params.Length];
                        for (int i = 0; i < @params.Length; i++)
                        {
                            initializers[i] = Expression.Call(null, jsValueType.GetMethod("FromObject"), Expression.Constant(_engine, engineType), @params[i]);
                        }

                        var @vars = Expression.NewArrayInit(jsValueType, initializers);

                        var callExpression = Expression.Block(
                                                Expression.Call(
                                                    Expression.Call(Expression.Constant(function.Target),
                                                        function.Method,
                                                        Expression.Constant(JsValue.Undefined, jsValueType),
                                                        @vars),
                                                    jsValueType.GetMethod("ToObject")),
                                                Expression.Empty());

                        var dynamicExpression = Expression.Invoke(Expression.Lambda(callExpression, new ReadOnlyCollection<ParameterExpression>(@params)), new ReadOnlyCollection<ParameterExpression>(@params));

                        return Expression.Lambda(type, dynamicExpression, new ReadOnlyCollection<ParameterExpression>(@params)).Compile();
                    }
                }

            }

            if (type.IsArray)
            {
                var source = value as object[];
                if (source == null)
                {
                    ExceptionHelper.ThrowArgumentException($"Value of object[] type is expected, but actual type is {value.GetType()}.");
                }

                var targetElementType = type.GetElementType();
                var itemsConverted = new object[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    itemsConverted[i] = Convert(source[i], targetElementType, formatProvider);
                }
                var result = Array.CreateInstance(targetElementType, source.Length);
                itemsConverted.CopyTo(result, 0);
                return result;
            }

            if (value is ExpandoObject eObj)
            {
                // public empty constructor required
                var constructors = type.GetConstructors();
                // value types
                if (type.IsValueType && constructors.Length > 0)
                {
                    return null;
                }

                // reference types - return null if no valid constructor is found
                if(!type.IsValueType)
                {
                    var found = false;
                    foreach (var constructor in constructors)
                    {
                        if (constructor.GetParameters().Length == 0 && constructor.IsPublic)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        // found no valid constructor
                        return null;
                    }
                }

                var dict = (IDictionary<string, object>) eObj;
                var obj = Activator.CreateInstance(type, System.Array.Empty<object>());

                var members = type.GetMembers();
                foreach (var member in members)
                {
                    // only use fields an properties
                    if (member.MemberType != MemberTypes.Property &&
                        member.MemberType != MemberTypes.Field)
                    {
                        continue;
                    }

                    var name = member.Name.UpperToLowerCamelCase();
                    if (dict.TryGetValue(name, out var val))
                    {
                        var output = Convert(val, member.GetDefinedType(), formatProvider);
                        member.SetValue(obj, output);
                    }
                }

                return obj;
            }

            return System.Convert.ChangeType(value, type, formatProvider);
        }

        public virtual bool TryConvert(object value, Type type, IFormatProvider formatProvider, out object converted)
        {
#if NETSTANDARD
            var key = value == null ? (null, type) : (value.GetType(), type);
#else
            var key = value == null ? $"Null->{type}" : $"{value.GetType()}->{type}";
#endif

            // string conversion is not stable, "filter" -> int is invalid, "0" -> int is valid
            var canConvert = value is string || _knownConversions.GetOrAdd(key, _ =>
            {
                try
                {
                    Convert(value, type, formatProvider);
                    return true;
                }
                catch
                {
                    return false;
                }
            });

            if (canConvert)
            {
                try
                {
                    converted = Convert(value, type, formatProvider);
                    return true;
                }
                catch
                {
                    converted = null;
                    return false;
                }
            }

            converted = null;
            return false;
        }
    }
}
