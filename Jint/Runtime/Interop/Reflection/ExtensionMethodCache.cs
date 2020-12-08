using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Jint.Extensions;

namespace Jint.Runtime.Interop.Reflection
{
    /// <summary>
    /// A extension method lookup that can be shared between engines, build based on extension methods provided via options.
    /// </summary>
    internal class ExtensionMethodCache
    {
        internal static readonly ExtensionMethodCache Empty = new(new Dictionary<Type, MethodInfo[]>());

        // starting point containing only extension methods targeting one type, based on given options configuration
        private readonly Dictionary<Type, MethodInfo[]> _allExtensionMethods;

        // cache of all possibilities for type including base types and implemented interfaces
        private Dictionary<Type, MethodInfo[]> _extensionMethods = new();

        private ExtensionMethodCache(Dictionary<Type, MethodInfo[]> extensionMethods)
        {
            _allExtensionMethods = extensionMethods;
        }
		
        internal static ExtensionMethodCache Build(List<Type> extensionMethodContainerTypes)
        {
            var methodsByTarget = extensionMethodContainerTypes
                .SelectMany(x => x.GetExtensionMethods())
                .GroupBy(x => x.GetParameters()[0].ParameterType)
                .ToDictionary(x => x.Key, x => x.ToArray());

            return new ExtensionMethodCache(methodsByTarget);
        }

        public bool HasMethods => _allExtensionMethods.Count > 0;

        public bool TryGetExtensionMethods(Type objectType, out MethodInfo[] methods)
        {
            var methodLookup = _extensionMethods;

            if (methodLookup.TryGetValue(objectType, out methods))
            {
                return true;
            }

            var results = new List<MethodInfo>();
            if (_allExtensionMethods.TryGetValue(objectType, out var ownExtensions))
            {
                results.AddRange(ownExtensions);
            }

            foreach (var parentType in GetParentTypes(objectType))
            {
                if (_allExtensionMethods.TryGetValue(parentType, out var parentExtensions))
                {
                    results.AddRange(parentExtensions);
                }
            }

            methods = results.ToArray();

            // racy, we don't care, worst case we'll catch up later
            Interlocked.CompareExchange(ref _extensionMethods, new Dictionary<Type, MethodInfo[]>(methodLookup)
            {
                [objectType] = methods
            }, methodLookup);

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