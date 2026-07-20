using Jint.Native;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Jint.Extensions;

#pragma warning disable IL2072

namespace Jint.Runtime.Interop;

internal sealed class MethodDescriptor
{
    internal MethodDescriptor(MethodBase method)
    {
        Method = method;
        Parameters = method.GetParameters();
        IsExtensionMethod = method.IsDefined(typeof(ExtensionAttribute), true);

        foreach (var parameter in Parameters)
        {
            if (Attribute.IsDefined(parameter, typeof(ParamArrayAttribute)))
            {
                HasParams = true;
                break;
            }

            if (parameter.HasDefaultValue)
            {
                ParameterDefaultValuesCount++;
            }
        }
    }

    public MethodBase Method { get; }
    public ParameterInfo[] Parameters { get; }
    public bool HasParams { get; }
    public int ParameterDefaultValuesCount { get; }
    public bool IsExtensionMethod { get; }

#if NET8_0_OR_GREATER
    // lazily initialized fast invokers, benign race - last writer wins
    private MethodInvoker? _methodInvoker;
    private ConstructorInvoker? _constructorInvoker;
#endif

    /// <summary>
    /// Invokes the method using a cached fast invoker when available. Target exceptions are
    /// normalized to <see cref="TargetInvocationException"/> to match MethodBase.Invoke behavior.
    /// </summary>
    public object? Invoke(object? instance, object?[] parameters)
    {
#if NET8_0_OR_GREATER
        return Invoke(instance, parameters.AsSpan());
#else
        return Method switch
        {
            MethodInfo m => m.Invoke(instance, parameters),
            ConstructorInfo c => c.Invoke(parameters),
            _ => throw new NotSupportedException("Method is unknown type"),
        };
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Invokes the method using a cached fast invoker; the span may be a slice of a pooled,
    /// oversized buffer. Target exceptions are normalized to <see cref="TargetInvocationException"/>.
    /// </summary>
    public object? Invoke(object? instance, Span<object?> parameters)
    {
        // MethodInvoker/ConstructorInvoker don't perform Type.Missing default-value
        // substitution, fall back to MethodBase.Invoke when optional arguments are elided
        if (ParameterDefaultValuesCount > 0)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                if (ReferenceEquals(parameters[i], System.Type.Missing))
                {
                    return Method switch
                    {
                        MethodInfo m => m.Invoke(instance, parameters.ToArray()),
                        ConstructorInfo c => c.Invoke(parameters.ToArray()),
                        _ => throw new NotSupportedException("Method is unknown type"),
                    };
                }
            }
        }

        try
        {
            if (Method is MethodInfo methodInfo)
            {
                var invoker = _methodInvoker ??= MethodInvoker.Create(methodInfo);
                return invoker.Invoke(instance, parameters);
            }

            if (Method is ConstructorInfo constructorInfo)
            {
                var invoker = _constructorInvoker ??= ConstructorInvoker.Create(constructorInfo);
                return invoker.Invoke(parameters);
            }
        }
        catch (Exception e) when (e is not TargetInvocationException)
        {
            // MethodBase.Invoke wraps exceptions thrown by the invoked member, the fast
            // invokers rethrow them as-is - normalize so callers see the legacy shape
            throw new TargetInvocationException(e);
        }

        throw new NotSupportedException("Method is unknown type");
    }
