using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Jint.Runtime.Interop
{
    internal class TypeDescriptor
    {
        private static readonly ConcurrentDictionary<Type, TypeDescriptor> _cache = new();

        private TypeDescriptor(Type type)
        {
            IsArrayLike = DetermineIfObjectIsArrayLikeClrCollection(type);
            IsDictionary = typeof(IDictionary).IsAssignableFrom(type) || typeof(IDictionary<string, object>).IsAssignableFrom(type);

            if (IsArrayLike)
            {
                LengthProperty = type.GetProperty("Count") ?? type.GetProperty("Length");
                IsIntegerIndexedArray = typeof(IList).IsAssignableFrom(type);
            }
        }

        public bool IsArrayLike { get; }
        public bool IsIntegerIndexedArray { get; }
        public bool IsDictionary { get; }
        public PropertyInfo LengthProperty { get; }

        public static TypeDescriptor Get(Type type)
        {
            return _cache.GetOrAdd(type, t => new TypeDescriptor(t));
        }

        private static bool DetermineIfObjectIsArrayLikeClrCollection(Type type)
        {
            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                // dictionaries are considered normal-object-like
                return false;
            }

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
    }
}