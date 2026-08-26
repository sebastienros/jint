#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Expression = System.Linq.Expressions.Expression;

// The invoker is compiled with System.Linq.Expressions and only ever binds publicly visible types
// (guarded below), so the generated dynamic method needs no reflection-visibility relaxation.
// The reflection and dynamic-code use is gated behind RuntimeFeature.IsDynamicCodeCompiled before
// anything is built.

namespace Jint.Runtime.Interop;

/// <summary>
/// Builds a strongly-typed thunk that invokes a host delegate through its own <c>Invoke</c> method
/// instead of <see cref="Delegate.DynamicInvoke"/>, which re-binds and re-validates the whole
/// argument list on every single call.
/// <para>
/// The thunk is keyed on the delegate <em>type</em> rather than on the target method, because that
/// type alone fixes the invocation signature. Going through <c>Invoke</c> also keeps the thunk
/// semantically identical to <see cref="Delegate.DynamicInvoke"/> for every delegate shape —
/// multicast invocation lists, closures and open-instance delegates included — so no per-shape
/// eligibility reasoning is needed; only types the generated code cannot reference are declined.
/// The signature it binds is the delegate type's own, which is not always its target method's — an
/// open-instance delegate declares the receiver as an <c>Invoke</c> parameter while the
/// <see cref="MethodInfo"/> behind it does not — so the arity is reported back and the caller only
/// uses the thunk for an argument list of exactly that length.
/// </para>
/// <para>
/// Exceptions thrown by the host delegate propagate unwrapped (there is no reflection frame to wrap
/// them), so the caller normalizes them to <see cref="TargetInvocationException"/> exactly as the
/// reflection path produced them.
/// </para>
/// </summary>
internal static class CompiledDelegateInvoker
{
    internal delegate object? Invoker(Delegate target, object?[] arguments);

    /// <summary>
    /// The one- and two-parameter twins of <see cref="Invoker"/>: same thunk, same casts, but the
    /// arguments arrive in registers instead of an array the caller has to allocate and fill first.
    /// A delegate type's arity is fixed at build time, so exactly one of them is ever built (and only
    /// for arity 1 or 2 — the arities the arity-specialized call lane offers).
    /// </summary>
    internal delegate object? Invoker1(Delegate target, object? argument0);

    /// <inheritdoc cref="Invoker1"/>
    internal delegate object? Invoker2(Delegate target, object? argument0, object? argument1);

