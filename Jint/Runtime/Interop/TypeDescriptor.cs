using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Jint.Runtime.Interop
{
    internal sealed class TypeDescriptor
    {
        private static readonly ConcurrentDictionary<Type, TypeDescriptor> _cache = new();
        private static readonly Type _genericDictionaryType = typeof(IDictionary<,>);
        private static readonly Type _stringType = typeof(string);

        private readonly MethodInfo? _tryGetValueMethod;
        private readonly MethodInfo? _removeMethod;
        private readonly PropertyInfo? _keysAccessor;
        private readonly Type? _valueType;

        private TypeDescriptor(Type type)
        {
            // check if object has any generic dictionary signature that accepts string as key
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType
                    && i.GetGenericTypeDefinition() == _genericDictionaryType
                    && i.GenericTypeArguments[0] == _stringType)
                {
                    _tryGetValueMethod = i.GetMethod("TryGetValue");
                    _removeMethod = i.GetMethod("Remove");
                    _keysAccessor = i.GetProperty("Keys");
                    _valueType = i.GenericTypeArguments[1];
                    break;
                }
            }

            IsDictionary = _tryGetValueMethod is not null || typeof(IDictionary).IsAssignableFrom(type);

            // dictionaries are considered normal-object-like
            IsArrayLike = !IsDictionary && DetermineIfObjectIsArrayLikeClrCollection(type);

            IsEnumerable = typeof(IEnumerable).IsAssignableFrom(type);

            if (IsArrayLike)
            {
                LengthProperty = type.GetProperty("Count") ?? type.GetProperty("Length");
                IsIntegerIndexedArray = typeof(IList).IsAssignableFrom(type);
            }
        }

        public bool IsArrayLike { get; }
        public bool IsIntegerIndexedArray { get; }
        public bool IsDictionary { get; }
        public bool IsStringKeyedGenericDictionary => _tryGetValueMethod is not null;
        public bool IsEnumerable { get; }
        public PropertyInfo? LengthProperty { get; }

        public bool Iterable => IsArrayLike || IsDictionary || IsEnumerable;

        public static TypeDescriptor Get(Type type)
        {
            return _cache.GetOrAdd(type, t => new TypeDescriptor(t));
        }

        private static bool DetermineIfObjectIsArrayLikeClrCollection(Type type)
        {
            if (typeof(ICollection).IsAssignableFrom(type))
            {
                return true;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                if (!interfaceType.IsGenericType)
                {
                    continue;
                }

                if (interfaceType.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)
                    || interfaceType.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    return true;
                }
            }

            return false;
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
                var parameters = new[] { member, _valueType!.IsValueType ? Activator.CreateInstance(_valueType) : null };
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

        public bool Remove(object target, string key)
        {
            if (_removeMethod is null)
            {
                return false;
            }

            return (bool) _removeMethod.Invoke(target , new object[] { key })!;
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
