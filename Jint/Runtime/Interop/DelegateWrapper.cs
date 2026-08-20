using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Function;

#pragma warning disable IL2072
#pragma warning disable IL3050

namespace Jint.Runtime.Interop;

/// <summary>
/// Represents a FunctionInstance wrapper around a CLR method. This is used by user to pass
/// custom methods to the engine.
/// </summary>
internal sealed class DelegateWrapper : Function
{
    private static readonly JsString _name = new JsString("delegate");

    private readonly Delegate _d;

    // Signature metadata is resolved once per target method and then read straight off the wrapper,
    // so neither construction nor invocation re-reflects. Hosts routinely register tens of delegates
    // per engine, and the previous shape paid GetParameters() (a defensive ParameterInfo[] clone)
    // once per construction plus once per *call*.
    private readonly DelegateMetadata _metadata;

#if NET8_0_OR_GREATER
    // Strongly-typed invocation lane; null when the runtime cannot compile one, in which case the
    // reflection-based DynamicInvoke path is kept.
    private readonly CompiledDelegateInvoker.Invoker? _invoker;

    // The same lane for a delegate of exactly one or two parameters, taking its arguments in registers
    // so the arity-specialized call lane needs no argument array at all. At most one is ever non-null,
    // and only when _invoker is too.
    private readonly CompiledDelegateInvoker.Invoker1? _invoker1;
    private readonly CompiledDelegateInvoker.Invoker2? _invoker2;
#endif

    public DelegateWrapper(
        Engine engine, Delegate d)
        : base(engine, engine.Realm, _name, FunctionThisMode.Global)
    {
        _d = d;
        _prototype = engine.Realm.Intrinsics.Function.PrototypeObject;
        _metadata = DelegateMetadata.For(d.Method);

#if NET8_0_OR_GREATER
        var compiled = CompiledDelegateInvoker.For(d.GetType());
        if (ThunkBindsTheTargetSignature(in compiled, d.Method))
        {
            _invoker = compiled.Invoke;
            _invoker1 = compiled.Invoke1;
            _invoker2 = compiled.Invoke2;
        }
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Whether the thunk built for the delegate <em>type</em> may be used to run this delegate's target
    /// method — that is, whether the signature it binds is the one the argument list is built to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The thunk indexes the argument array positionally and casts each element to the delegate type's own
    /// <c>Invoke</c> parameter type, while <see cref="BindArguments"/> converts to the <em>target method's</em>
    /// parameter types. The CLR does not require those to agree: relaxed delegate binding accepts a target
    /// whose parameter is a base type of the delegate's — an <c>Action&lt;string&gt;</c> over a
    /// <c>void M(object)</c> — and a return type the other way round. Conversion would then hand over a value
    /// the target accepts and the thunk's cast rejects, turning what the reflection binder reports as an
    /// <see cref="ArgumentException"/> against the delegate signature into a cast failure surfaced as a host
    /// error. Only an exact element-wise match keeps the two lanes indistinguishable.
    /// </para>
    /// <para>
    /// Arity is part of the same check: an open-instance delegate declares the receiver as an <c>Invoke</c>
    /// parameter while the <see cref="MethodInfo"/> behind it does not. Such a delegate cannot be bound by
    /// this wrapper at all, and keeping it on the reflection path preserves the exception it already produced.
    /// </para>
    /// </remarks>
    private bool ThunkBindsTheTargetSignature(in CompiledDelegateInvoker.CompiledInvoker compiled, MethodInfo target)
    {
        if (compiled.Invoke is null)
        {
            return false;
        }

        var invokeParameterTypes = compiled.ParameterTypes!;
        var targetParameters = _metadata.Parameters;
        if (invokeParameterTypes.Length != targetParameters.Length || compiled.ReturnType != target.ReturnType)
        {
            return false;
        }

        for (var i = 0; i < targetParameters.Length; i++)
        {
            if (invokeParameterTypes[i] != targetParameters[i].ParameterType)
            {
                return false;
            }
        }

        return true;
    }
#endif

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Invoke(BindArguments(arguments));
    }

