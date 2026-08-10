using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jint.Runtime.Interop.Reflection;

#pragma warning disable IL2067
#pragma warning disable IL2075
#pragma warning disable IL2077

namespace Jint.Runtime.Interop;

internal sealed class TypeDescriptor
{
    private static readonly ConcurrentDictionary<Type, TypeDescriptor> _cache = new();

    private static readonly Type _listType = typeof(IList);
    private static readonly PropertyInfo _listIndexer = typeof(IList).GetProperty("Item")!;

    private static readonly Type _genericDictionaryType = typeof(IDictionary<,>);
    private static readonly Type _readOnlyGenericDictionaryType = typeof(IReadOnlyDictionary<,>);
    private static readonly Type _stringType = typeof(string);

    private readonly MethodInfo? _tryGetValueMethod;
    private readonly PropertyInfo? _keysAccessor;
    private readonly Type? _keyType;
    private readonly Type? _valueType;
    private readonly MethodInfo? _toJsonMethod;
    private readonly MethodInfo? _genericContainsKeyMethod;
    private readonly MethodInfo? _genericIndexerSetMethod;
    private readonly MethodInfo? _genericRemoveMethod;

    // per-descriptor L1 caches over the process-wide compiled delegates, so a dictionary operation is a
    // field read. Resolved lazily and racily - the delegates are pure, so a duplicate resolve is harmless.
    private CompiledKeyedAccessor.KeyedValueGetter? _compiledValueGetter;
    private bool _compiledValueGetterResolved;

    private CompiledKeyedAccessor.KeyedPredicate? _compiledContainsKey;
    private bool _compiledContainsKeyResolved;

    private CompiledKeyedAccessor.KeyedValueSetter? _compiledValueSetter;
    private bool _compiledValueSetterResolved;

    private CompiledKeyedAccessor.KeyedPredicate? _compiledRemove;
    private bool _compiledRemoveResolved;

