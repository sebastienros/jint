using Jint.Native;
using System.Collections.Concurrent;
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
        // MethodBase.IsGenericMethod ends up in RuntimeMethodHandle::HasMethodInstantiation, a
        // non-trivial reflection call; cache it so the per-call binding path never pays for it.
        IsGenericMethod = method.IsGenericMethod;
        IsGenericMethodDefinition = method is MethodInfo { IsGenericMethodDefinition: true };
        IsStatic = method.IsStatic;
        DeclaringType = method.DeclaringType;
        ReturnType = (method as MethodInfo)?.ReturnType;

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

        ParameterFlags = Parameters.Length == 0 ? [] : new InteropParameterFlags[Parameters.Length];
        for (var i = 0; i < Parameters.Length; i++)
        {
            ParameterFlags[i] = ComputeParameterFlags(Parameters[i].ParameterType);
        }
    }

    public MethodBase Method { get; }
    public ParameterInfo[] Parameters { get; }
    public bool HasParams { get; }
    public int ParameterDefaultValuesCount { get; }
    public bool IsExtensionMethod { get; }

    /// <summary>
    /// Cached <see cref="MethodBase.IsGenericMethod"/> — the reflection property is a per-call
    /// hotspot in argument binding, so it is computed once in the constructor.
    /// </summary>
    public bool IsGenericMethod { get; }

    /// <summary>
    /// Cached "<see cref="Method"/> is a <see cref="MethodInfo"/> whose
    /// <see cref="MethodBase.IsGenericMethodDefinition"/> is true" — same rationale as
    /// <see cref="IsGenericMethod"/>, it is read per call while resolving a generic method.
    /// </summary>
    public bool IsGenericMethodDefinition { get; }

    /// <summary>
    /// Cached <see cref="MethodBase.IsStatic"/> — read on every call to decide whether the receiver
    /// has to be type-checked, and the virtual reflection property shows up in interop call profiles.
    /// </summary>
    public bool IsStatic { get; }

    /// <summary>
    /// Cached <see cref="MemberInfo.DeclaringType"/> — read on every call to validate the receiver.
    /// </summary>
    public Type? DeclaringType { get; }

    /// <summary>
    /// Cached <see cref="MethodInfo.ReturnType"/>, or <see langword="null"/> when <see cref="Method"/>
    /// is a <see cref="ConstructorInfo"/> — read on every call to map the CLR result back to a
    /// <see cref="JsValue"/>.
    /// </summary>
    public Type? ReturnType { get; }

    /// <summary>
    /// Facts about each parameter type that argument binding consults on every call — computed
    /// once so the per-call loop avoids reflection checks (and the boxing ToObject detour that
    /// only generic-shaped parameters need). Valid for <see cref="Parameters"/> only; a resolved
    /// generic method's parameters must be re-classified with <see cref="ComputeParameterFlags"/>.
    /// </summary>
    public InteropParameterFlags[] ParameterFlags { get; }

    public static InteropParameterFlags ComputeParameterFlags(Type parameterType)
    {
        var flags = InteropParameterFlags.None;
        if (typeof(JsValue).IsAssignableFrom(parameterType))
        {
            flags |= InteropParameterFlags.JsValueAssignable;
        }
        if (parameterType.IsGenericParameter || parameterType.IsGenericType)
        {
            flags |= InteropParameterFlags.GenericLike;
        }
        if (parameterType == typeof(JsValue[]))
        {
            flags |= InteropParameterFlags.JsValueArray;
        }
        return flags;
    }