    /// <summary>
    /// A host delegate runs arbitrary embedder code, so the frameless lane is never available: it can
    /// raise a JavaScript error and it can re-enter the interpreter, both of which observe the
    /// call-stack frame through <c>error.stack</c>. Only the arity-specialized lane is offered, and
    /// only for the shapes where an argument list of <paramref name="argumentCount"/> binds exactly
    /// like the positional form.
    /// </summary>
    /// <remarks>
    /// <see cref="CallFast"/> receives a fixed pair of argument slots and no count, so eligibility has
    /// to be decided from <paramref name="argumentCount"/> alone. With no params array and at least as
    /// many JavaScript arguments as the delegate has parameters, binding consumes exactly the declared
    /// parameters and ignores the rest, which is precisely what the two slots supply. Fewer arguments
    /// than parameters would substitute CLR defaults for the missing ones, which is observably
    /// different from converting the <c>undefined</c> that <see cref="CallFast"/> passes, so those
    /// arities decline.
    /// </remarks>
    internal override FastCallShape GetFastCallShape(int argumentCount)
    {
        var metadata = _metadata;
        if (metadata.HasParamsArray
            || metadata.Parameters.Length > 2
            || argumentCount < metadata.Parameters.Length)
        {
            return default;
        }

        return new FastCallShape(Supported: true, Leaf: false, Variadic: false, FastCallGuard.Any, FastCallGuard.Any, FastCallGuard.Any);
    }

    /// <remarks>
    /// The arity is fixed per delegate type, so on this lane the argument array exists only to satisfy the
    /// thunk's own signature. Where a register-passing thunk was built for that arity, the converted values
    /// go straight into it and no array is allocated. Everything else — the conversions, the exact-type
    /// check that decides between the compiled and the reflection lane, and the exceptions either produces —
    /// is unchanged; only the shapes that stay on <see cref="Delegate.DynamicInvoke"/>, which takes the
    /// array, still build one.
    /// </remarks>
    internal override JsValue CallFast(JsValue thisObject, JsValue arg0, JsValue arg1)
    {
        var parameterMetadata = _metadata.Parameters;
        var count = parameterMetadata.Length;
        if (count == 0)
        {
            return Invoke([]);
        }

        var converter = Engine.TypeConverter;
        var valueCoercionType = Engine.Options.Interop.ValueCoercion;

        // GetFastCallShape only offers this lane for one or two parameters, so element 0 always exists:
        // take a bounds-check-free reference to it (MA0212) and reach element 1 through Unsafe.Add.
        ref var parameter0 = ref MemoryMarshal.GetReference(parameterMetadata.AsSpan());

        var converted0 = Convert(in parameter0, arg0, converter, valueCoercionType);
        if (count == 1)
        {
#if NET8_0_OR_GREATER
            var invoker1 = _invoker1;
            if (invoker1 is not null && DelegateMetadata.CanBind(in parameter0, converted0))
            {
                return InvokeInRegisters(invoker1, converted0);
            }
#endif
            return Invoke([converted0]);
        }

        ref var parameter1 = ref Unsafe.Add(ref parameter0, 1);
        var converted1 = Convert(in parameter1, arg1, converter, valueCoercionType);
#if NET8_0_OR_GREATER
        var invoker2 = _invoker2;
        if (invoker2 is not null
            && DelegateMetadata.CanBind(in parameter0, converted0)
            && DelegateMetadata.CanBind(in parameter1, converted1))
        {
            return InvokeInRegisters(invoker2, converted0, converted1);
        }
#endif
        return Invoke([converted0, converted1]);
    }

    private object?[] BindArguments(JsCallArguments arguments)
    {
        var metadata = _metadata;
        var parameterMetadata = metadata.Parameters;

        var delegateArgumentsCount = parameterMetadata.Length;
        if (delegateArgumentsCount == 0)
        {
            return [];
        }

        var delegateNonParamsArgumentsCount = metadata.HasParamsArray ? delegateArgumentsCount - 1 : delegateArgumentsCount;

        var jsArgumentsCount = arguments.Length;
        var jsArgumentsWithoutParamsCount = Math.Min(jsArgumentsCount, delegateNonParamsArgumentsCount);

        var converter = Engine.TypeConverter;
        var valueCoercionType = Engine.Options.Interop.ValueCoercion;
        var parameters = new object?[delegateArgumentsCount];

        // convert non params parameter to expected types
        for (var i = 0; i < jsArgumentsWithoutParamsCount; i++)
        {
            parameters[i] = Convert(in parameterMetadata[i], arguments[i], converter, valueCoercionType);
        }

