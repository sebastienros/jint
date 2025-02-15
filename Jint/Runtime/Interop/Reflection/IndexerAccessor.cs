using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

#pragma warning disable IL2067

namespace Jint.Runtime.Interop.Reflection;

internal sealed class IndexerAccessor : ReflectionAccessor
{
    private readonly object _key;

    private readonly MethodInfo? _getter;
    private readonly MethodInfo? _setter;
    private readonly MethodInfo? _containsKey;

    private IndexerAccessor(PropertyInfo indexer, MethodInfo? containsKey, object key) : base(indexer.PropertyType)
    {
        Indexer = indexer;
        FirstIndexParameter = indexer.GetIndexParameters()[0];

        _containsKey = containsKey;
        _key = key;

        _getter = indexer.GetGetMethod();
        _setter = indexer.GetSetMethod();
    }

    internal PropertyInfo Indexer { get; }

    internal ParameterInfo FirstIndexParameter { get; }

    internal static bool TryFindIndexer(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] Type targetType,
        string propertyName,
        [NotNullWhen(true)] out IndexerAccessor? indexerAccessor,
        [NotNullWhen(true)] out PropertyInfo? indexer)
    {
        indexerAccessor = null;
        indexer = null;
        var paramTypeArray = new Type[1];

        // integer keys can be ambiguous as we only know string keys
        int? integerKey = null;

        if (int.TryParse(propertyName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intKeyTemp))
        {
            integerKey = intKeyTemp;
        }

        var filter = new Func<MemberInfo, bool>(m => engine.Options.Interop.TypeResolver.Filter(engine, targetType, m));

        // default indexer wins
        var descriptor = TypeDescriptor.Get(targetType);
        if (descriptor.IntegerIndexerProperty is not null && !filter(descriptor.IntegerIndexerProperty))
        {
            indexerAccessor = ComposeIndexerFactory(engine, targetType, descriptor.IntegerIndexerProperty, paramType: typeof(int), propertyName, integerKey, paramTypeArray);
            if (indexerAccessor != null)
            {
                indexer = descriptor.IntegerIndexerProperty;
                return true;
            }
        }

        // try to find first indexer having either public getter or setter with matching argument type
        PropertyInfo? fallbackIndexer = null;
        foreach (var candidate in targetType.GetProperties())
        {
            if (!filter(candidate))
            {
                continue;
            }

            var indexParameters = candidate.GetIndexParameters();
            if (indexParameters.Length != 1)
            {
                continue;
            }

            if (candidate.GetGetMethod() != null || candidate.GetSetMethod() != null)
            {
                var paramType = indexParameters[0].ParameterType;
                indexerAccessor = ComposeIndexerFactory(engine, targetType, candidate, paramType, propertyName, integerKey, paramTypeArray);
                if (indexerAccessor != null)
                {
                    if (paramType != typeof(string) ||  integerKey is null)
                    {
                        // exact match, we don't need to check for integer key
                        indexer = candidate;
                        return true;
                    }

                    if (fallbackIndexer is null)
                    {
                        // our fallback
                        fallbackIndexer = candidate;
                    }
                }
            }
        }

        if (fallbackIndexer is not null)
        {
            indexer = fallbackIndexer;
            // just to keep compiler happy, we know we have a value
            indexerAccessor ??= new IndexerAccessor(indexer, containsKey: null, key: null!);
            return true;
        }

        indexerAccessor = default;
        indexer = default;
        return false;
    }

    private static IndexerAccessor? ComposeIndexerFactory(
        Engine engine,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] Type targetType,
        PropertyInfo candidate,
        Type paramType,
        string propertyName,
        int? integerKey,
        Type[] paramTypeArray)
    {
        // check for known incompatible types
#if NET8_0_OR_GREATER
        if (typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(targetType)
            && (targetType != typeof(System.Text.Json.Nodes.JsonArray) || paramType != typeof(int))
            && (targetType != typeof(System.Text.Json.Nodes.JsonObject) || paramType != typeof(string)))
        {
            // we cannot access this[string] with anything else than JsonObject, otherwise itw will throw
            // we cannot access this[int] with anything else than JsonArray, otherwise itw will throw
            return null;
        }
#endif

        object? key = null;
        // int key is quite common case
        if (paramType == typeof(int))
        {
            if (integerKey is not null)
            {
                key = integerKey;
            }
        }
        else
        {
            engine.TypeConverter.TryConvert(propertyName, paramType, CultureInfo.InvariantCulture, out key);
        }

        if (key is not null)
        {
            // the key can be converted for this indexer
            var indexerProperty = candidate;
            // get contains key method to avoid index exception being thrown in dictionaries
            paramTypeArray[0] = paramType;
            var containsKeyMethod = targetType.GetMethod(nameof(IDictionary<string, string>.ContainsKey), paramTypeArray);
            if (containsKeyMethod is null && targetType.IsAssignableFrom(typeof(IDictionary)))
            {
                paramTypeArray[0] = typeof(object);
                containsKeyMethod = targetType.GetMethod(nameof(IDictionary.Contains), paramTypeArray);
            }

            return new IndexerAccessor(indexerProperty, containsKeyMethod, key);
        }

        // the key type doesn't work for this indexer
        return null;
    }


    public override bool Readable => Indexer.CanRead;

    public override bool Writable => Indexer.CanWrite;

    protected override object? DoGetValue(object target, string memberName)
    {
        if (_getter is null)
        {
            ExceptionHelper.ThrowInvalidOperationException("Indexer has no public getter.");
            return null;
        }

        object[] parameters = [_key];

        if (_containsKey != null)
        {
            if (_containsKey.Invoke(target, parameters) as bool? != true)
            {
                return JsValue.Undefined;
            }
        }

        try
        {
            return _getter.Invoke(target, parameters);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is KeyNotFoundException)
        {
            return JsValue.Undefined;
        }
    }

    protected override void DoSetValue(object target, string memberName, object? value)
    {
        if (_setter is null)
        {
            ExceptionHelper.ThrowInvalidOperationException("Indexer has no public setter.");
        }

        object?[] parameters = [_key, value];
        _setter.Invoke(target, parameters);
    }

    public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        if (_containsKey != null)
        {
            if (_containsKey.Invoke(target, [_key]) as bool? != true)
            {
                return PropertyDescriptor.Undefined;
            }
        }

        return new ReflectionDescriptor(engine, this, target, memberName, enumerable: true);
    }
}
