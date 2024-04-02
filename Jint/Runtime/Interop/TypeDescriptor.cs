using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

#pragma warning disable IL2067
#pragma warning disable IL2075
#pragma warning disable IL2077

namespace Jint.Runtime.Interop
{
    internal sealed class TypeDescriptor
    {
        private static readonly ConcurrentDictionary<Type, TypeDescriptor> _cache = new();

        private static readonly Type _listType = typeof(IList);
        private static readonly Type _genericCollectionType = typeof(ICollection<>);
        private static readonly Type _genericListType = typeof(IList<>);
        private static readonly Type _genericReadonlyListType = typeof(IReadOnlyList<>);
        private static readonly PropertyInfo _listIndexer = typeof(IList).GetProperty("Item")!;

        private static readonly Type _genericDictionaryType = typeof(IDictionary<,>);
        private static readonly Type _stringType = typeof(string);

        private readonly MethodInfo? _tryGetValueMethod;
        private readonly MethodInfo? _removeMethod;
        private readonly MethodInfo? _remoteAtMethod;
        private readonly MethodInfo? _addMethod;
        private readonly PropertyInfo? _keysAccessor;
        private readonly Type? _valueType;
        private readonly Type? _arrayItemType;

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
                out var integerIndexer,
                out _arrayItemType,
                out _remoteAtMethod,
                out _addMethod);
            
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
            out PropertyInfo? integerIndexer,
            out Type? arrayItemType,
            out MethodInfo? remoteAtMethod,
            out MethodInfo? addMethod)
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
                out integerIndexer,
                out arrayItemType,
                out remoteAtMethod,
                out addMethod);

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
                    out var integerIndexerForSubType,
                    out var arrayItemTypeForSubType,
                    out var remoteAtMethodForSubType,
                    out var addMethodForSubType);
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
                integerIndexer ??= integerIndexerForSubType;
                arrayItemType ??= arrayItemTypeForSubType;
                remoteAtMethod ??= remoteAtMethodForSubType;
                addMethod ??= addMethodForSubType;
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
            out PropertyInfo? integerIndexer,
            out Type? arrayItemType,
            out MethodInfo? removeAtMethod,
            out MethodInfo? addMethod)
        {
            isCollection = typeof(ICollection).IsAssignableFrom(type);
            isCollection |= type.IsGenericType && type.GetGenericTypeDefinition() == _genericCollectionType;
            isEnumerable = typeof(IEnumerable).IsAssignableFrom(type);
            integerIndexer = _listType.IsAssignableFrom(type) ? _listIndexer : null;
            
            // generic list indexer
            arrayItemType = null;
            removeAtMethod = null;
            addMethod = null;
            
#pragma warning disable IL2055
#pragma warning disable IL2080
#pragma warning disable IL3050
            if (type.IsGenericType && type.GetGenericTypeDefinition() == _genericListType)
            {
                arrayItemType = type.GenericTypeArguments[0];
                integerIndexer ??= _genericListType.MakeGenericType(arrayItemType).GetProperty("Item");
                removeAtMethod = _genericListType.MakeGenericType(arrayItemType).GetMethod("RemoveAt");
                addMethod = _genericCollectionType.MakeGenericType(arrayItemType).GetMethod("Add");
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == _genericReadonlyListType)
            {
                arrayItemType = type.GenericTypeArguments[0];
                integerIndexer ??= _genericReadonlyListType.MakeGenericType(arrayItemType).GetProperty("Item");
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == _genericCollectionType)
            {
                arrayItemType = type.GenericTypeArguments[0];
                addMethod = _genericCollectionType.MakeGenericType(arrayItemType).GetMethod("Add");
            }
#pragma warning restore IL2055
#pragma warning restore IL2080
#pragma warning restore IL3050

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

        public bool TryGetIndexerValue(object target, int index, [NotNullWhen(true)] out object? o)
        {
            if (target is IList list)
            {
                if (index >= 0 && index < list.Count)
                {
                    o = list[index];
                    return o != null;
                }
                
                o = null;
                return false;
            }
            
            if (!IsIntegerIndexed)
            {
                ExceptionHelper.ThrowInvalidOperationException("Not an integer-indexed object");
            }
            
            // we could throw when indexing with an invalid key
            try
            {
                o = IntegerIndexerProperty!.GetValue(target, [index]);
                return o != null;
            }
            catch (TargetInvocationException tie) when (tie.InnerException is IndexOutOfRangeException)
            {
                o = null;
                return false;
            }
        }

        public bool Remove(object target, string key)
        {
            if (_removeMethod is null)
            {
                return false;
            }

            return (bool) _removeMethod.Invoke(target, [key])!;
        }

        public void RemoveAt(object target, int index)
        {
            if (!IsIntegerIndexed)
            {
                ExceptionHelper.ThrowInvalidOperationException("Not an integer-indexed object");
            }

            if (target is IList list)
            {
                list.RemoveAt(index);
            }
            else
            {
                _remoteAtMethod!.Invoke(target, [index]);
            }
        }

        public void AddDefault(object target)
        {
            if (!IsArrayLike)
            {
                ExceptionHelper.ThrowInvalidOperationException("Not an array-like object");
            }

            if (target is IList list)
            {
                list.Add(default);
            }
            else
            {
                if (_arrayItemType == null)
                {
                    ExceptionHelper.ThrowInvalidOperationException("Not a generic list");
                }
                
                _addMethod!.Invoke(target, [CreateDefaultInstance(_arrayItemType)]);
            }
        }
        
        internal static object? CreateDefaultInstance(Type type)
        {
            if (type.IsValueType)
            {
#pragma warning disable IL3050
                return Activator.CreateInstance(type);
#pragma warning restore IL3050
            }
            
            return null;
        }
        
        public int GetLength(object target)
        {
            if (target is ICollection collection)
            {
                return collection.Count;
            }
            
            if (LengthProperty is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Not an array-like object");
            }

            return (int) LengthProperty.GetValue(target)!;
        }

        public ICollection<string> GetKeys(object target)
        {
            if (!IsStringKeyedGenericDictionary)
            {
                ExceptionHelper.ThrowInvalidOperationException("Not a string-keyed dictionary");
            }

            return (ICollection<string>) _keysAccessor!.GetValue(target)!;
        }
    }
}