        // assign null to parameters not provided
        for (var i = jsArgumentsWithoutParamsCount; i < delegateNonParamsArgumentsCount; i++)
        {
            var parameter = parameterMetadata[i];
            parameters[i] = parameter.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
        }

        // assign params to array and converts each object to expected type
        if (metadata.HasParamsArray)
        {
            var paramsArgumentIndex = delegateArgumentsCount - 1;
            var paramsCount = Math.Max(0, jsArgumentsCount - delegateNonParamsArgumentsCount);

            var paramsParameterType = metadata.ParamsElementType!;

            // A `params JsValue[]` tail takes its arguments straight through, so it can be built and
            // filled as the typed array it is. Array.CreateInstance goes through the runtime's general
            // array factory to produce the very same JsValue[], and Array.SetValue then re-derives the
            // element type and re-checks assignability once per element - both to store a value the
            // C# type system already proved fits. Every other element type keeps the general path,
            // where SetValue's widening conversions are part of the behaviour.
            if (metadata.ParamsElementIsJsValue)
            {
                var typedParams = new JsValue[paramsCount];
                for (var i = paramsArgumentIndex; i < jsArgumentsCount; i++)
                {
                    // typedParams is exactly JsValue[] by construction, so the covariant store check
                    // the CLR would otherwise emit is provably redundant
                    Arguments.WriteNoTypeCheck(typedParams, i - paramsArgumentIndex, arguments[i]);
                }

                parameters[paramsArgumentIndex] = typedParams;
                return parameters;
            }

            var paramsParameter = Array.CreateInstance(paramsParameterType, paramsCount);

            for (var i = paramsArgumentIndex; i < jsArgumentsCount; i++)
            {
                var paramsIndex = i - paramsArgumentIndex;
                var value = arguments[i];

                if (!ReflectionExtensions.TryConvertViaTypeCoercion(paramsParameterType, valueCoercionType, value, out var converted))
                {
                    converted = converter.Convert(
                        value.ToObject(),
                        paramsParameterType,
                        CultureInfo.InvariantCulture);
                }

                paramsParameter.SetValue(converted, paramsIndex);
            }

            parameters[paramsArgumentIndex] = paramsParameter;
        }

        return parameters;
    }

    private static object? Convert(
        in ParameterMetadata parameter,
        JsValue value,
        ITypeConverter converter,
        ValueCoercionType valueCoercionType)
    {
        if (parameter.IsJsValue)
        {
            return value;
        }

        var parameterType = parameter.ParameterType;
        if (ReflectionExtensions.TryConvertViaTypeCoercion(parameterType, valueCoercionType, value, out var converted))
        {
            return converted;
        }

        return converter.Convert(value.ToObject(), parameterType, CultureInfo.InvariantCulture);
    }

    private JsValue Invoke(object?[] parameters)
    {
        object? result;
#if NET8_0_OR_GREATER
        // The compiled thunk emits an unbox/castclass per argument, which - unlike the reflection
        // binder - coerces nothing, so it may only run for an exactly-typed argument list.
        var invoker = _invoker;
        var viaCompiledLane = invoker is not null && _metadata.CanBind(parameters);
#endif
        try
        {
            using var suspension = Engine.SuspendHostCallForCallbacks(
                _metadata.MayReceiveHostCallback && DefaultTypeConverter.ContainsHostCallback(parameters));
#if NET8_0_OR_GREATER
            result = viaCompiledLane ? invoker!(_d, parameters) : _d.DynamicInvoke(parameters);
#else
            result = _d.DynamicInvoke(parameters);
#endif
        }
#if NET8_0_OR_GREATER
        // The compiled lane calls the delegate directly, so a host exception arrives unwrapped; the
        // DynamicInvoke lane keeps its historical filter so only target exceptions (which it wraps)
        // are intercepted and a binder failure still surfaces as-is.
        catch (Exception exception) when (viaCompiledLane || exception is TargetInvocationException)
#else
        catch (TargetInvocationException exception)
#endif
        {
            ThrowHostCallFailure(exception);
            throw;
        }

        return CompleteHostCall(result);
    }

#if NET8_0_OR_GREATER
    private JsValue InvokeInRegisters(CompiledDelegateInvoker.Invoker1 invoker, object? argument0)
    {
        object? result;
        try
        {
            using var suspension = Engine.SuspendHostCallForCallbacks(
                _metadata.MayReceiveHostCallback && DefaultTypeConverter.IsHostCallback(argument0));
            result = invoker(_d, argument0);
        }
        catch (Exception exception)
        {
            ThrowHostCallFailure(exception);
            throw;
        }

        return CompleteHostCall(result);
    }