    /// <summary>
    /// A built thunk together with the signature it binds — the delegate type's own <c>Invoke</c> signature,
    /// which the caller has to check against the target method before using the thunk.
    /// <see cref="Invoke"/> is <see langword="null"/> for a declined delegate type, in which case every other
    /// member is <see langword="null"/> as well. <see cref="Invoke1"/>/<see cref="Invoke2"/> are the
    /// register-passing twins, non-null only for a delegate type of exactly that arity; a caller that has one
    /// may skip building the argument array entirely, and a caller that does not falls back to
    /// <see cref="Invoke"/>.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct CompiledInvoker(
        Invoker? Invoke,
        Invoker1? Invoke1,
        Invoker2? Invoke2,
        Type[]? ParameterTypes,
        Type? ReturnType);

    // Process-wide L2 cache; a null thunk is the "known ineligible" sentinel so a declined delegate
    // type is never re-probed. Keyed on a Type, which pins nothing the delegate instance did not
    // already pin, and the compiled thunk closes over no engine state — one entry serves every
    // engine in the process. A concurrent duplicate build is benign; one thunk is discarded.
    private static readonly ConcurrentDictionary<Type, CompiledInvoker> _cache = new();

    /// <summary>
    /// Returns the thunk for <paramref name="delegateType"/> together with the signature it binds, or an
    /// entry whose <see cref="CompiledInvoker.Invoke"/> is <see langword="null"/> when the type is
    /// ineligible or the runtime cannot compile the thunk to native code. The reported signature is the
    /// delegate type's, which the caller must reconcile with the target method's before running the thunk:
    /// the argument array is built for the target's parameter types while the thunk casts to the delegate's,
    /// and relaxed delegate binding lets the two differ.
    /// </summary>
    /// <param name="delegateType">The concrete delegate type to build for.</param>
    internal static CompiledInvoker For(Type delegateType)
    {
        return _cache.GetOrAdd(delegateType, static t =>
        {
            // Build ONLY when the runtime will JIT the thunk. Under AOT and under an interpreted-only
            // Expression.Compile (e.g. the Mono interpreter) an interpreted lambda is no faster than
            // DynamicInvoke, so decline and keep the reflection path.
            if (!RuntimeFeature.IsDynamicCodeCompiled || !TryBuild(t, out var built))
            {
                return default;
            }

            return built;
        });
    }

    private static bool TryBuild(Type delegateType, out CompiledInvoker result)
    {
        result = default;

        if (!delegateType.IsVisible || delegateType.GetMethod("Invoke") is not { } invokeMethod)
        {
            return false;
        }

        var invokeReturnType = invokeMethod.ReturnType;
        if (invokeReturnType != typeof(void) && !IsBindable(invokeReturnType))
        {
            return false;
        }

        var parameters = invokeMethod.GetParameters();
        var invokeParameterTypes = parameters.Length == 0 ? [] : new Type[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            // by-ref (ref / out / in) and pointer parameters cannot be fed from an object?[] element,
            // and the reflection path is the only one that models them
            var parameterType = parameters[i].ParameterType;
            if (!IsBindable(parameterType))
            {
                return false;
            }

            invokeParameterTypes[i] = parameterType;
        }

        var delegateParameter = Expression.Parameter(typeof(Delegate), "d");
        var argumentsParameter = Expression.Parameter(typeof(object?[]), "args");

        var arguments = new Expression[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var element = Expression.ArrayIndex(argumentsParameter, Expression.Constant(i));
            arguments[i] = Expression.Convert(element, invokeParameterTypes[i]);
        }

        var invoker = Compile<Invoker>(delegateType, invokeReturnType, arguments, delegateParameter, argumentsParameter);
        if (invoker is null)
        {
            return false;
        }

        // The register-passing twin for the arities the arity-specialized call lane serves. It is a
        // pure convenience for the caller — same delegate type, same casts, same exceptions — so
        // failing to build one is not a reason to decline the array-shaped thunk.
        Invoker1? invoker1 = null;
        Invoker2? invoker2 = null;
        if (parameters.Length == 1)
        {
            var argument0 = Expression.Parameter(typeof(object), "a0");
            invoker1 = Compile<Invoker1>(
                delegateType,
                invokeReturnType,
                [Expression.Convert(argument0, invokeParameterTypes[0])],
                delegateParameter,
                argument0);
        }
        else if (parameters.Length == 2)
        {
            var argument0 = Expression.Parameter(typeof(object), "a0");
            var argument1 = Expression.Parameter(typeof(object), "a1");
            invoker2 = Compile<Invoker2>(
                delegateType,
                invokeReturnType,
                [Expression.Convert(argument0, invokeParameterTypes[0]), Expression.Convert(argument1, invokeParameterTypes[1])],
                delegateParameter,
                argument0,
                argument1);
        }

        result = new CompiledInvoker(invoker, invoker1, invoker2, invokeParameterTypes, invokeReturnType);
        return true;
    }

    /// <summary>
    /// Compiles one thunk shape: <c>(Delegate d, ...) =&gt; (object) ((TDelegate) d).Invoke(&lt;arguments&gt;)</c>,
    /// with a <see langword="null"/> stand-in for a <c>void</c> return. The first lambda parameter is always
    /// the <see cref="Delegate"/> to cast; the rest supply the arguments and differ per shape.
    /// </summary>
    private static TInvoker? Compile<TInvoker>(
        Type delegateType,
        Type invokeReturnType,
        Expression[] arguments,
        params ParameterExpression[] lambdaParameters)
        where TInvoker : Delegate
    {
        Expression body = Expression.Invoke(Expression.Convert(lambdaParameters[0], delegateType), arguments);
        body = invokeReturnType == typeof(void)
            ? Expression.Block(body, Expression.Constant(null, typeof(object)))
            : Expression.Convert(body, typeof(object));

        try
        {
            return Expression.Lambda<TInvoker>(body, lambdaParameters).Compile();
        }
        catch (Exception)
        {
            // any shape the expression compiler refuses keeps the reflection path
            return null;
        }
    }

    private static bool IsBindable(Type type) => !type.IsByRef && !type.IsPointer && type.IsVisible;
}
#endif
