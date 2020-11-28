using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jint.Extensions;

namespace Jint
{
    internal static class ExtensionMethodsCache
    {
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, List<MethodInfo>>> CachedExtensionMethodsTypes = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, List<MethodInfo>>>();


        internal static Dictionary<Type, MethodInfo[]> RegisterExtensionMethods(IEnumerable<Type> extensionMethodsTypes)
        {
            var engineInstanceExtensionMethods = new ConcurrentDictionary<Type, List<MethodInfo>>();
            foreach (var extensionMethodClassType in extensionMethodsTypes)
            {
                var dict = GetOrAdd(extensionMethodClassType);
                foreach (var kv in dict)
                {
                    engineInstanceExtensionMethods.AddOrUpdate(
                        kv.Key,
                        type => kv.Value,
                        (type, list) =>
                        {
                            var nList = new List<MethodInfo>(list);
                            nList.AddRange(kv.Value);
                            return nList;
                        }
                    );
                }
            }

            return engineInstanceExtensionMethods.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        }

        private static ConcurrentDictionary<Type, List<MethodInfo>> GetOrAdd(Type classType)
        {
            return CachedExtensionMethodsTypes.GetOrAdd(classType, BuildDictionaryForType);
        }


        private static ConcurrentDictionary<Type, List<MethodInfo>> BuildDictionaryForType(Type type)
        {
            var extensionMethodsDictionary = new ConcurrentDictionary<Type, List<MethodInfo>>();
            foreach (var methodInfo in type.GetExtensionMethods())
            {
                var firstParameterType = methodInfo.GetParameters()[0].ParameterType;
                extensionMethodsDictionary.AddOrUpdate(
                    firstParameterType,
                    type1 => new List<MethodInfo> { methodInfo },
                    (type1, list) => new List<MethodInfo>(list) { methodInfo }
                );
            }

            return extensionMethodsDictionary;
        }
        
    }
}
