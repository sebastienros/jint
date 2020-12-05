using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jint.Extensions;

namespace Jint.Runtime.Interop.Reflection
{
    internal static class ExtensionMethodCache
    {
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, List<MethodInfo>>> CachedExtensionMethodsTypes = new();

        private static readonly ConcurrentDictionary<string, EngineExtensionMethodCache> CachedEngineExtensionMethods = new();

        internal static EngineExtensionMethodCache GetEngineExtensionMethods(List<Type> extensionMethodsTypes)
        {
            // we need to cache by key so we always give the same results per engine's options
            var key = string.Join("#", extensionMethodsTypes.Select(x => x.FullName));

            return CachedEngineExtensionMethods.GetOrAdd(key, _ =>
            {
                var engineInstanceExtensionMethods = new Dictionary<Type, List<MethodInfo>>();
                foreach (var extensionMethodClassType in extensionMethodsTypes)
                {
                    var dict = CachedExtensionMethodsTypes.GetOrAdd(extensionMethodClassType, BuildDictionaryForType);
                    foreach (var kv in dict)
                    {
                        if (!engineInstanceExtensionMethods.TryGetValue(kv.Key, out var list))
                        {
                            list = new List<MethodInfo>();
                            engineInstanceExtensionMethods[kv.Key] = list;
                        }

                        list.AddRange(kv.Value);
                    }
                }

                return new EngineExtensionMethodCache(engineInstanceExtensionMethods);
            });
        }

        private static ConcurrentDictionary<Type, List<MethodInfo>> BuildDictionaryForType(Type type)
        {
            var extensionMethodsDictionary = new ConcurrentDictionary<Type, List<MethodInfo>>();
            foreach (var methodInfo in type.GetExtensionMethods())
            {
                var firstParameterType = methodInfo.GetParameters()[0].ParameterType;
                extensionMethodsDictionary.AddOrUpdate(
                    firstParameterType,
                    _ => new List<MethodInfo> {methodInfo},
                    (_, list) => new List<MethodInfo>(list) {methodInfo}
                );
            }

            return extensionMethodsDictionary;
        }
    }
}