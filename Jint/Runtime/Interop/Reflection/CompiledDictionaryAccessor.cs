using System.Reflection;

#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Expression = System.Linq.Expressions.Expression;
#endif

// The delegate is compiled with System.Linq.Expressions and only references the reflected dictionary
// interface, whose visibility is checked before anything is emitted. IL2075/IL3050 cover the reflection +
// dynamic-code use, which is gated behind RuntimeFeature.IsDynamicCodeCompiled.
#pragma warning disable IL2075
#pragma warning disable IL3050

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// Builds and caches a delegate that calls <c>TryGetValue</c> on a closed generic
/// <see cref="IDictionary{TKey,TValue}"/> / <see cref="IReadOnlyDictionary{TKey,TValue}"/>, so that reading a
/// property off a dictionary-shaped host object does not go through <see cref="MethodBase.Invoke(object, object[])"/>
/// — which costs an <c>object[]</c> argument array and a reflection dispatch on <b>every</b> property read,
/// there being no inline-cache backstop for dictionaries. Those two are what the lane removes; a
/// value-typed <c>TValue</c> is still boxed on the way out, because the delegate hands the value back as
/// <see cref="object"/> exactly as the reflection path did.
/// <para>
/// Keyed by the <c>TryGetValue</c> <see cref="MethodInfo"/>, which is the one declared on the generic
/// interface and therefore already shared by every implementation of it, and cached process-wide because
/// nothing the delegate closes over is affine to an <see cref="Engine"/>. Same trade-off as the other
/// process-wide reflection caches here: the cached <see cref="MethodInfo"/> keeps its declaring assembly
/// alive for the process lifetime.
/// </para>
/// </summary>
internal static class CompiledDictionaryAccessor
{
    /// <summary>
    /// Reads <paramref name="key"/> out of the dictionary <paramref name="target"/>, boxing the value.
    /// </summary>
    internal delegate bool DictionaryValueGetter(object target, object key, out object? value);

#if NET8_0_OR_GREATER
    // a null value is the "known ineligible" sentinel so an ineligible dictionary shape is never re-probed
    private static readonly ConcurrentDictionary<MethodInfo, DictionaryValueGetter?> _valueGetters = new();
#endif

    /// <summary>
    /// Returns the compiled reader for <paramref name="tryGetValue"/>, or <see langword="null"/> when the
    /// dictionary shape is not eligible and the caller has to keep using reflection.
    /// </summary>
    internal static DictionaryValueGetter? GetValueGetter(MethodInfo? tryGetValue)
    {
#if NET8_0_OR_GREATER
        if (tryGetValue is null)
        {
            return null;
        }

        return _valueGetters.GetOrAdd(tryGetValue, static m => Build(m));
#else
        return null;
#endif
    }

#if NET8_0_OR_GREATER
    private static DictionaryValueGetter? Build(MethodInfo tryGetValue)
    {
        // Under AOT and an interpreted-only Expression.Compile (e.g. the Mono interpreter) an interpreted
        // lambda is slower than the reflection path it replaces, so decline entirely.
        if (!RuntimeFeature.IsDynamicCodeCompiled)
        {
            return null;
        }

        // the constructed generic interface is only visible when its type arguments are, so this single
        // check also covers a non-public key or value type
        var declaringType = tryGetValue.DeclaringType;
        if (declaringType is null || !declaringType.IsVisible || tryGetValue.ReturnType != typeof(bool))
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
        if (valueType is null || keyType.IsByRef || keyType.IsPointer || valueType.IsPointer)
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
            return Expression.Lambda<DictionaryValueGetter>(body, targetParameter, keyParameter, valueParameter).Compile();
        }
        catch (Exception)
        {
            // an accessibility/binding quirk the eligibility checks did not anticipate
            return null;
        }
    }
#endif
}