    private JsValue InvokeInRegisters(CompiledDelegateInvoker.Invoker2 invoker, object? argument0, object? argument1)
    {
        object? result;
        try
        {
            using var suspension = Engine.SuspendHostCallForCallbacks(
                _metadata.MayReceiveHostCallback
                && (DefaultTypeConverter.IsHostCallback(argument0) || DefaultTypeConverter.IsHostCallback(argument1)));
            result = invoker(_d, argument0, argument1);
        }
        catch (Exception exception)
        {
            ThrowHostCallFailure(exception);
            throw;
        }

        return CompleteHostCall(result);
    }
#endif

    /// <summary>
    /// A throwing host call never reaches the post-invoke boundary check, so it is re-checked here: a loop
    /// of throwing host calls must not stretch the constraint-detection window either.
    /// </summary>
    [DoesNotReturn]
    private void ThrowHostCallFailure(Exception exception)
    {
        Engine.CheckAmortizedConstraintsAtHostBoundary();
        Throw.MeaningfulException(Engine, exception as TargetInvocationException ?? new TargetInvocationException(exception));
    }

    private JsValue CompleteHostCall(object? result)
    {
        // an awaitable result must reach promise conversion before a constraint can throw,
        // so that the in-flight Task gets a continuation attached and is never left unobserved
        var returnValue = IsAwaitable(result)
            ? ConvertAwaitableToPromise(Engine, result!)
            : FromObject(Engine, result);
        Engine.CheckAmortizedConstraintsAtHostBoundary();
        return returnValue;
    }

    private static bool IsAwaitable(object? obj)
    {
        if (obj is null)
        {
            return false;
        }
        if (obj is Task)
        {
            return true;
        }
#if !NETFRAMEWORK && !NETSTANDARD2_0
        if (obj is ValueTask)
        {
            return true;
        }

        // ValueTask<T> is not derived from ValueTask, so we need to check for it explicitly
        var type = obj.GetType();
        if (!type.IsGenericType)
        {
            return false;
        }

        return type.GetGenericTypeDefinition() == typeof(ValueTask<>);
#else
        return false;
#endif
    }
}

/// <summary>
/// One parameter of a wrapped delegate, with the per-call reflection questions
/// (<c>typeof(JsValue).IsAssignableFrom(...)</c>, <c>IsValueType</c>, the boxed representation of a
/// <see cref="Nullable{T}"/>) answered up front.
/// </summary>
/// <param name="ParameterType">The declared parameter type.</param>
/// <param name="BoxedType">
/// The runtime type an argument for this parameter is boxed as — <c>T</c> rather than
/// <c>Nullable&lt;T&gt;</c>, since a nullable never survives boxing.
/// </param>
/// <param name="IsJsValue">Whether the parameter takes a <see cref="JsValue"/> straight through.</param>
/// <param name="IsValueType">Whether the parameter is a value type, which an argument must match exactly.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ParameterMetadata(Type ParameterType, Type BoxedType, bool IsJsValue, bool IsValueType);

/// <summary>
/// Signature metadata for a wrapped delegate's target method: everything <see cref="DelegateWrapper"/>
/// needs in order to bind JavaScript arguments, resolved once instead of on every construction and
/// every call.
/// </summary>
internal sealed class DelegateMetadata
{
    // Keyed on the target MethodInfo rather than on the Delegate instance: a capturing lambda produces
    // a brand new Delegate for every registration but always reports the same compiler-generated
    // MethodInfo, so an instance-keyed cache would never hit. Everything stored here derives purely
    // from that MethodInfo and holds no engine state, which is what makes one process-wide cache sound
    // across engines. The trade-off matches the other reflection caches in this namespace: an entry
    // pins the MethodInfo, and therefore its declaring assembly, for the process lifetime. A concurrent
    // duplicate build is benign — both produce equivalent metadata and one is discarded.
    private static readonly ConcurrentDictionary<MethodInfo, DelegateMetadata> _cache = new();

