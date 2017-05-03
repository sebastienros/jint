#if NETSTANDARD1_3
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Jint
{
    internal static class ReflectionExtensions
    {
        internal static bool IsEnum(this Type type)
        {
            return type.GetTypeInfo().IsEnum;
        }

        internal static bool IsGenericType(this Type type)
        {
            return type.GetTypeInfo().IsGenericType;
        }

        internal static bool IsValueType(this Type type)
        {
            return type.GetTypeInfo().IsValueType;
        }

        internal static bool HasAttribute<T>(this ParameterInfo member) where T : Attribute
        {
            return member.GetCustomAttributes<T>().Any();
        }

        internal static IEnumerable<MethodInfo> GetExtensionMethods(this Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.IsExtensionMethod());
        }

        internal static Boolean IsExtensionMethod(this MethodInfo methodInfo)
        {
            return methodInfo.IsDefined(typeof(ExtensionAttribute), true);
        }

    }
}
#else
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Jint
{
    internal static class ReflectionExtensions
    {
        internal static bool IsEnum(this Type type)
        {
            return type.IsEnum;
        }

        internal static bool IsGenericType(this Type type)
        {
            return type.IsGenericType;
        }

        internal static bool IsValueType(this Type type)
        {
            return type.IsValueType;
        }

        internal static bool HasAttribute<T>(this ParameterInfo member) where T : Attribute
        {
            return Attribute.IsDefined(member, typeof(T));
        }

        internal static MethodInfo GetMethodInfo(this Delegate d)
        {
            return d.Method;
        }

        internal static IEnumerable<MethodInfo> GetExtensionMethods(this Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.IsExtensionMethod());
        }

        internal static Boolean IsExtensionMethod(this MethodInfo methodInfo)
        {
            return methodInfo.IsDefined(typeof(ExtensionAttribute), true);
        }

    }
}
#endif
