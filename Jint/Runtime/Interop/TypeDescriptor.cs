using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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

    public bool TryGetDictionaryValue(object target, object key, out object? o)
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
        return _genericContainsKeyMethod is not null
            && _genericContainsKeyMethod.Invoke(target, [key]) is true;
    }

    public bool TrySetDictionaryValue(object target, object key, object? value)
    {
        if (_genericIndexerSetMethod is null)
        {
            return false;
        }

        try
        {
            _genericIndexerSetMethod.Invoke(target, [key, value]);
            return true;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is ArgumentException or InvalidCastException)
        {
            return false;
        }
    }

    public bool TryRemoveDictionaryValue(object target, object key)
    {
        return _genericRemoveMethod is not null
            && _genericRemoveMethod.Invoke(target, [key]) is true;
    }
}