    private TypeDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)]
        Type type)
    {
        Analyze(
            type,
            out var isCollection,
            out var isEnumerable,
            out var isDictionary,
            out _tryGetValueMethod,
            out _keysAccessor,
            out _keyType,
            out _valueType,
            out var lengthProperty,
            out var integerIndexer,
            out _toJsonMethod,
            out _genericContainsKeyMethod,
            out _genericIndexerSetMethod,
            out _genericRemoveMethod);

        IntegerIndexerProperty = integerIndexer;
        IsDictionary = _tryGetValueMethod is not null || isDictionary;
        IsGenericDictionary = _tryGetValueMethod is not null;
        IsStringKeyedGenericDictionary = IsGenericDictionary && _keyType == _stringType;
        IsNonStringKeyedGenericDictionary = IsGenericDictionary && _keyType != _stringType;

        // dictionaries are considered normal-object-like
        IsArrayLike = !IsDictionary && isCollection;

        IsEnumerable = isEnumerable;

        IsDisposable = type.GetInterface(nameof(IDisposable)) is not null;

#if SUPPORTS_ASYNC_DISPOSE
        IsAsyncDisposable = type.GetInterface(nameof(IAsyncDisposable)) is not null;
#endif

        if (IsArrayLike)
        {
            LengthProperty = lengthProperty;
        }
    }

    public bool IsArrayLike { get; }

    /// <summary>
    /// Is this read-write indexed.
    /// </summary>
    public bool IsIntegerIndexed => IntegerIndexerProperty is not null;

    /// <summary>
    /// Read-write indexer.
    /// </summary>
    public PropertyInfo? IntegerIndexerProperty { get; }

    public bool IsDictionary { get; }

    /// <summary>
    /// Whether <see cref="ContainsDictionaryKey"/> can actually answer. <c>ContainsKey</c> is declared on
    /// both <see cref="IDictionary{TKey,TValue}"/> and <see cref="IReadOnlyDictionary{TKey,TValue}"/>, so it
    /// is found whenever <c>TryGetValue</c> is — but a caller that reads a <see langword="false"/> as
    /// "the key is absent" has to know the difference between that and "there was nothing to ask".
    /// </summary>
    public bool CanTestDictionaryKey => _genericContainsKeyMethod is not null;

    public bool IsStringKeyedGenericDictionary { get; }
    public bool IsGenericDictionary { get; }
    public bool IsNonStringKeyedGenericDictionary { get; }
    public Type? GenericDictionaryKeyType => _keyType;
    public Type? GenericDictionaryValueType => _valueType;
    public bool IsEnumerable { get; }
    public bool IsDisposable { get; }
    public bool IsAsyncDisposable { get; }
    public PropertyInfo? LengthProperty { get; }

    public bool Iterable => IsArrayLike || IsDictionary || IsEnumerable;

    public PropertyInfo? KeysAccessor => _keysAccessor;

    public MethodInfo? ToJsonMethod => _toJsonMethod;

    public static TypeDescriptor Get(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)]
        Type type)
    {
        return _cache.GetOrAdd(type, t => new TypeDescriptor(t));
    }

    private static void Analyze(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)]
        Type type,
        out bool isCollection,
        out bool isEnumerable,
        out bool isDictionary,
        out MethodInfo? tryGetValueMethod,
        out PropertyInfo? keysAccessor,
        out Type? keyType,
        out Type? valueType,
        out PropertyInfo? lengthProperty,
        out PropertyInfo? integerIndexer,
        out MethodInfo? toJsonMethod,
        out MethodInfo? genericContainsKeyMethod,
        out MethodInfo? genericIndexerSetMethod,
        out MethodInfo? genericRemoveMethod)
    {
        AnalyzeType(
            type,
            out isCollection,
            out isEnumerable,
            out isDictionary,
            out tryGetValueMethod,
            out keysAccessor,
            out keyType,
            out valueType,
            out lengthProperty,
            out integerIndexer,
            out toJsonMethod,
            out genericContainsKeyMethod,
            out genericIndexerSetMethod,
            out genericRemoveMethod);

        foreach (var t in type.GetInterfaces())
        {
#pragma warning disable IL2072
            AnalyzeType(
                t,
                out var isCollectionForSubType,
                out var isEnumerableForSubType,
                out var isDictionaryForSubType,
                out var tryGetValueMethodForSubType,
                out var keysAccessorForSubType,
                out var keyTypeForSubType,
                out var valueTypeForSubType,
                out var lengthPropertyForSubType,
                out var integerIndexerForSubType,
                out var toJsonMethodForSubType,
                out var genericContainsKeyMethodForSubType,
                out var genericIndexerSetMethodForSubType,
                out var genericRemoveMethodForSubType);
#pragma warning restore IL2072

            isCollection |= isCollectionForSubType;
            isEnumerable |= isEnumerableForSubType;
            isDictionary |= isDictionaryForSubType;

            tryGetValueMethod ??= tryGetValueMethodForSubType;
            keysAccessor ??= keysAccessorForSubType;
            keyType ??= keyTypeForSubType;
            valueType ??= valueTypeForSubType;
            lengthProperty ??= lengthPropertyForSubType;
            integerIndexer ??= integerIndexerForSubType;
            toJsonMethod ??= toJsonMethodForSubType;
            genericContainsKeyMethod ??= genericContainsKeyMethodForSubType;
            genericIndexerSetMethod ??= genericIndexerSetMethodForSubType;
            genericRemoveMethod ??= genericRemoveMethodForSubType;
        }
    }

    private static void AnalyzeType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)]
        Type type,
        out bool isCollection,
        out bool isEnumerable,
        out bool isDictionary,
        out MethodInfo? tryGetValueMethod,
        out PropertyInfo? keysAccessor,
        out Type? keyType,
        out Type? valueType,
        out PropertyInfo? lengthProperty,
        out PropertyInfo? integerIndexer,
        out MethodInfo? toJsonMethod,
        out MethodInfo? genericContainsKeyMethod,
        out MethodInfo? genericIndexerSetMethod,
        out MethodInfo? genericRemoveMethod)
    {
        isCollection = typeof(ICollection).IsAssignableFrom(type);
        isEnumerable = typeof(IEnumerable).IsAssignableFrom(type);
        integerIndexer = _listType.IsAssignableFrom(type) ? _listIndexer : null;

        isDictionary = typeof(IDictionary).IsAssignableFrom(type);
        lengthProperty = type.GetProperty("Count") ?? type.GetProperty("Length");

        tryGetValueMethod = null;
        keysAccessor = null;
        keyType = null;
        valueType = null;
        genericContainsKeyMethod = null;
        genericIndexerSetMethod = null;
        genericRemoveMethod = null;
        // Find parameterless toJSON method to match JSON.stringify's expected signature
        // Note: The method name uses camelCase (toJSON) to match the JavaScript specification
        toJsonMethod = type.GetMethod("toJSON", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();

            // capture metadata for any IDictionary<TKey,TValue> / IReadOnlyDictionary<TKey,TValue>
            var isGenericDictionary = genericTypeDefinition == _genericDictionaryType;
            var isReadOnlyGenericDictionary = genericTypeDefinition == _readOnlyGenericDictionaryType;
            if (isGenericDictionary || isReadOnlyGenericDictionary)
            {
                var genericKeyType = type.GenericTypeArguments[0];
                tryGetValueMethod ??= type.GetMethod("TryGetValue");
                keysAccessor ??= type.GetProperty("Keys");
                keyType ??= genericKeyType;
                valueType ??= type.GenericTypeArguments[1];

                // ContainsKey is declared on both IDictionary<,> and IReadOnlyDictionary<,>
                genericContainsKeyMethod ??= type.GetMethod("ContainsKey", [genericKeyType]);

                if (isGenericDictionary)
                {
                    genericRemoveMethod ??= type.GetMethod("Remove", [genericKeyType]);
                    var indexerProperty = type.GetProperty("Item", [genericKeyType]);
                    genericIndexerSetMethod ??= indexerProperty?.GetSetMethod();
                }
            }

            isCollection |= genericTypeDefinition == typeof(IReadOnlyCollection<>) || genericTypeDefinition == typeof(ICollection<>);
            if (genericTypeDefinition == typeof(IList<>))
            {
                integerIndexer ??= type.GetProperty("Item");
            }
            isDictionary |= isGenericDictionary || isReadOnlyGenericDictionary;
        }
    }

    /// <summary>
    /// Whether the compiled lanes may cast <paramref name="key"/> straight to the dictionary's key type.
    /// A key that is not already an instance of it keeps the reflection path, whose
    /// <see cref="ArgumentException"/> for a mismatched key is the established behaviour.
    /// </summary>
    private bool KeyFitsCompiledLane(object key)
    {
        return IsStringKeyedGenericDictionary ? key is string : _keyType!.IsInstanceOfType(key);
    }

    /// <summary>
    /// Whether <paramref name="method"/>'s first parameter is exactly the key type
    /// <see cref="KeyFitsCompiledLane"/> guards with. The member and the key type are read off the same
    /// interface in <see cref="AnalyzeType"/>, so they agree in practice; verifying it keeps a type whose
    /// interface map surprises us on the reflection path rather than emitting a cast that could fail.
    /// </summary>
    private bool KeyParameterMatchesGuard(MethodInfo? method)
    {
        return method is not null
               && method.GetParameters() is [{ ParameterType: var keyParameterType }, ..]
               && keyParameterType == _keyType;
    }

    public bool TryGetDictionaryValue(object target, object key, out object? o)
    {
        if (!_compiledValueGetterResolved)
        {
            _compiledValueGetter = KeyParameterMatchesGuard(_tryGetValueMethod)
                ? CompiledKeyedAccessor.GetValueGetter(_tryGetValueMethod)
                : null;
            _compiledValueGetterResolved = true;
        }

        var getter = _compiledValueGetter;
        if (getter is not null && KeyFitsCompiledLane(key))
        {
            return TryGetDictionaryValueCompiled(getter, target, key, out o);
        }

        return TryGetDictionaryValueReflection(target, key, out o);
    }

    private static bool TryGetDictionaryValueCompiled(
        CompiledKeyedAccessor.KeyedValueGetter getter,
        object target,
        object key,
        out object? o)
    {
        try
        {
            return getter(target, key, out o);
        }
        catch (KeyNotFoundException)
        {
            o = null;
            return false;
        }
        catch (Exception exception) when (exception is not TargetInvocationException)
        {
            // reflection wraps whatever the dictionary throws, the compiled delegate rethrows it as-is:
            // normalize so the callers' TargetInvocationException handling stays identical
            throw new TargetInvocationException(exception);
        }
    }

    private bool TryGetDictionaryValueReflection(object target, object key, out object? o)
    {
        // IDictionary<,>.TryGetValue / IReadOnlyDictionary<,>.TryGetValue do not throw KeyNotFoundException,
        // but a custom implementation of either interface might — keep the catch defensively.
        try
        {
            object?[] parameters = [key, _valueType!.IsValueType ? Activator.CreateInstance(_valueType) : null];
            var result = _tryGetValueMethod!.Invoke(target, parameters) is true;
            o = parameters[1];
            return result;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is KeyNotFoundException)
        {
            o = null;
            return false;
        }
    }

    public bool ContainsDictionaryKey(object target, object key)
    {
        if (_genericContainsKeyMethod is null)
        {
            return false;
        }

        if (!_compiledContainsKeyResolved)
        {
            _compiledContainsKey = KeyParameterMatchesGuard(_genericContainsKeyMethod)
                ? CompiledKeyedAccessor.GetKeyPredicate(_genericContainsKeyMethod)
                : null;
            _compiledContainsKeyResolved = true;
        }

        var containsKey = _compiledContainsKey;
        if (containsKey is not null && KeyFitsCompiledLane(key))
        {
            return InvokeKeyPredicateCompiled(containsKey, target, key);
        }

        return _genericContainsKeyMethod.Invoke(target, [key]) is true;
    }

    public bool TrySetDictionaryValue(object target, object key, object? value)
    {
        if (_genericIndexerSetMethod is null)
        {
            return false;
        }

        if (!_compiledValueSetterResolved)
        {
            _compiledValueSetter = KeyParameterMatchesGuard(_genericIndexerSetMethod)
                                  && _genericIndexerSetMethod.GetParameters() is [_, { ParameterType: var valueParameterType }]
                                  && valueParameterType == _valueType
                ? CompiledKeyedAccessor.GetValueSetter(_genericIndexerSetMethod)
                : null;
            _compiledValueSetterResolved = true;
        }

        try
        {
            var setter = _compiledValueSetter;
            if (setter is not null && KeyFitsCompiledLane(key) && ValueFitsCompiledLane(value))
            {
                InvokeSetterCompiled(setter, target, key, value);
            }
            else
            {
                _genericIndexerSetMethod.Invoke(target, [key, value]);
            }

            return true;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is ArgumentException or InvalidCastException)
        {
            return false;
        }
    }

    public bool TryRemoveDictionaryValue(object target, object key)
    {
        if (_genericRemoveMethod is null)
        {
            return false;
        }

        if (!_compiledRemoveResolved)
        {
            _compiledRemove = KeyParameterMatchesGuard(_genericRemoveMethod)
                ? CompiledKeyedAccessor.GetKeyPredicate(_genericRemoveMethod)
                : null;
            _compiledRemoveResolved = true;
        }

        var remove = _compiledRemove;
        if (remove is not null && KeyFitsCompiledLane(key))
        {
            return InvokeKeyPredicateCompiled(remove, target, key);
        }

        return _genericRemoveMethod.Invoke(target, [key]) is true;
    }

    /// <summary>
    /// Whether the compiled setter may cast <paramref name="value"/> straight to the dictionary's value
    /// type. Reflection additionally performs widening conversions, which an <c>unbox.any</c> cannot do, so
    /// anything not already an instance keeps the reflection path — as does a <see langword="null"/> for a
    /// non-nullable value type, whose <see cref="ArgumentException"/> is the established behaviour.
    /// </summary>
    private bool ValueFitsCompiledLane(object? value)
    {
        var valueType = _valueType;
        if (valueType is null)
        {
            return false;
        }

        var underlyingType = Nullable.GetUnderlyingType(valueType);

        if (value is null)
        {
            // unbox.any of a null reference yields the empty Nullable<T>, which is what reflection stores too
            return !valueType.IsValueType || underlyingType is not null;
        }

        // a Nullable<T> never has a boxed form of its own: the value arrives boxed as T, and unbox.any
        // Nullable<T> accepts exactly that
        return valueType.IsInstanceOfType(value) || underlyingType?.IsInstanceOfType(value) == true;
    }

    private static bool InvokeKeyPredicateCompiled(
        CompiledKeyedAccessor.KeyedPredicate predicate,
        object target,
        object key)
    {
        try
        {
            return predicate(target, key);
        }
        catch (Exception exception) when (exception is not TargetInvocationException)
        {
            // reflection wraps whatever the dictionary throws, the compiled delegate rethrows it as-is:
            // normalize so the callers' TargetInvocationException handling stays identical
            throw new TargetInvocationException(exception);
        }
    }

    private static void InvokeSetterCompiled(
        CompiledKeyedAccessor.KeyedValueSetter setter,
        object target,
        object key,
        object? value)
    {
        try
        {
            setter(target, key, value);
        }
        catch (Exception exception) when (exception is not TargetInvocationException)
        {
            throw new TargetInvocationException(exception);
        }
    }
}
