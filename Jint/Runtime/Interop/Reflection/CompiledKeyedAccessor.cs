using System.Reflection;

#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Expression = System.Linq.Expressions.Expression;
#endif

// The delegates are compiled with System.Linq.Expressions and only reference the reflected declaring
// type, whose visibility is checked before anything is emitted. IL2075/IL3050 cover the reflection +
// dynamic-code use, which is gated behind RuntimeFeature.IsDynamicCodeCompiled.
#pragma warning disable IL2075
#pragma warning disable IL3050

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// Builds and caches delegates for the CLR members that take a single <em>key</em> argument, so that reading,
/// probing, writing or deleting such a member does not go through
/// <see cref="MethodBase.Invoke(object, object[])"/> — which costs an <c>object[]</c> argument array and a
/// reflection dispatch on <b>every</b> operation, there being no inline-cache backstop for either shape this
/// serves:
/// <list type="bullet">
/// <item>the members of a closed generic <see cref="IDictionary{TKey,TValue}"/> /
/// <see cref="IReadOnlyDictionary{TKey,TValue}"/> — <c>TryGetValue</c>, <c>ContainsKey</c>, <c>Remove</c> and
/// the indexer's setter — reached through <see cref="TypeDescriptor"/>;</item>
/// <item>a declared indexer's getter, setter and companion <c>ContainsKey</c>/<c>Contains</c> probe, reached
/// through <see cref="IndexerAccessor"/> (which is also what serves a string-keyed dictionary write).</item>
/// </list>
/// The argument array and the dispatch are what the lanes remove; a value-typed key or value is still boxed on
/// the way in and out, because the delegates hand values across as <see cref="object"/> exactly as the
/// reflection path did.
/// <para>
/// Keyed by the <see cref="MethodInfo"/> — for a dictionary that is the one declared on the generic interface
/// and therefore already shared by every implementation of it — and cached process-wide because nothing the
/// delegates close over is affine to an <see cref="Engine"/>. Same trade-off as the other process-wide
/// reflection caches here: the cached <see cref="MethodInfo"/> keeps its declaring assembly alive for the
/// process lifetime.
/// </para>
/// </summary>
internal static class CompiledKeyedAccessor
{
    /// <summary>
    /// Reads <paramref name="key"/> out of the dictionary <paramref name="target"/>, boxing the value.
    /// </summary>
    internal delegate bool KeyedValueGetter(object target, object key, out object? value);

    /// <summary>
    /// Reads <paramref name="key"/> through an indexer's getter, boxing the value.
    /// </summary>
    internal delegate object? KeyedIndexerGetter(object target, object key);

    /// <summary>
    /// Answers a <c>bool</c>-returning single-key member — <c>ContainsKey</c>, <c>Contains</c> or
    /// <c>Remove</c> — on <paramref name="target"/>.
    /// </summary>
    internal delegate bool KeyedPredicate(object target, object key);

    /// <summary>
    /// Writes <paramref name="value"/> under <paramref name="key"/> through an indexer's setter.
    /// </summary>
    internal delegate void KeyedValueSetter(object target, object key, object? value);

#if NET8_0_OR_GREATER
    // a null value is the "known ineligible" sentinel so an ineligible shape is never re-probed
    private static readonly ConcurrentDictionary<MethodInfo, KeyedValueGetter?> _valueGetters = new();
    private static readonly ConcurrentDictionary<MethodInfo, KeyedIndexerGetter?> _indexerGetters = new();
    private static readonly ConcurrentDictionary<MethodInfo, KeyedPredicate?> _keyPredicates = new();
    private static readonly ConcurrentDictionary<MethodInfo, KeyedValueSetter?> _valueSetters = new();
#endif

