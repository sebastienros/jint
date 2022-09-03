using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop
{
    public class DefaultTypeConverter : ITypeConverter
    {
        private readonly Engine _engine;

        private readonly record struct TypeConversionKey(Type Source, Type Target);

        private static readonly ConcurrentDictionary<TypeConversionKey, bool> _knownConversions = new();
        private static readonly ConcurrentDictionary<TypeConversionKey, MethodInfo?> _knownCastOperators = new();

        private static readonly Type intType = typeof(int);
        private static readonly Type iCallableType = typeof(Func<JsValue, JsValue[], JsValue>);
        private static readonly Type jsValueType = typeof(JsValue);
        private static readonly Type objectType = typeof(object);
        private static readonly Type engineType = typeof(Engine);
        private static readonly Type typeType = typeof(Type);

        private static readonly MethodInfo convertChangeType = typeof(Convert).GetMethod("ChangeType", new[] { objectType, typeType, typeof(IFormatProvider) });
        private static readonly MethodInfo jsValueFromObject = jsValueType.GetMethod(nameof(JsValue.FromObject));
        private static readonly MethodInfo jsValueToObject = jsValueType.GetMethod(nameof(JsValue.ToObject));


        public DefaultTypeConverter(Engine engine)
        {
            _engine = engine;
        }

        public virtual object? Convert(object? value, Type type, IFormatProvider formatProvider)
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

            if (type.IsGenericType)
            {
                var result = TypeConverter.IsAssignableToGenericType(value.GetType(), type);
                if (result.IsAssignable)
                {
                    return value;
                }
            }

            if (type.IsNullable())
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
                if (typeof(Delegate).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    // use target function instance as cache holder, this way delegate and target hold same lifetime
                    var delegatePropertyKey = "__jint_delegate_" + type.GUID;

                    var func = (Func<JsValue, JsValue[], JsValue>) value;
                    var functionInstance = func.Target as FunctionInstance;

                    var d = functionInstance?.GetHiddenClrObjectProperty(delegatePropertyKey) as Delegate;

                    if (d is null)
                    {
                        d = BuildDelegate(type, func);
                        functionInstance?.SetHiddenClrObjectProperty(delegatePropertyKey, d);
                    }

                    return d;
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
                var itemsConverted = new object?[source.Length];
                for (var i = 0; i < source.Length; i++)
                {
                    itemsConverted[i] = Convert(source[i], targetElementType, formatProvider);
                }
                var result = Array.CreateInstance(targetElementType, source.Length);
                itemsConverted.CopyTo(result, 0);
                return result;
            }

            var typeDescriptor = TypeDescriptor.Get(valueType);
            if (typeDescriptor.IsStringKeyedGenericDictionary)
            {
                // public empty constructor required
                var constructors = type.GetConstructors();
                // value types
                if (type.IsValueType && constructors.Length > 0)
                {
                    ExceptionHelper.ThrowArgumentException("No valid constructors found");
                }

                // reference types - return null if no valid constructor is found
                if (!type.IsValueType)
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
                        ExceptionHelper.ThrowArgumentException("No valid constructors found");
                    }
                }

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
                    if (typeDescriptor.TryGetValue(value, name, out var val))
                    {
                        var output = Convert(val, member.GetDefinedType(), formatProvider);
                        member.SetValue(obj, output);
                    }
                }

                return obj;
            }

            try
            {
                return System.Convert.ChangeType(value, type, formatProvider);
            }
            catch (Exception e)
            {
                // check if we can do a cast with operator overloading
                if (TryCastWithOperators(value, type, valueType, out var invoke))
                {
                    return invoke;
                }

                if (!_engine.Options.Interop.ExceptionHandler(e))
                {
                    throw;
                }

                ExceptionHelper.ThrowError(_engine, e.Message);
                return null;
            }
        }

        private Delegate BuildDelegate(Type type, Func<JsValue, JsValue[], JsValue> function)
        {
            var method = type.GetMethod("Invoke");
            var arguments = method.GetParameters();

            var parameters = new ParameterExpression[arguments.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                parameters[i] = Expression.Parameter(arguments[i].ParameterType, arguments[i].Name);
            }

            var initializers = new MethodCallExpression[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                if (param.Type.IsValueType)
                {
                    var boxing = Expression.Convert(param, objectType);
                    initializers[i] = Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), boxing);
                }
                else
                {
                    initializers[i] = Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), param);
                }
            }

            var vars = Expression.NewArrayInit(jsValueType, initializers);

            var callExpression = Expression.Call(
                Expression.Constant(function.Target),
                function.Method,
                Expression.Constant(JsValue.Undefined, jsValueType),
                vars);

            if (method.ReturnType != typeof(void))
            {
                return Expression.Lambda(
                    type,
                    Expression.Convert(
                        Expression.Call(
                            null,
                            convertChangeType,
                            Expression.Call(callExpression, jsValueToObject),
                            Expression.Constant(method.ReturnType),
                            Expression.Constant(System.Globalization.CultureInfo.InvariantCulture, typeof(IFormatProvider))
                        ),
                        method.ReturnType
                    ),
                    new ReadOnlyCollection<ParameterExpression>(parameters)).Compile();
            }

            return Expression.Lambda(
                type,
                callExpression,
                new ReadOnlyCollection<ParameterExpression>(parameters)).Compile();
        }

        private static bool TryCastWithOperators(object value, Type type, Type valueType, [NotNullWhen(true)] out object? converted)
        {
            var key = new TypeConversionKey(valueType, type);

            static MethodInfo? CreateValueFactory(TypeConversionKey k)
            {
                var (source, target) = k;
                foreach (var m in source.GetOperatorOverloadMethods().Concat(target.GetOperatorOverloadMethods()))
                {
                    if (!target.IsAssignableFrom(m.ReturnType) || m.Name is not ("op_Implicit" or "op_Explicit"))
                    {
                        continue;
                    }

                    var parameters = m.GetParameters();
                    if (parameters.Length != 1 || !parameters[0].ParameterType.IsAssignableFrom(source))
                    {
                        continue;
                    }

                    // we found a match
                    return m;
                }

                return null;
            }

            var castOperator = _knownCastOperators.GetOrAdd(key, CreateValueFactory);

            if (castOperator != null)
            {
                try
                {
                    converted = castOperator.Invoke(null, new[] { value });
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

        public virtual bool TryConvert(object? value, Type type, IFormatProvider formatProvider, out object? converted)
        {
            var key = new TypeConversionKey(value?.GetType() ?? typeof(void), type);

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

    internal static class ObjectExtensions
    {
        public static object? GetHiddenClrObjectProperty(this ObjectInstance obj, string name)
        {
            return (obj.Get(name) as IObjectWrapper)?.Target;
        }

        public static void SetHiddenClrObjectProperty(this ObjectInstance obj, string name, object value)
        {
            obj.SetOwnProperty(name, new PropertyDescriptor(new ObjectWrapper(obj.Engine, value), PropertyFlag.AllForbidden));
        }
    }
}
