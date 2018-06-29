using System;
using System.Reflection;

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
    }
}