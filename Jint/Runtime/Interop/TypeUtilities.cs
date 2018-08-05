using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jint.Runtime.Interop
{
    internal static class TypeUtilities
    {
        public static IEnumerable<PropertyInfo> GetProperties(Type type)
        {
            DiscoveryModes discoveryMode = type.GetCustomAttribute<JintClassAttribute>()?.DiscoveryMode ?? DiscoveryModes.OptOut;

            foreach (var p in type.GetProperties())
            {
                if (p.GetCustomAttribute<JintIgnoreAttribute>() != null)
                {
                    continue;
                }

                if (discoveryMode == DiscoveryModes.OptIn && p.GetCustomAttribute<JintPropertyAttribute>() == null)
                {
                    continue;
                }

                yield return p;
            }
        }

        public static IEnumerable<PropertyInfo> GetProperties(Type type, BindingFlags bindingFlags)
        {
            DiscoveryModes discoveryMode = type.GetCustomAttribute<JintClassAttribute>()?.DiscoveryMode ?? DiscoveryModes.OptOut;

            foreach (var p in type.GetProperties(bindingFlags))
            {
                if (p.GetCustomAttribute<JintIgnoreAttribute>() != null)
                {
                    continue;
                }

                if (discoveryMode == DiscoveryModes.OptIn && p.GetCustomAttribute<JintPropertyAttribute>() == null)
                {
                    continue;
                }

                yield return p;
            }
        }

        public static PropertyInfo GetProperty(Type type, string name, BindingFlags bindingFlags)
        {
            DiscoveryModes discoveryMode = type.GetCustomAttribute<JintClassAttribute>()?.DiscoveryMode ?? DiscoveryModes.OptOut;

            var p = type.GetProperty(name, bindingFlags);
            if (p != null)
            {
                if (p.GetCustomAttribute<JintIgnoreAttribute>() != null)
                {
                    return null;
                }

                if (discoveryMode == DiscoveryModes.OptIn && p.GetCustomAttribute<JintPropertyAttribute>() == null)
                {
                    return null;
                }
            }

            return p;
        }

        public static IEnumerable<FieldInfo> GetFields(Type type, BindingFlags bindingFlags)
        {
            DiscoveryModes discoveryMode = type.GetCustomAttribute<JintClassAttribute>()?.DiscoveryMode ?? DiscoveryModes.OptOut;

            foreach (var f in type.GetFields(bindingFlags))
            {
                if (f.GetCustomAttribute<JintIgnoreAttribute>() != null)
                {
                    continue;
                }

                if (discoveryMode == DiscoveryModes.OptIn && f.GetCustomAttribute<JintFieldAttribute>() == null)
                {
                    continue;
                }

                yield return f;
            }
        }

        public static FieldInfo GetField(Type type, string name, BindingFlags bindingFlags)
        {
            DiscoveryModes discoveryMode = type.GetCustomAttribute<JintClassAttribute>()?.DiscoveryMode ?? DiscoveryModes.OptOut;

            var f = type.GetField(name, bindingFlags);
            if (f != null)
            {
                if (f.GetCustomAttribute<JintIgnoreAttribute>() != null)
                {
                    return null;
                }

                if (discoveryMode == DiscoveryModes.OptIn && f.GetCustomAttribute<JintFieldAttribute>() == null)
                {
                    return null;
                }
            }

            return f;
        }

        public static IEnumerable<MethodInfo> GetMethods(Type type)
        {
            DiscoveryModes discoveryMode = type.GetCustomAttribute<JintClassAttribute>()?.DiscoveryMode ?? DiscoveryModes.OptOut;

            foreach (var m in type.GetMethods())
            {
                if (m.GetCustomAttribute<JintIgnoreAttribute>() != null)
                {
                    continue;
                }

                if (discoveryMode == DiscoveryModes.OptIn && m.GetCustomAttribute<JintMethodAttribute>() == null)
                {
                    continue;
                }

                yield return m;
            }
        }

        public static IEnumerable<MethodInfo> GetMethods(Type type, BindingFlags bindingFlags)
        {
            DiscoveryModes discoveryMode = type.GetCustomAttribute<JintClassAttribute>()?.DiscoveryMode ?? DiscoveryModes.OptOut;

            foreach (var m in type.GetMethods(bindingFlags))
            {
                if (m.GetCustomAttribute<JintIgnoreAttribute>() != null)
                {
                    continue;
                }

                if (discoveryMode == DiscoveryModes.OptIn && m.GetCustomAttribute<JintMethodAttribute>() == null)
                {
                    continue;
                }

                yield return m;
            }
        }

        public static IEnumerable<ConstructorInfo> GetConstructors(Type type, BindingFlags bindingFlags)
        {
            DiscoveryModes discoveryMode = type.GetCustomAttribute<JintClassAttribute>()?.DiscoveryMode ?? DiscoveryModes.OptOut;

            foreach (var c in type.GetConstructors(bindingFlags))
            {
                if (c.GetCustomAttribute<JintIgnoreAttribute>() != null)
                {
                    continue;
                }

                if (discoveryMode == DiscoveryModes.OptIn && c.GetCustomAttribute<JintConstructorAttribute>() == null)
                {
                    continue;
                }

                yield return c;
            }
        }
    }
}
