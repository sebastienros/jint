using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

        private static readonly ConcurrentDictionary<TypeConversionKey, MethodInfo?> _knownCastOperators = new();

        private static readonly Type intType = typeof(int);
        private static readonly Type iCallableType = typeof(Func<JsValue, JsValue[], JsValue>);
        private static readonly Type jsValueType = typeof(JsValue);
        private static readonly Type objectType = typeof(object);
        private static readonly Type engineType = typeof(Engine);
        private static readonly Type typeType = typeof(Type);

        private static readonly MethodInfo convertChangeType = typeof(Convert).GetMethod("ChangeType", new[] { objectType, typeType, typeof(IFormatProvider) })!;
        private static readonly MethodInfo jsValueFromObject = jsValueType.GetMethod(nameof(JsValue.FromObject))!;
        private static readonly MethodInfo jsValueToObject = jsValueType.GetMethod(nameof(JsValue.ToObject))!;


        public DefaultTypeConverter(Engine engine)
        {
            _engine = engine;
        }

        public virtual object? Convert(object? value, Type type, IFormatProvider formatProvider)
        {
            if (!TryConvert(value, type, formatProvider, propagateException: true, out var converted, out var problemMessage))
            {
                ExceptionHelper.ThrowError(_engine, problemMessage ?? $"Unable to convert {value} to type {type}");
            }
            return converted;
        }

        public virtual bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [NotNullWhen(true)] out object? converted)
        {
            return TryConvert(value, type, formatProvider, propagateException: false, out converted, out _);
        }

        private bool TryConvert(object? value, Type type, IFormatProvider formatProvider, bool propagateException, out object? converted, out string? problemMessage)
        {
            converted = null;
            problemMessage = null;

            if (value is null)
            {
                if (TypeConverter.TypeIsNullable(type))
                {
                    return true;
                }

                problemMessage = $"Unable to convert null to '{type.FullName}'";
                return false;
            }

            // don't try to convert if value is derived from type
            if (type.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            if (type.IsGenericType)
            {
                var result = TypeConverter.IsAssignableToGenericType(value.GetType(), type);
                if (result.IsAssignable)
                {
                    converted = value;
                    return true;
                }
            }

            if (type.IsNullable())
            {
                type = Nullable.GetUnderlyingType(type)!;
            }

            if (type.IsEnum)
            {
                var integer = System.Convert.ChangeType(value, intType, formatProvider);
                if (integer == null)
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException();
                }

                converted = Enum.ToObject(type, integer);
                return true;
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

                    converted = d;
                    return true;
                }
            }

            if (type.IsArray)
            {
                var source = value as object[];
                if (source == null)
                {
                    problemMessage = $"Value of object[] type is expected, but actual type is {value.GetType()}";
                    return false;
                }

                var targetElementType = type.GetElementType()!;
                var itemsConverted = new object?[source.Length];
                for (var i = 0; i < source.Length; i++)
                {
                    itemsConverted[i] = Convert(source[i], targetElementType, formatProvider);
                }
                var result = Array.CreateInstance(targetElementType, source.Length);
                itemsConverted.CopyTo(result, 0);

                converted = result;
                return true;
            }

            var typeDescriptor = TypeDescriptor.Get(valueType);
            if (typeDescriptor.IsStringKeyedGenericDictionary)
            {
                // public empty constructor required
                var constructors = type.GetConstructors();
                // value types
                if (type.IsValueType && constructors.Length > 0)
                {
                    problemMessage = $"No valid constructors found for {type}";
                    return false;
                }

                var constructorParameters = Array.Empty<object>();

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
                        foreach (var constructor in constructors)
                        {
                            var parameterInfos = constructor.GetParameters();
                            if (parameterInfos.All(static p => p.IsOptional) && constructor.IsPublic)
                            {
                                constructorParameters = new object[parameterInfos.Length];
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        problemMessage = $"No valid constructors found for type {type}";
                        return false;
                    }
                }

                var obj = Activator.CreateInstance(type, constructorParameters)!;

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

                converted = obj;
                return true;
            }

            try
            {
                converted = System.Convert.ChangeType(value, type, formatProvider);
                return true;
            }
            catch (Exception e)
            {
                // check if we can do a cast with operator overloading
                if (TryCastWithOperators(value, type, valueType, out var invoke))
                {
                    converted = invoke;
                    return true;
                }

                if (propagateException && !_engine.Options.Interop.ExceptionHandler(e))
                {
                    throw;
                }

                problemMessage = e.Message;
                return false;
            }
        }

        private Delegate BuildDelegate(Type type, Func<JsValue, JsValue[], JsValue> function)
        {
            var method = type.GetMethod("Invoke");
            var arguments = method!.GetParameters();

            var parameters = new ParameterExpression[arguments.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                parameters[i] = Expression.Parameter(arguments[i].ParameterType, arguments[i].Name);
            }

            var initializers = new List<MethodCallExpression>(parameters.Length);

            for (var i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                if (param.Type.IsValueType)
                {
                    var boxing = Expression.Convert(param, objectType);
                    initializers.Add(Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), boxing));
                }
                else if (param.Type.IsArray &&
                         arguments[i].GetCustomAttribute<ParamArrayAttribute>() is not null &&
                         function.Target is FunctionInstance instance)
                {
                    for (var j = 0; j < instance.Length; j++)
                    {
                        var returnLabel = Expression.Label(typeof(object));
                        var checkIndex = Expression.GreaterThanOrEqual(Expression.Property(param, nameof(Array.Length)), Expression.Constant(j));
                        var condition = Expression.IfThen(checkIndex, Expression.Return(returnLabel, Expression.ArrayAccess(param, Expression.Constant(j))));
                        var block = Expression.Block(condition, Expression.Label(returnLabel, Expression.Constant(JsValue.Undefined)));

                        initializers.Add(Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), block));
                    }
                }
                else
                {
                    initializers.Add(Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), param));
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
                    return converted is not null;
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
