#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Expression = System.Linq.Expressions.Expression;

// The invoker is compiled with System.Linq.Expressions and only ever binds publicly visible types
// (guarded below), so the generated dynamic method needs no reflection-visibility relaxation.
// IL2075/IL3050 cover the reflection + dynamic-code use, which is gated behind
// RuntimeFeature.IsDynamicCodeCompiled before anything is built.
#pragma warning disable IL2075
#pragma warning disable IL3050

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
    /// A built thunk together with the number of arguments it indexes out of the array it is handed.
    /// <see cref="Invoke"/> is <see langword="null"/> for a declined delegate type, in which case
    /// <see cref="Arity"/> is meaningless.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct CompiledInvoker(Invoker? Invoke, int Arity);

    // Process-wide L2 cache; a null thunk is the "known ineligible" sentinel so a declined delegate
    // type is never re-probed. Keyed on a Type, which pins nothing the delegate instance did not
    // already pin, and the compiled thunk closes over no engine state — one entry serves every
    // engine in the process. A concurrent duplicate build is benign; one thunk is discarded.
    private static readonly ConcurrentDictionary<Type, CompiledInvoker> _cache = new();

    /// <summary>
    /// Returns the thunk for <paramref name="delegateType"/>, or <see langword="null"/> when the type
    /// is ineligible or the runtime cannot compile the thunk to native code.
    /// </summary>
    /// <param name="delegateType">The concrete delegate type to build for.</param>
    /// <param name="arity">
    /// How many arguments the thunk reads. The caller must only hand it an argument array of exactly
    /// that length, since the thunk indexes it positionally with no bounds reasoning of its own.
    /// </param>
    internal static Invoker? For(Type delegateType, out int arity)
    {
        var entry = _cache.GetOrAdd(delegateType, static t =>
        {
            // Build ONLY when the runtime will JIT the thunk. Under AOT and under an interpreted-only
            // Expression.Compile (e.g. the Mono interpreter) an interpreted lambda is no faster than
            // DynamicInvoke, so decline and keep the reflection path.
            if (!RuntimeFeature.IsDynamicCodeCompiled || !TryBuild(t, out var built, out var builtArity))
            {
                return default;
            }

            return new CompiledInvoker(built, builtArity);
        });

        arity = entry.Arity;
        return entry.Invoke;
    }

    private static bool TryBuild(Type delegateType, [NotNullWhen(true)] out Invoker? invoker, out int arity)
    {
        invoker = null;
        arity = 0;

        if (!delegateType.IsVisible || delegateType.GetMethod("Invoke") is not { } invokeMethod)
        {
            return false;
        }

        var returnType = invokeMethod.ReturnType;
        if (returnType != typeof(void) && !IsBindable(returnType))
        {
            return false;
        }

        var parameters = invokeMethod.GetParameters();
        foreach (var parameter in parameters)
        {
            // by-ref (ref / out / in) and pointer parameters cannot be fed from an object?[] element,
            // and the reflection path is the only one that models them
            if (!IsBindable(parameter.ParameterType))
            {
                return false;
            }
        }

        var delegateParameter = Expression.Parameter(typeof(Delegate), "d");
        var argumentsParameter = Expression.Parameter(typeof(object?[]), "args");

        var arguments = new Expression[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var element = Expression.ArrayIndex(argumentsParameter, Expression.Constant(i));
            arguments[i] = Expression.Convert(element, parameters[i].ParameterType);
        }

        Expression body = Expression.Invoke(Expression.Convert(delegateParameter, delegateType), arguments);
        body = returnType == typeof(void)
            ? Expression.Block(body, Expression.Constant(null, typeof(object)))
            : Expression.Convert(body, typeof(object));

        try
        {
            invoker = Expression.Lambda<Invoker>(body, delegateParameter, argumentsParameter).Compile();
        }
        catch (Exception)
        {
            // any shape the expression compiler refuses keeps the reflection path
            return false;
        }

        arity = parameters.Length;
        return invoker is not null;
    }

    private static bool IsBindable(Type type) => !type.IsByRef && !type.IsPointer && type.IsVisible;
}
#endif
