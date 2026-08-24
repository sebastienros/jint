using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

#pragma warning disable IL2067
#pragma warning disable IL2072
#pragma warning disable IL3050

namespace Jint.Runtime.Interop;

internal sealed class MethodInfoFunction : Function
{
    private readonly Type _targetType;
    private readonly object? _target;
    private readonly string _name;
    private readonly MethodDescriptor[] _methods;
    private readonly Function? _fallbackFunctionInstance;

    public MethodInfoFunction(
        Engine engine,
        Type targetType,
        object? target,
        string name,
        MethodDescriptor[] methods,
        Function? fallbackFunctionInstance = null)
        : base(engine, engine.Realm, new JsString(name))
    {
        _targetType = targetType;
        _target = target;
        _name = name;
        _methods = methods;
        _fallbackFunctionInstance = fallbackFunctionInstance;
        _prototype = engine.Realm.Intrinsics.Function.PrototypeObject;
    }

    private static bool IsGenericParameter(object? argObj, Type parameterType)
    {
        if (argObj is null)
        {
            return false;
        }

        var result = InteropHelper.IsAssignableToGenericType(argObj.GetType(), parameterType);
        if (result.Score < 0)
        {
            return false;
        }

        if (parameterType.IsGenericParameter || parameterType.IsGenericType)
        {
            // For fully concrete generic types (no open type parameters), verify the argument
            // is actually assignable to prevent incorrect direct assignment when type arguments
            // differ (e.g., object[] should not be directly assigned to IList<string>)
            if (!parameterType.ContainsGenericParameters && !parameterType.IsAssignableFrom(argObj.GetType()))
            {
                return false;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Records the type arguments a <em>constructed</em> generic parameter (<c>IEnumerable&lt;T&gt;</c>,
    /// <c>ISelector&lt;TState, TResult&gt;</c>) implies, read off the constructed base/interface the argument
    /// actually matches. These are <em>pins</em>: the matching argument is handed to the method unconverted
    /// (see <see cref="IsGenericParameter"/> and the binding loop in <see cref="TryCall"/>), so only this
    /// exact instantiation can bind and no other parameter may overrule it.
    /// </summary>
    /// <remarks>
    /// When the argument's type implements the same generic interface more than once
    /// (<c>class Multi : IEnumerable&lt;string&gt;, IEnumerable&lt;int&gt;</c>) the pin follows whichever
    /// instantiation <c>Type.GetInterfaces()</c> reports first, which is the same arbitrary choice this
    /// inference has always made for a method whose type arguments come only from such a parameter.
    /// </remarks>
    private static void PinGenericArguments(Type argType, Type parameterType, Type[] pinnedArgTypes)
    {
        var result = InteropHelper.IsAssignableToGenericType(argType, parameterType);
        if (result.Score < 0)
        {
            // e.g. a JS function, whose CLR shape is always JsCallDelegate, probed against Func<T, TResult>:
            // nothing about T or TResult can be learned from it
            return;
        }

        // TPC: maybe we can pull the generic parameters from the arguments?
        var genericArgs = parameterType.GetGenericArguments();
        var givenTypeGenericArgs = result.MatchingGivenType.GetGenericArguments();
        for (var j = 0; j < genericArgs.Length && j < givenTypeGenericArgs.Length; ++j)
        {
            var genericArg = genericArgs[j];
            if (genericArg.IsGenericParameter)
            {
                var position = genericArg.GenericParameterPosition;
                if ((uint) position < (uint) pinnedArgTypes.Length)
                {
                    // a position belonging to the declaring type rather than to the method is out of range here
                    pinnedArgTypes[position] = givenTypeGenericArgs[j];
                }
            }
        }
    }

    private static MethodBase? ResolveMethod(MethodDescriptor descriptor, ParameterInfo[] methodParameters, JsCallArguments arguments)
    {
        var method = descriptor.Method;
        if (!descriptor.IsGenericMethod)
        {
            return method;
        }
        // the cached flag is "Method is a MethodInfo that is a generic method definition", which folds
        // in the MethodInfo cast the reflection path used to repeat here
        if (!descriptor.IsGenericMethodDefinition)
        {
            return method;
        }
        var methodInfo = (MethodInfo) method;

        // TPC: we could also && "(method.Method.IsGenericMethodDefinition)" because we won't create a generic method if that isn't the case
        var methodGenericArgs = method.GetGenericArguments();
        var genericArgTypes = new Type[methodGenericArgs.Length];

        // A bare "T item" parameter only *hints* at its type argument: that argument still goes through
        // ClrTypeConverter on the way in, so it can widen, while a pin cannot - which is why a hint must never
        // overrule a pin (#2987: "includes<T>(this IEnumerable<T>, T item)" on an IEnumerable<object> used to
        // be re-inferred as includes<string> from its argument and then failed to bind its own receiver).
        // Collected separately, and allocated only for the methods that actually have such a parameter.
        Type[]? hintedArgTypes = null;

        for (var i = 0; i < methodParameters.Length; ++i)
        {
            var parameterType = methodParameters[i].ParameterType;
            var isGenericParameter = parameterType.IsGenericParameter;
            if (!isGenericParameter && !parameterType.IsGenericType)
            {
                // nothing to infer from this parameter - and skipping it also skips its ToObject(), which
                // for a JS object literal materializes a CLR object and runs the literal's getters
                continue;
            }

            // an elided optional argument contributes nothing; it must not be inferred from a sentinel
            var argObj = i < arguments.Length ? arguments[i].ToObject() : null;
            if (argObj is null)
            {
                continue;
            }

            if (isGenericParameter)
            {
                // IsAssignableToGenericType answers a constant "score 2, matches the given type" for a bare
                // generic parameter (it is never IsConstructedGenericType), so it is not consulted here
                var position = parameterType.GenericParameterPosition;
                if ((uint) position < (uint) genericArgTypes.Length)
                {
                    // last hint wins, as before: Fancy<T, U>(T, U, T) infers T from its final argument
                    (hintedArgTypes ??= new Type[genericArgTypes.Length])[position] = argObj.GetType();
                }
            }
            else
            {
                PinGenericArguments(argObj.GetType(), parameterType, genericArgTypes);
            }
        }

        for (var i = 0; i < genericArgTypes.Length; ++i)
        {
            // pin, else hint, else object - the last is also how a type argument appearing only in the return
            // type is closed, since "void" cannot be used as a type argument
            genericArgTypes[i] ??= hintedArgTypes?[i] ?? typeof(object);
        }

        try
        {
            return methodInfo.MakeGenericMethod(genericArgTypes);
        }
        catch (ArgumentException)
        {
            // the inferred type arguments violate the method's constraints, so this candidate simply does not
            // apply. Declining lets overload resolution move on, and an exhausted candidate list becomes a
            // catchable TypeError instead of a CLR exception escaping Engine.Evaluate. Deliberately not
            // NotSupportedException, which under NativeAOT means "this instantiation was not rooted" and has
            // to stay diagnosable.
            return null;
        }
    }

    private readonly record struct MethodResolverState(Engine Engine, JsValue This, JsCallArguments Arguments);

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments jsArguments)
    {
        var converter = Engine._typeConverter;
        var thisObj = ResolveThisObject(thisObject);
        var state = new MethodResolverState(_engine, thisObject, jsArguments);

        if (_methods.Length == 1)
        {
            // single candidate, no overload resolution needed - bind directly and skip scoring
            var method = _methods[0];
            var parameterInfos = method.Parameters;
            var arguments = ArgumentProvider(method, in state);
            if (arguments.Length <= parameterInfos.Length
                && arguments.Length >= parameterInfos.Length - method.ParameterDefaultValuesCount
                && CanBindNullArguments(parameterInfos, arguments))
            {
#if NET8_0_OR_GREATER
                // exact-type fast lane: a compiled delegate binds and invokes without the object?[]
                // parameter array, argument boxes, boxed return, and return-mapper lookup. Skipped
                // when custom object converters are registered because those must see return values,
                // with two exceptions. The return types a converter provably cannot observe (void and
                // JsValue, which JsValue.FromObjectWithType short-circuits before ever reaching a
                // converter) keep the lane whatever is registered; and when every registered converter
                // declared the CLR types it handles (see OptionsExtensions.AddObjectConverter), a
                // return type none of them claims keeps it too - the same filter, asked the same
                // question, as the compiled member-read lane in CompilableMemberAccessor. Also skipped
                // when a custom ClrTypeConverter could answer for one of the parameter types, because the
                // slow path consults it for some exact-type conversions (e.g. bool) that the compiled lane
                // performs directly - and, symmetrically, a converter that declared its target types keeps
                // the lane for every method none of them can be the target of. A wrong-typed receiver
                // (extracted method invoked via .call on a foreign this) also declines so the reflection
                // path can surface the receiver mismatch as a TypeError.
                var converterTypeFilter = _engine._objectConverterTypeFilter;
                var converterTargetFilter = _engine._typeConverterTargetFilter;
                if ((converterTypeFilter is null
                        || method.ReturnValueIsInvisibleToObjectConverters
                        || !method.ReturnTypeClaimedBy(converterTypeFilter))
                    && (converterTargetFilter is null || !method.ParameterTypesClaimedBy(converterTargetFilter))
                    && method.GetCompiledInvoker() is { } compiledInvoker
                    && (method.IsStatic || method.DeclaringType?.IsInstanceOfType(thisObj) == true))
                {
                    JsValue compiledResult = null!;
                    bool handled;
                    try
                    {
                        handled = compiledInvoker(thisObj, arguments, out compiledResult);
                    }
                    catch (Exception exception)
                    {
                        // the target method threw; surface it exactly like the reflection path, which
                        // normalizes non-TargetInvocationException throws to TargetInvocationException
                        _engine.CheckAmortizedConstraintsAtHostBoundary();
                        var normalized = exception as TargetInvocationException ?? new TargetInvocationException(exception);
                        Throw.MeaningfulException(_engine, normalized);
                        throw; // unreachable, MeaningfulException does not return
                    }

                    if (handled)
                    {
                        _engine.CheckAmortizedConstraintsAtHostBoundary();
                        return compiledResult;
                    }
                    // declined (non-exact argument) - fall through to the full binding path
                }
#endif
                if (TryCall(method, arguments, thisObj, converter, out var fastResult))
                {
                    return fastResult;
                }
            }
        }
        else
        {
            foreach (var (method, arguments, _) in InteropHelper.FindBestMatch(_engine, _methods, static (method, state) => ArgumentProvider(method, in state), state))
            {
                if (TryCall(method, arguments, thisObj, converter, out var result))
                {
                    return result;
                }
            }
        }

        if (_fallbackFunctionInstance is not null)
        {
            return _fallbackFunctionInstance.Call(thisObject, jsArguments);
        }

        var message = _engine.Options.Interop.ExposeDetailedResolutionErrors
            ? InteropErrorHelper.CreateNoMatchingMethodMessage(_targetType, _name, jsArguments, _methods)
            : "No public methods with the specified arguments were found.";
        Throw.InteropResolutionError(_engine.Realm, message, _targetType, _name, jsArguments, _methods);
        return null;
    }

    /// <summary>
    /// Derives the CLR instance the reflected method is invoked on. A receiver wrapping a real CLR
    /// object (<see cref="ObjectWrapper"/>, any <see cref="IObjectWrapper"/>, a Proxy over one)
    /// unwraps to it; a plain JS receiver — e.g. an object whose prototype chain contains the
    /// wrapper — has no CLR identity of its own (ToObject would synthesize a stand-in such as an
    /// ExpandoObject), so the method operates on the instance it was resolved from, exactly as
    /// <see cref="Descriptors.Specialized.ReflectionDescriptor"/> ignores the JS receiver for property reads.
    /// </summary>
    private object? ResolveThisObject(JsValue thisObject) => ResolveClrReceiver(_engine, thisObject, _target, _name);

    /// <inheritdoc cref="ResolveThisObject" />
    /// <remarks>
    /// Shared with <see cref="Reflection.GeneratedMethodAccessor"/> so a <c>[JsAccessible]</c> method and the
    /// reflected one it replaces derive their receiver from one rule rather than from two copies of it.
    /// </remarks>
    internal static object? ResolveClrReceiver(Engine engine, JsValue thisObject, object? owningTarget, string name)
    {
        // hot path: a member call on the wrapper itself
        if (thisObject is ObjectWrapper wrapper)
        {
            return wrapper.Target;
        }

        var receiver = thisObject;
        while (receiver is JsProxy proxy)
        {
            if (proxy.IsRevoked)
            {
                Throw.TypeError(engine.Realm, $"Cannot perform '{name}' on a proxy that has been revoked");
            }

            receiver = proxy._target;
        }

        if (receiver is ObjectInstance objectInstance)
        {
            if (objectInstance is IObjectWrapper hostWrapper)
            {
                return hostWrapper.Target;
            }

            return owningTarget ?? objectInstance.ToObject();
        }

        // undefined/null unwrap to null and fall back to the owning instance; primitives keep converting
        return receiver.ToObject() ?? owningTarget;
    }

    [DoesNotReturn]
    private void ThrowIncompatibleReceiver()
    {
        Throw.TypeError(_engine.Realm, $"Method '{_name}' called on incompatible receiver");
    }

    private static JsCallArguments ArgumentProvider(MethodDescriptor method, in MethodResolverState state)
    {
        if (method.IsExtensionMethod)
        {
            var jsArgumentsTemp = new JsValue[1 + state.Arguments.Length];
            jsArgumentsTemp[0] = state.This;
            Array.Copy(state.Arguments, 0, jsArgumentsTemp, 1, state.Arguments.Length);
            return method.HasParams
                ? ProcessParamsArrays(state.Engine, method, jsArgumentsTemp)
                : jsArgumentsTemp;
        }

        return method.HasParams
            ? ProcessParamsArrays(state.Engine, method, state.Arguments)
            : state.Arguments;
    }

    /// <summary>
    /// Mirrors the null/undefined rejection rule of overload scoring: null cannot bind to a
    /// non-optional parameter of non-nullable value type (even when value coercion could
    /// otherwise produce a value).
    /// </summary>
    private static bool CanBindNullArguments(ParameterInfo[] parameterInfos, JsCallArguments arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].IsNullOrUndefined())
            {
                var parameter = parameterInfos[i];
                if (!parameter.IsOptional && !InteropHelper.TypeIsNullable(parameter.ParameterType))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool TryCall(
        MethodDescriptor method,
        JsCallArguments arguments,
        object? thisObj,
        ClrTypeConverter converter,
        [NotNullWhen(true)] out JsValue? callResult)
    {
        callResult = null;

        var methodParameters = method.Parameters;
        var resolvedMethod = ResolveMethod(method, methodParameters, arguments);
        if (resolvedMethod is null)
        {
            // the inferred type arguments could not close this generic method - not a candidate
            return false;
        }

        // We only need to call GetParameters it if this ends up being a generic method (i.e. they will be different in that scenario)
        var isGenericDefinition = false;
        var parameterFlags = method.ParameterFlags;
        // when the descriptor is not generic, resolvedMethod is the (non-generic) descriptor method,
        // so the cached flag lets us skip the resolvedMethod.IsGenericMethod reflection call
        if (method.IsGenericMethod)
        {
            // the resolved parameters differ from the descriptor's, re-classify them
            methodParameters = resolvedMethod.GetParameters();
            isGenericDefinition = method.IsGenericMethodDefinition;
            parameterFlags = new InteropParameterFlags[methodParameters.Length];
            for (var i = 0; i < methodParameters.Length; i++)
            {
                parameterFlags[i] = MethodDescriptor.ComputeParameterFlags(methodParameters[i].ParameterType);
            }
        }

        // NOTE: pooling this buffer via ArrayPool was measured slower than allocation (tiny
        // gen0 arrays), keep the plain exact-size allocation
        var parameterCount = methodParameters.Length;
        object?[] parameters = parameterCount == 0 ? [] : new object[parameterCount];

        for (var i = 0; i < parameterCount; i++)
        {
            var methodParameter = methodParameters[i];
            var parameterType = methodParameter.ParameterType;
            var flags = parameterFlags[i];
            var argument = arguments.Length > i ? arguments[i] : null;
            object? argumentObject = null;

            if ((flags & InteropParameterFlags.JsValueAssignable) != InteropParameterFlags.None)
            {
                parameters[i] = argument;
            }
            else if (argument is null)
            {
                // optional
                parameters[i] = System.Type.Missing;
            }
            else if ((flags & InteropParameterFlags.GenericLike) != InteropParameterFlags.None && IsGenericParameter(argumentObject = argument.ToObject(), parameterType))
            {
                // only generic-shaped parameter types can match the probe, so the boxing
                // ToObject() detour is skipped entirely for plain parameter types
                parameters[i] = argumentObject;
            }
            else if ((flags & InteropParameterFlags.JsValueArray) != InteropParameterFlags.None && argument.IsArray())
            {
                // Handle specific case of F(params JsValue[])
                var arrayInstance = argument.AsArray();
                var len = TypeConverter.ToInt32(arrayInstance.Get(CommonProperties.Length, this));
                var result = new JsValue[len];
                for (uint k = 0; k < len; k++)
                {
                    result[k] = arrayInstance.TryGetValue(k, out var value) ? value : Undefined;
                }

                parameters[i] = result;
            }
            else if (argument is JsNumber jsNumber && InteropHelper.TryConvertNumberFast(jsNumber._value, parameterType, out parameters[i]))
            {
                // common numeric argument converted without the generic converter
            }
            else
            {
                if (!ReflectionExtensions.TryConvertViaTypeCoercion(parameterType, _engine.Options.Interop.ValueCoercion, argument, out parameters[i])
                    && !converter.TryConvert(argumentObject ?? argument.ToObject(), parameterType, CultureInfo.InvariantCulture, out parameters[i]))
                {
                    // arguments don't match this method
                    return false;
                }

                if (parameters[i] is LambdaExpression lambdaExpression)
                {
                    parameters[i] = lambdaExpression.Compile();
                }
            }
        }

        // Classify the receiver here, not from whatever the invoke raises. A TargetException coming out of
        // MethodBase.Invoke says nothing about who produced it: the host method's own body may have used
        // reflection and got a target wrong, and on net8+ MethodDescriptor.Invoke normalizes that into the
        // same TargetInvocationException shape a receiver mismatch takes. Rewriting either into a TypeError
        // discards the host's exception and defeats JintException.TryGetClrException. This is the predicate
        // the compiled-invoker lane already gates on; a null DeclaringType (a module-level global method,
        // which reflection over a Type never yields) is left for the invoke to judge as before.
        if (!method.IsStatic && method.DeclaringType is { } declaringType && !declaringType.IsInstanceOfType(thisObj))
        {
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            ThrowIncompatibleReceiver();
        }

        try
        {
            if (isGenericDefinition)
            {
                // the resolved generic method differs per call, cannot use the cached invoker
                object? result;
                try
                {
                    using var suspension = _engine.SuspendHostCallForCallbacks(
                        DefaultTypeConverter.ContainsHostCallback(parameters));
                    result = resolvedMethod.Invoke(thisObj, parameters);
                }
                catch (ArgumentException)
                {
                    // MethodBase.Invoke always wraps what the *body* threw in a TargetInvocationException, so
                    // a bare ArgumentException out of it can only be the binder rejecting an argument the
                    // inferred instantiation cannot accept. That is "this candidate does not apply", not a
                    // host failure - report it as such rather than letting a CLR exception escape Evaluate.
                    // Only the invoke is guarded; the conversion below has ArgumentException paths of its own.
                    _engine.CheckAmortizedConstraintsAtHostBoundary();
                    return false;
                }

                // conversion before the check so an awaitable result gets its continuation attached
                callResult = FromObjectWithType(Engine, result, type: (resolvedMethod as MethodInfo)?.ReturnType);
                _engine.CheckAmortizedConstraintsAtHostBoundary();
                return true;
            }

            object? invokeResult;
            using (Engine.SuspendHostCallForCallbacks(
                       DefaultTypeConverter.ContainsHostCallback(parameters)))
            {
                invokeResult = method.Invoke(thisObj, parameters);
            }
            callResult = FromObjectWithType(Engine, invokeResult, type: method.ReturnType);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return true;
        }
        catch (TargetInvocationException exception)
        {
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            Throw.MeaningfulException(_engine, exception);
            return false;
        }
    }

    /// <summary>
    /// Reduces a flat list of parameters to a params array, if needed
    /// </summary>
    private static JsCallArguments ProcessParamsArrays(Engine engine, MethodDescriptor methodInfo, JsCallArguments arguments)
    {
        var parameters = methodInfo.Parameters;

        var nonParamsArgumentsCount = parameters.Length - 1;
        if (arguments.Length < nonParamsArgumentsCount)
        {
            return arguments;
        }

        var argsToTransform = Arguments.Slice(arguments, nonParamsArgumentsCount);

        if (argsToTransform.Length == 1 && argsToTransform[0].IsArray())
        {
            return arguments;
        }

        var array = new JsArray(engine, argsToTransform);
        var newArguments = new JsValue[nonParamsArgumentsCount + 1];
        for (var j = 0; j < nonParamsArgumentsCount; ++j)
        {
            newArguments[j] = arguments[j];
        }

        newArguments[nonParamsArgumentsCount] = array;
        return newArguments;
    }

    public override string ToString()
    {
        return $"function {_targetType}.{_name}() {{ [native code] }}";
    }
}
