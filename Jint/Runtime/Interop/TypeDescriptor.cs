using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Jint.Runtime.Interop
{
    internal sealed class TypeDescriptor
    {
        private static readonly ConcurrentDictionary<Type, TypeDescriptor> _cache = new();
        private static readonly Type _genericDictionaryType = typeof(IDictionary<,>);
        private static readonly Type _stringType = typeof(string);

        private readonly PropertyInfo _stringIndexer;
        private readonly PropertyInfo _keysAccessor;

        private TypeDescriptor(Type type)
        {
            // check if object has any generic dictionary signature that accepts string as key
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType
                    && i.GetGenericTypeDefinition() == _genericDictionaryType
                    && i.GenericTypeArguments[0] == _stringType)
                {
                    _stringIndexer = i.GetProperty("Item");
                    _keysAccessor = i.GetProperty("Keys");
                    break;
                }
            }

            IsDictionary = _stringIndexer is not null || typeof(IDictionary).IsAssignableFrom(type);

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
        public bool IsStringKeyedGenericDictionary => _stringIndexer is not null;
        public bool IsEnumerable { get; }
        public PropertyInfo LengthProperty { get; }

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

        public bool TryGetValue(object target, string member, out object o)
        {
            if (!IsStringKeyedGenericDictionary)
            {
                ExceptionHelper.ThrowInvalidOperationException("Not a string-keyed dictionary");
            }

            // we could throw when indexing with an invalid key
            try
            {
                o = _stringIndexer.GetValue(target, new [] { member });
                return true;
            }
            catch (TargetInvocationException tie) when (tie.InnerException is KeyNotFoundException)
            {
                o = null;
                return false;
            }
        }

        public ICollection<string> GetKeys(object target)
        {
            if (!IsStringKeyedGenericDictionary)
            {
                ExceptionHelper.ThrowInvalidOperationException("Not a string-keyed dictionary");
            }

            return (ICollection<string>)_keysAccessor.GetValue(target);
        }
    }
}