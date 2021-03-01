using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Jint.Runtime.Interop
{
    internal class TypeDescriptor
    {
        private static readonly ConcurrentDictionary<Type, TypeDescriptor> _cache = new();

        private TypeDescriptor(Type type)
        {
            IsArrayLike = DetermineIfObjectIsArrayLikeClrCollection(type);
            IsIntegerIndexedArray = typeof(IList).IsAssignableFrom(type);
        }


        public bool IsArrayLike { get; }
        public bool IsIntegerIndexedArray { get; }

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

    }
}