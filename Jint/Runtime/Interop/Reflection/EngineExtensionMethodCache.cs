using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Jint.Runtime.Interop.Reflection
{
    /// <summary>
    /// A thread-safe extension method lookup that can be shared between engines, build based on extension methods
    /// provided via options.
    /// </summary>
    internal class EngineExtensionMethodCache
    {
        internal static EngineExtensionMethodCache Empty = new(new Dictionary<Type, List<MethodInfo>>());

        // starting point containing only extension methods targeting one type
        private readonly Dictionary<Type, List<MethodInfo>> _allExtensionMethods;

        // cache of all possibilities for type including base types and implemented interfaces
        private readonly ConcurrentDictionary<Type, MethodInfo[]> _extensionMethods = new();

        public EngineExtensionMethodCache(Dictionary<Type, List<MethodInfo>> extensionMethods)
        {
            _allExtensionMethods = extensionMethods;
        }

        public bool HasMethods => _allExtensionMethods.Count > 0;

        public bool TryGetExtensionMethods(Type objectType, out MethodInfo[] methods)
        {
            methods = _extensionMethods.GetOrAdd(objectType, t =>
            {
                var results = new List<MethodInfo>();
                if (_allExtensionMethods.TryGetValue(t, out var ownExtensions))
                {
                    results.AddRange(ownExtensions);
                }

                foreach (var parentType in GetParentTypes(t))
                {
                    if (_allExtensionMethods.TryGetValue(parentType, out var parentExtensions))
                    {
                        results.AddRange(parentExtensions);
                    }
                }

                return results.ToArray();
            });

            return methods.Length > 0;
        }

        private static IEnumerable<Type> GetParentTypes(Type type)
        {
            // is there any base type?
            if (type == null)
            {
                yield break;
            }

            // return all implemented or inherited interfaces
            foreach (var i in type.GetInterfaces())
            {
                yield return i;
            }

            // return all inherited types
            var currentBaseType = type.BaseType;
            while (currentBaseType != null)
            {
                yield return currentBaseType;
                currentBaseType = currentBaseType.BaseType;
            }
        }
    }
}