#endif

    public static MethodDescriptor[] Build<T>(List<T> source) where T : MethodBase
    {
        var descriptors = new MethodDescriptor[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            descriptors[i] = new MethodDescriptor(source[i]);
        }

        return Prioritize(descriptors);
    }

    public static MethodDescriptor[] Build<T>(T[] source) where T : MethodBase
    {
        var descriptors = new MethodDescriptor[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            descriptors[i] = new MethodDescriptor(source[i]);
        }

        return Prioritize(descriptors);
    }

    private static MethodDescriptor[] Prioritize(MethodDescriptor[] descriptors)
    {
        static int CreateComparison(MethodDescriptor d1, MethodDescriptor d2)
        {
            // if its a generic method, put it on the end
            if (d1.Method.IsGenericMethod && !d2.Method.IsGenericMethod)
            {
                return 1;
            }

            if (d2.Method.IsGenericMethod && !d1.Method.IsGenericMethod)
            {
                return -1;
            }

            // put params versions to end, they can be tricky to match and can cause trouble / extra overhead
            if (d1.HasParams && !d2.HasParams)
            {
                return 1;
            }

            if (d2.HasParams && !d1.HasParams)
            {
                return -1;
            }

            // then favor less parameters
            if (d1.Parameters.Length > d2.Parameters.Length)
            {
                return 1;
            }

            if (d2.Parameters.Length > d1.Parameters.Length)
            {
                return -1;
            }

            return 0;
        }

        Array.Sort(descriptors, CreateComparison);

        return descriptors;
    }

    /// <summary>
    /// Renders a C#-like signature for error messages, e.g. "Say(String message)" or
    /// "PersonExtensions.FizzBuzz(this Person person)". Cold path only.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Method is ConstructorInfo)
        {
            sb.Append(GetTypeName(Method.DeclaringType!));
        }
        else
        {
            if (IsExtensionMethod)
            {
                sb.Append(GetTypeName(Method.DeclaringType!)).Append('.');
            }

            sb.Append(Method.Name);

            if (Method.IsGenericMethodDefinition)
            {
                sb.Append('<');
                var genericArguments = Method.GetGenericArguments();
                for (var i = 0; i < genericArguments.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append(genericArguments[i].Name);
                }
                sb.Append('>');
            }
        }

        sb.Append('(');
        for (var i = 0; i < Parameters.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var parameter = Parameters[i];
            if (i == 0 && IsExtensionMethod)
            {
                sb.Append("this ");
            }
            else if (HasParams && i == Parameters.Length - 1 && parameter.ParameterType.IsArray)
            {
                sb.Append("params ");
            }

            sb.Append(GetTypeName(parameter.ParameterType)).Append(' ').Append(parameter.Name);

            if (parameter.HasDefaultValue)
            {
                sb.Append(" = ").Append(parameter.DefaultValue ?? "null");
            }
        }
        sb.Append(')');

        return sb.ToString();
    }

    private static string GetTypeName(Type type)
    {
        if (type.IsArray)
        {
            return GetTypeName(type.GetElementType()!) + "[]";
        }

        if (Nullable.GetUnderlyingType(type) is { } underlyingType)
        {
            return GetTypeName(underlyingType) + "?";
        }

        if (type.IsGenericType)
        {
            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            var sb = new StringBuilder(tickIndex > 0 ? name.Substring(0, tickIndex) : name);
            sb.Append('<');
            var genericArguments = type.GetGenericArguments();
            for (var i = 0; i < genericArguments.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(GetTypeName(genericArguments[i]));
            }
            sb.Append('>');
            return sb.ToString();
        }

        return type.Name;
    }

    public JsValue Call(Engine engine, object? instance, JsCallArguments arguments)
    {
        engine.CheckAmortizedConstraintsAtHostBoundary();

        object?[] parameters = arguments.Length == 0 ? [] : new object?[arguments.Length];
        var methodParameters = Parameters;
        var valueCoercionType = engine.Options.Interop.ValueCoercion;

        try
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                var methodParameter = methodParameters[i];
                var parameterType = methodParameter.ParameterType;
                var value = arguments[i];
                object? converted;

                if (typeof(JsValue).IsAssignableFrom(parameterType))
                {
                    converted = value;
                }
                else if (value.IsUndefined() && methodParameter.IsOptional)
                {
                    // undefined is considered missing, null is considered explicit value
                    converted = methodParameter.DefaultValue;
                }
                else if (value is JsNumber jsNumber && InteropHelper.TryConvertNumberFast(jsNumber._value, parameterType, out converted))
                {
                    // common numeric argument converted without the generic converter
                }
                else if (!ReflectionExtensions.TryConvertViaTypeCoercion(parameterType, valueCoercionType, value, out converted))
                {
                    converted = engine.TypeConverter.Convert(
                        value.ToObject(),
                        parameterType,
                        System.Globalization.CultureInfo.InvariantCulture);
                }

                parameters[i] = converted;
            }

            var retVal = Invoke(instance, parameters);
            engine.CheckAmortizedConstraintsAtHostBoundary();
            return JsValue.FromObject(engine, retVal);
        }
        catch (TargetInvocationException exception)
        {
            Throw.MeaningfulException(engine, exception);
            return null;
        }
    }
}
