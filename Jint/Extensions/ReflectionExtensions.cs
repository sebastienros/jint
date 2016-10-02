using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jint.Extensions
{
    internal static class ReflectionExtensions
    {
        internal static bool HasAttribute<T>(this MemberInfo member) where T : Attribute
        {
            return member.GetCustomAttributes<T>().Any();
        }

        internal static bool HasAttribute<T>(this ParameterInfo member) where T : Attribute
        {
            return member.GetCustomAttributes<T>().Any();
        }

        internal static MethodInfo GetMethod(this Type type, string methodName)
        {
            if (methodName == null)
                throw new ArgumentNullException(methodName);
            var methods = type.GetTypeInfo().DeclaredMethods.Where(x => x.IsPublic && x.Name == methodName).ToArray();
            if (methods.Length > 1)
                throw new AmbiguousMatchException();

            return methods.FirstOrDefault();
        }

        internal static bool IsInstanceOfType(this Type src, object obj)
        {
            return obj != null && src.GetTypeInfo().IsAssignableFrom(obj.GetType().GetTypeInfo());
        }

        internal static PropertyInfo[] GetAllNonStaticProperties(this Type src)
        {
            return src.GetTypeInfo()
                    .DeclaredProperties.Where(
                        x =>
                            (x.GetMethod != null && !x.GetMethod.IsStatic) ||
                            (x.SetMethod != null && !x.SetMethod.IsStatic)).ToArray();
        }

        internal static PropertyInfo[] GetAllStaticProperties(this Type src)
        {
            return src.GetTypeInfo()
                    .DeclaredProperties.Where(
                        x =>
                            (x.GetMethod != null && x.GetMethod.IsStatic) ||
                            (x.SetMethod != null && x.SetMethod.IsStatic)).ToArray();
        }

        internal static PropertyInfo[] GetPublicStaticProperties(this Type src)
        {
            return src.GetTypeInfo()
                    .DeclaredProperties.Where(
                        x =>
                            (x.GetMethod != null && x.GetMethod.IsStatic && x.GetMethod.IsPublic) ||
                            (x.SetMethod != null && x.SetMethod.IsStatic && x.SetMethod.IsPublic)).ToArray();
        }
    }
}