    /// <summary>
    /// Returns the compiled reader for a dictionary's <c>bool TryGetValue(TKey, out TValue)</c>, or
    /// <see langword="null"/> when the shape is not eligible and the caller has to keep using reflection.
    /// </summary>
    internal static KeyedValueGetter? GetValueGetter(MethodInfo? tryGetValue)
    {
#if NET8_0_OR_GREATER
        if (tryGetValue is null)
        {
            return null;
        }

        return _valueGetters.GetOrAdd(tryGetValue, static m => BuildValueGetter(m));
#else
        return null;
#endif
    }

    /// <summary>
    /// Returns the compiled caller for an indexer's <c>TValue get_Item(TKey)</c>, or <see langword="null"/>
    /// when the shape is not eligible.
    /// </summary>
    internal static KeyedIndexerGetter? GetIndexerGetter(MethodInfo? getMethod)
    {
#if NET8_0_OR_GREATER
        if (getMethod is null)
        {
            return null;
        }

        return _indexerGetters.GetOrAdd(getMethod, static m => BuildIndexerGetter(m));
#else
        return null;
#endif
    }

    /// <summary>
    /// Returns the compiled caller for a <c>bool M(TKey)</c> member (<c>ContainsKey</c>, <c>Contains</c> or
    /// <c>Remove</c>), or <see langword="null"/> when the shape is not eligible.
    /// </summary>
    internal static KeyedPredicate? GetKeyPredicate(MethodInfo? method)
    {
#if NET8_0_OR_GREATER
        if (method is null)
        {
            return null;
        }

        return _keyPredicates.GetOrAdd(method, static m => BuildKeyPredicate(m));
#else
        return null;
#endif
    }