    internal static DelegateMetadata For(MethodInfo method)
        => _cache.GetOrAdd(method, static m => new DelegateMetadata(m));

    private DelegateMetadata(MethodInfo method)
    {
        var parameterInfos = method.GetParameters();

#if NETFRAMEWORK
        if (parameterInfos.Length > 0 && parameterInfos[0].ParameterType == typeof(System.Runtime.CompilerServices.Closure))
        {
            var reducedLength = parameterInfos.Length - 1;
            var reducedParameterInfos = new ParameterInfo[reducedLength];
            Array.Copy(parameterInfos, 1, reducedParameterInfos, 0, reducedLength);
            parameterInfos = reducedParameterInfos;
        }
#endif

        ParameterMetadata[] parameters = parameterInfos.Length == 0 ? [] : new ParameterMetadata[parameterInfos.Length];
        var hasParamsArray = false;
        var mayReceiveHostCallback = false;
        for (var i = 0; i < parameterInfos.Length; i++)
        {
            var parameterInfo = parameterInfos[i];
            var parameterType = parameterInfo.ParameterType;
            parameters[i] = new ParameterMetadata(
                parameterType,
                BoxedType: Nullable.GetUnderlyingType(parameterType) ?? parameterType,
                IsJsValue: typeof(JsValue).IsAssignableFrom(parameterType),
                IsValueType: parameterType.IsValueType);

            hasParamsArray |= Attribute.IsDefined(parameterInfo, typeof(ParamArrayAttribute));
            mayReceiveHostCallback |= typeof(Delegate).IsAssignableFrom(parameterType)
                || (parameterType.IsArray && typeof(Delegate).IsAssignableFrom(parameterType.GetElementType()!));
        }

        Parameters = parameters;
        HasParamsArray = hasParamsArray;
        MayReceiveHostCallback = mayReceiveHostCallback;

        if (hasParamsArray)
        {
            ParamsElementType = parameterInfos[parameterInfos.Length - 1].ParameterType.GetElementType();
            ParamsElementIsJsValue = ParamsElementType == typeof(JsValue);
        }
    }

    internal ParameterMetadata[] Parameters { get; }

    internal bool HasParamsArray { get; }

    internal bool MayReceiveHostCallback { get; }

    /// <summary>Element type of the trailing params array; only meaningful when <see cref="HasParamsArray"/>.</summary>
    internal Type? ParamsElementType { get; }

    internal bool ParamsElementIsJsValue { get; }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Whether <paramref name="parameters"/> may be handed to a strongly-typed invoker, whose emitted
    /// unbox/castclass per argument is stricter than the reflection binder: the binder accepts any box
    /// it knows how to coerce to the parameter type, the emitted unbox demands the exact type.
    /// </summary>
    /// <remarks>
    /// Everything the built-in conversion path produces satisfies that — it boxes as the declared
    /// member type — but two things reach here that it does not control: the path ends in an
    /// <see cref="ITypeConverter"/> a host may replace with one that returns whatever it likes, and a
    /// <see cref="JsValue"/>-typed parameter takes its argument straight through unconverted, so a
    /// parameter narrower than <see cref="JsValue"/> can be handed a value it does not accept. Both
    /// keep the reflection path, whose <see cref="ArgumentException"/> is the outcome they already
    /// produced; taking the compiled lane would instead surface a cast failure as a host error.
    /// </remarks>
    internal bool CanBind(object?[] parameters)
    {
        var parameterMetadata = Parameters;
        for (var i = 0; i < parameterMetadata.Length; i++)
        {
            if (!CanBind(in parameterMetadata[i], parameters[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc cref="CanBind(object?[])"/>
    /// <remarks>
    /// The single-parameter form, so a caller holding its converted arguments in locals can ask the same
    /// question without materializing an array to ask it through.
    /// </remarks>
    internal static bool CanBind(in ParameterMetadata parameter, object? value)
    {
        if (value is null)
        {
            // a castclass accepts null and so does a Nullable<T> conversion; a plain unbox does not
            return !parameter.IsValueType || !ReferenceEquals(parameter.BoxedType, parameter.ParameterType);
        }

        if (ReferenceEquals(value.GetType(), parameter.BoxedType))
        {
            return true;
        }

        return !parameter.IsValueType && parameter.ParameterType.IsInstanceOfType(value);
    }
#endif
}
