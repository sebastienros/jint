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
            if (extensionMethodContainerTypes.Count == 0)
            {
                return Empty;
            }

            Type GetTypeDefinition(Type type)
            {
                return type.IsConstructedGenericType && type.GenericTypeArguments.Any(x => x.IsGenericParameter) ?
                    type.GetGenericTypeDefinition() : type;
            }

            var methodsByTarget = extensionMethodContainerTypes
                .SelectMany(x => x.GetExtensionMethods())
                .GroupBy(x => GetTypeDefinition(x.GetParameters()[0].ParameterType))
                .ToDictionary(x => x.Key, x => x.ToArray());

            return new ExtensionMethodCache(methodsByTarget);
        }

        public bool HasMethods => _allExtensionMethods.Count > 0;

        private MethodInfo BindMethodGenericParameters(MethodInfo method)
        {
            if (method.IsGenericMethodDefinition && method.ContainsGenericParameters)
            {
                var methodGenerics = method.GetGenericArguments();
                var parameterList = Enumerable.Repeat(typeof(object), methodGenerics.Length).ToArray();

                try
                {
                    return method.MakeGenericMethod(parameterList);
                }
                catch
                {
                    // Generic parameter constraints failed probably.
                    // If it does not work, let it be. We don't need to do anything.
                }
            }
            return method;
        }


        public bool TryGetExtensionMethods(Type objectType, out MethodInfo[] methods)
        {
            var methodLookup = _extensionMethods;

            if (methodLookup.TryGetValue(objectType, out methods))
            {
                return methods.Length > 0;
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

            methods = results.Select(BindMethodGenericParameters).ToArray();

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

                if (i.IsConstructedGenericType)
                {
                    yield return i.GetGenericTypeDefinition();
                }
            }

            // return all inherited types
            var currentBaseType = type.BaseType;
            while (currentBaseType != null)
            {
                yield return currentBaseType;

                if (currentBaseType.IsConstructedGenericType)
                {
                    yield return currentBaseType.GetGenericTypeDefinition();
                }

                currentBaseType = currentBaseType.BaseType;
            }
        }
    }
}