    /// <summary>
    /// Returns the compiled caller for an indexer's <c>void set_Item(TKey, TValue)</c>, or
    /// <see langword="null"/> when the shape is not eligible.
    /// </summary>
    internal static KeyedValueSetter? GetValueSetter(MethodInfo? setMethod)
    {
#if NET8_0_OR_GREATER
        if (setMethod is null)
        {
            return null;
        }

        return _valueSetters.GetOrAdd(setMethod, static m => BuildValueSetter(m));
#else
        return null;
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The checks every lane shares. A constructed generic interface is only visible when its type arguments
    /// are, so the single visibility check also covers a non-public key or value type. Value-type receivers
    /// are excluded because the compiled call would run against the unboxed copy, so a write would not reach
    /// the original — the same reason <see cref="CompiledMemberAccessor"/> excludes them.
    /// </summary>
    private static bool IsEligible(MethodInfo method, [NotNullWhen(true)] out Type? declaringType)
    {
        declaringType = method.DeclaringType;

        // Under AOT and an interpreted-only Expression.Compile (e.g. the Mono interpreter) an interpreted
        // lambda is slower than the reflection path it replaces, so decline entirely.
        return RuntimeFeature.IsDynamicCodeCompiled
               && declaringType is not null
               && declaringType.IsVisible
               && !declaringType.IsValueType
               && !method.IsStatic;
    }

    private static bool IsBindable(Type type) => !type.IsByRef && !type.IsPointer;

    private static KeyedIndexerGetter? BuildIndexerGetter(MethodInfo getMethod)
    {
        if (!IsEligible(getMethod, out var declaringType))
        {
            return null;
        }

        var returnType = getMethod.ReturnType;
        var parameters = getMethod.GetParameters();
        if (parameters.Length != 1
            || !IsBindable(parameters[0].ParameterType)
            || returnType == typeof(void)
            || !IsBindable(returnType))
        {
            return null;
        }

        var targetParameter = Expression.Parameter(typeof(object), "target");
        var keyParameter = Expression.Parameter(typeof(object), "key");

        // the caller only takes this lane when the key's runtime type matches, so the cast cannot fail;
        // boxing the result reproduces the reflection path's boxed value
        var call = Expression.Convert(
            Expression.Call(
                Expression.Convert(targetParameter, declaringType),
                getMethod,
                Expression.Convert(keyParameter, parameters[0].ParameterType)),
            typeof(object));

        try
        {
            return Expression.Lambda<KeyedIndexerGetter>(call, targetParameter, keyParameter).Compile();
        }
        catch (Exception)
        {
            // an accessibility/binding quirk the eligibility checks did not anticipate
            return null;
        }
    }

    private static KeyedPredicate? BuildKeyPredicate(MethodInfo method)
    {
        if (!IsEligible(method, out var declaringType) || method.ReturnType != typeof(bool))
        {
            return null;
        }

        var parameters = method.GetParameters();
        if (parameters.Length != 1 || !IsBindable(parameters[0].ParameterType))
        {
            return null;
        }

        var targetParameter = Expression.Parameter(typeof(object), "target");
        var keyParameter = Expression.Parameter(typeof(object), "key");

        var call = Expression.Call(
            Expression.Convert(targetParameter, declaringType),
            method,
            Expression.Convert(keyParameter, parameters[0].ParameterType));

        try
        {
            return Expression.Lambda<KeyedPredicate>(call, targetParameter, keyParameter).Compile();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static KeyedValueSetter? BuildValueSetter(MethodInfo setMethod)
    {
        if (!IsEligible(setMethod, out var declaringType) || setMethod.ReturnType != typeof(void))
        {
            return null;
        }

        var parameters = setMethod.GetParameters();
        if (parameters.Length != 2
            || !IsBindable(parameters[0].ParameterType)
            || !IsBindable(parameters[1].ParameterType))
        {
            return null;
        }

        var targetParameter = Expression.Parameter(typeof(object), "target");
        var keyParameter = Expression.Parameter(typeof(object), "key");
        var valueParameter = Expression.Parameter(typeof(object), "value");

        // the caller only takes this lane when both runtime types match, so neither cast can fail
        var call = Expression.Call(
            Expression.Convert(targetParameter, declaringType),
            setMethod,
            Expression.Convert(keyParameter, parameters[0].ParameterType),
            Expression.Convert(valueParameter, parameters[1].ParameterType));

        try
        {
            return Expression.Lambda<KeyedValueSetter>(call, targetParameter, keyParameter, valueParameter).Compile();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static KeyedValueGetter? BuildValueGetter(MethodInfo tryGetValue)
    {
        if (!IsEligible(tryGetValue, out var declaringType) || tryGetValue.ReturnType != typeof(bool))
        {
            return null;
        }

        var parameters = tryGetValue.GetParameters();
        if (parameters.Length != 2)
        {
            return null;
        }

        var keyType = parameters[0].ParameterType;
        var valueType = parameters[1].ParameterType.GetElementType();
        if (valueType is null || !IsBindable(keyType) || valueType.IsPointer)
        {
            return null;
        }

        var targetParameter = Expression.Parameter(typeof(object), "target");
        var keyParameter = Expression.Parameter(typeof(object), "key");
        var valueParameter = Expression.Parameter(typeof(object).MakeByRefType(), "value");

        // the caller only takes this lane when the key's runtime type matches, so the cast cannot fail
        var typedValue = Expression.Variable(valueType, "typedValue");
        var found = Expression.Variable(typeof(bool), "found");

        var call = Expression.Call(
            Expression.Convert(targetParameter, declaringType),
            tryGetValue,
            Expression.Convert(keyParameter, keyType),
            typedValue);

        // the out local is default-initialized exactly like the Activator.CreateInstance the reflection
        // path needs for a value-typed TValue, and boxing it here reproduces that path's boxed result
        var body = Expression.Block(
            typeof(bool),
            [typedValue, found],
            Expression.Assign(found, call),
            Expression.Assign(valueParameter, Expression.Convert(typedValue, typeof(object))),
            found);

        try
        {
            return Expression.Lambda<KeyedValueGetter>(body, targetParameter, keyParameter, valueParameter).Compile();
        }
        catch (Exception)
        {
            return null;
        }
    }
#endif
}