#if NET8_0_OR_GREATER
    // Process-wide L2 caches for the invoker machinery, keyed by the reflected member.
    //
    // MethodDescriptor instances are per-Engine: TypeResolver.GetAccessor caches the
    // ReflectionAccessor that owns them in Engine._reflectionAccessors. With a purely per-instance
    // cache every new Engine therefore re-emitted an invoke stub and re-compiled the whole
    // expression tree for every host method it called, which dominates the common
    // fresh-Engine-per-operation embedding pattern.
    //
    // Keying on the MethodBase is sound because every input the builders consume is derived purely
    // from that MethodBase - CompiledMethodInvoker.TryBuild reads Method, Parameters
    // (== method.GetParameters()), HasParams / ParameterDefaultValuesCount (parameter attributes),
    // IsGenericMethod and IsExtensionMethod, all computed from the MethodBase in the constructor -
    // and the delegate it produces closes over nothing Engine-specific: only the open invocation
    // delegate created from the same MethodBase, the JsValue.Undefined / JsValue.Null process-wide
    // singletons, the public JsValue implicit operators and JsValueExtensions accessors. The
    // Engine-affine policy decisions (custom object converters, a custom ITypeConverter, receiver
    // type checks) live at the call site in MethodInfoFunction.Call and gate *use* of the invoker,
    // never its construction, so one Engine's policy can never leak into another through this cache.
    //
    // Trade-off: a static cache keyed by MethodBase pins that MethodBase - and therefore its
    // declaring assembly - for the lifetime of the process. That matches the precedent already set
    // by this codebase's other process-wide reflection caches (TypeDescriptor._cache,
    // TypeReference._memberAccessors, JintBinaryExpression._knownOperators,
    // DefaultTypeConverter._knownCastOperators).
    //
    // A concurrent duplicate build for the same key is benign (both produce equivalent invokers and
    // one is discarded), matching the existing lazy-init contract; the dictionaries themselves are
    // thread-safe.
    private static readonly ConcurrentDictionary<MethodInfo, MethodInvoker> _sharedMethodInvokers = new();
    private static readonly ConcurrentDictionary<ConstructorInfo, ConstructorInvoker> _sharedConstructorInvokers = new();

    // A null value is the "known ineligible" sentinel so an ineligible method is never re-probed.
    private static readonly ConcurrentDictionary<MethodBase, CompiledMethodInvoker.Invoker?> _sharedCompiledInvokers = new();

    // Per-instance L1 caches over the shared dictionaries, so the steady-state per-call path stays a
    // plain field read with no dictionary probe - only a descriptor's first miss consults L2.
    // Benign race - last writer wins.
    private MethodInvoker? _methodInvoker;
    private ConstructorInvoker? _constructorInvoker;

    // lazily built strongly-typed invoker for the exact-type numeric/string/bool fast lane.
    // Once _compiledInvokerUnavailable is set the method is permanently ineligible (or the runtime
    // cannot JIT the lambda) and we never retry.
    private CompiledMethodInvoker.Invoker? _compiledInvoker;
    private bool _compiledInvokerUnavailable;

    /// <summary>
    /// Returns a compiled delegate that binds and invokes this method without the per-call
    /// <c>object?[]</c> parameter array, argument boxing, boxed return, and return-mapper lookup —
    /// or <see langword="null"/> when the method is ineligible or the runtime cannot compile the
    /// delegate to native code. Only usable for single-candidate call sites.
    /// </summary>
    internal CompiledMethodInvoker.Invoker? GetCompiledInvoker()
    {
        if (_compiledInvoker is { } invoker)
        {
            return invoker;
        }

        if (_compiledInvokerUnavailable)
        {
            return null;
        }

        var built = _sharedCompiledInvokers.GetOrAdd(
            Method,
            static (_, descriptor) =>
            {
                // Build the delegate ONLY when the runtime will JIT it to native code. Under AOT and
                // under an interpreted-only Expression.Compile (e.g. Mono interpreter)
                // IsDynamicCodeCompiled is false, and an interpreted lambda is slower than the
                // cached MethodInvoker reflection path, so we decline and keep that path.
                if (!RuntimeFeature.IsDynamicCodeCompiled
                    || !CompiledMethodInvoker.TryBuild(descriptor, out var compiled))
                {
                    return null;
                }

                return compiled;
            },
            this);

        if (built is null)
        {
            _compiledInvokerUnavailable = true;
            return null;
        }

        _compiledInvoker = built;
        return built;
    }
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
                // MethodInvoker is thread-safe and designed for reuse (the BCL itself keeps one per
                // MethodInfo), so the emitted invoke stub is shared process-wide instead of being
                // re-emitted for every Engine that happens to call this method.
                var invoker = _methodInvoker ??= _sharedMethodInvokers.GetOrAdd(methodInfo, static m => MethodInvoker.Create(m));
                return invoker.Invoke(instance, parameters);
            }

            if (Method is ConstructorInfo constructorInfo)
            {
                var invoker = _constructorInvoker ??= _sharedConstructorInvokers.GetOrAdd(constructorInfo, static c => ConstructorInvoker.Create(c));
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
            if (d1.IsGenericMethod && !d2.IsGenericMethod)
            {
                return 1;
            }

            if (d2.IsGenericMethod && !d1.IsGenericMethod)
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
        object?[] parameters = arguments.Length == 0 ? [] : new object?[arguments.Length];
        var methodParameters = Parameters;
        var parameterFlags = ParameterFlags;
        var valueCoercionType = engine.Options.Interop.ValueCoercion;

        try
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                var methodParameter = methodParameters[i];
                var parameterType = methodParameter.ParameterType;
                var value = arguments[i];
                object? converted;

                if ((parameterFlags[i] & InteropParameterFlags.JsValueAssignable) != InteropParameterFlags.None)
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
            return JsValue.FromObject(engine, retVal);
        }
        catch (TargetInvocationException exception)
        {
            Throw.MeaningfulException(engine, exception);
            return null;
        }
    }
}

/// <summary>
/// Per-parameter-type facts consulted by interop argument binding on every call.
/// </summary>
[Flags]
internal enum InteropParameterFlags : byte
{
    None = 0,

    /// <summary>The parameter accepts a JsValue directly, no conversion needed.</summary>
    JsValueAssignable = 1,

    /// <summary>Generic parameter or generic type — the only shapes the generic-binding probe can match.</summary>
    GenericLike = 2,

    /// <summary>The parameter is exactly JsValue[] (the params JsValue[] host signature).</summary>
    JsValueArray = 4,
}
