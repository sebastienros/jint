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
    private static readonly Type _stringType = typeof(string);

    private readonly MethodInfo? _tryGetValueMethod;
    private readonly MethodInfo? _removeMethod;
    private readonly PropertyInfo? _keysAccessor;
    private readonly Type? _valueType;

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
            out _removeMethod,
            out _keysAccessor,
            out _valueType,
            out var lengthProperty,
            out var integerIndexer);

        IntegerIndexerProperty = integerIndexer;
        IsDictionary = _tryGetValueMethod is not null || isDictionary;

        // dictionaries are considered normal-object-like
        IsArrayLike = !IsDictionary && isCollection;

        IsEnumerable = isEnumerable;

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
    public bool IsStringKeyedGenericDictionary => _tryGetValueMethod is not null;
    public bool IsEnumerable { get; }
    public PropertyInfo? LengthProperty { get; }

    public bool Iterable => IsArrayLike || IsDictionary || IsEnumerable;

    public MethodInfo? RemoveMethod => _removeMethod;

    public PropertyInfo? KeysAccessor => _keysAccessor;

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
        out MethodInfo? removeMethod,
        out PropertyInfo? keysAccessor,
        out Type? valueType,
        out PropertyInfo? lengthProperty,
        out PropertyInfo? integerIndexer)
    {
        AnalyzeType(
            type,
            out isCollection,
            out isEnumerable,
            out isDictionary,
            out tryGetValueMethod,
            out removeMethod,
            out keysAccessor,
            out valueType,
            out lengthProperty,
            out integerIndexer);

        foreach (var t in type.GetInterfaces())
        {
#pragma warning disable IL2072
            AnalyzeType(
                t,
                out var isCollectionForSubType,
                out var isEnumerableForSubType,
                out var isDictionaryForSubType,
                out var tryGetValueMethodForSubType,
                out var removeMethodForSubType,
                out var keysAccessorForSubType,
                out var valueTypeForSubType,
                out var lengthPropertyForSubType,
                out var integerIndexerForSubType);
#pragma warning restore IL2072

            isCollection |= isCollectionForSubType;
            isEnumerable |= isEnumerableForSubType;
            isDictionary |= isDictionaryForSubType;

            tryGetValueMethod ??= tryGetValueMethodForSubType;
            removeMethod ??= removeMethodForSubType;
            keysAccessor ??= keysAccessorForSubType;
            valueType ??= valueTypeForSubType;
            lengthProperty ??= lengthPropertyForSubType;
            integerIndexer ??= integerIndexerForSubType;
        }
    }

    private static void AnalyzeType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)]
        Type type,
        out bool isCollection,
        out bool isEnumerable,
        out bool isDictionary,
        out MethodInfo? tryGetValueMethod,
        out MethodInfo? removeMethod,
        out PropertyInfo? keysAccessor,
        out Type? valueType,
        out PropertyInfo? lengthProperty,
        out PropertyInfo? integerIndexer)
    {
        isCollection = typeof(ICollection).IsAssignableFrom(type);
        isEnumerable = typeof(IEnumerable).IsAssignableFrom(type);
        integerIndexer = _listType.IsAssignableFrom(type) ? _listIndexer : null;

        isDictionary = typeof(IDictionary).IsAssignableFrom(type);
        lengthProperty = type.GetProperty("Count") ?? type.GetProperty("Length");

        tryGetValueMethod = null;
        removeMethod = null;
        keysAccessor = null;
        valueType = null;

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();

            // check if object has any generic dictionary signature that accepts string as key
            var b = genericTypeDefinition == _genericDictionaryType;
            if (b && type.GenericTypeArguments[0] == _stringType)
            {
                tryGetValueMethod = type.GetMethod("TryGetValue");
                removeMethod = type.GetMethod("Remove");
                keysAccessor = type.GetProperty("Keys");
                valueType = type.GenericTypeArguments[1];
            }

            isCollection |= genericTypeDefinition == typeof(IReadOnlyCollection<>) || genericTypeDefinition == typeof(ICollection<>);
            if (genericTypeDefinition == typeof(IList<>))
            {
                integerIndexer ??= type.GetProperty("Item");
            }
            isDictionary |= genericTypeDefinition == _genericDictionaryType;
        }
    }

    public bool TryGetValue(object target, string member, [NotNullWhen(true)] out object? o)
    {
        if (!IsStringKeyedGenericDictionary)
        {
            ExceptionHelper.ThrowInvalidOperationException("Not a string-keyed dictionary");
        }

        // we could throw when indexing with an invalid key
        try
        {
            object?[] parameters = [member, _valueType!.IsValueType ? Activator.CreateInstance(_valueType) : null];
            var result = (bool) _tryGetValueMethod!.Invoke(target, parameters)!;
            o = parameters[1];
            return result;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is KeyNotFoundException)
        {
            o = null;
            return false;
        }
    }
}
