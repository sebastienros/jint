using System;
using System.Linq;
using System.Reflection;

namespace Jint.Reflection
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
            var methods = type.GetRuntimeMethods().Where(x => x.IsPublic && x.Name == methodName).ToArray();
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
            return src.GetRuntimeProperties().Where(
                x =>
                    (x.GetMethod != null && !x.GetMethod.IsStatic) ||
                    (x.SetMethod != null && !x.SetMethod.IsStatic)).ToArray();
        }

        internal static PropertyInfo[] GetAllStaticProperties(this Type src)
        {
            return src.GetRuntimeProperties().Where(
                x =>
                    (x.GetMethod != null && x.GetMethod.IsStatic) ||
                    (x.SetMethod != null && x.SetMethod.IsStatic)).ToArray();
        }

        internal static PropertyInfo[] GetPublicStaticProperties(this Type src)
        {
            return src.GetRuntimeProperties().Where(
                x =>
                    (x.GetMethod != null && x.GetMethod.IsStatic && x.GetMethod.IsPublic) ||
                    (x.SetMethod != null && x.SetMethod.IsStatic && x.SetMethod.IsPublic)).ToArray();
        }

        internal static PropertyInfo[] GetPublicProperties(this Type src)
        {
            return src.GetRuntimeProperties().Where(
                x =>
                    (x.GetMethod != null && x.GetMethod.IsPublic) ||
                    (x.SetMethod != null && x.SetMethod.IsPublic) || x.IsExplicitInterfaceImplementation()).ToArray();
        }

        internal static PropertyInfo GetPublicProperty(this Type src, string propertyName)
        {
            return src.GetRuntimeProperties().FirstOrDefault(
                x =>
                    (((x.GetMethod != null && x.GetMethod.IsPublic) ||
                      (x.SetMethod != null && x.SetMethod.IsPublic)) &&
                     string.Equals(propertyName, x.Name, StringComparison.OrdinalIgnoreCase) ||
                     (x.IsExplicitInterfaceImplementation() &&
                      x.Name.EndsWith("." + propertyName, StringComparison.OrdinalIgnoreCase))));
        }

        internal static MethodInfo[] GetPublicMethods(this Type src, string methodName)
        {
            return src.GetRuntimeMethods().Where(
                x =>
                    x.IsExplicitInterfaceImplementation()
                        ? x.Name.EndsWith("." + methodName, StringComparison.OrdinalIgnoreCase) 
                        :  string.Equals(methodName, x.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        }


        internal static bool IsExplicitInterfaceImplementation(this MethodInfo method)
        {
            // Check all interfaces implemented in the type that declares
            // the method we want to check, with this we'll exclude all methods
            // that don't implement an interface method
            var declaringType = method.DeclaringType;
            foreach (var implementedInterface in declaringType.GetTypeInfo().ImplementedInterfaces)
            {

                var mapping = declaringType.GetTypeInfo().GetRuntimeInterfaceMap(implementedInterface);

                // If interface isn't implemented in the type that owns
                // this method then we can ignore it (for sure it's not
                // an explicit implementation)
                if (mapping.TargetType != declaringType)
                    continue;

                // Is this method the implementation of this interface?
                int methodIndex = Array.IndexOf(mapping.TargetMethods, method);
                if (methodIndex == -1)
                    continue;

                // Is it true for any language? Can we just skip this check?
                if (!method.IsFinal || !method.IsVirtual)
                    return false;

                // It's not required in all languages to implement every method
                // in the interface (if the type is abstract)
                string methodName = "";
                if (mapping.InterfaceMethods[methodIndex] != null)
                    methodName = mapping.InterfaceMethods[methodIndex].Name;

                // If names don't match then it's explicit
                if (!method.Name.Equals(methodName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        internal static bool IsExplicitInterfaceImplementation(this PropertyInfo property)
        {
            // At least one accessor must exists, I arbitrary check first for
            // "get" one. Note that in Managed C++ (not C++ CLI) these methods
            // are logically separated so they may follow different rules (one of them
            // is explicit and the other one is not). It's a pretty corner case
            // so we may just ignore it.
            if (property.GetMethod != null)
                return IsExplicitInterfaceImplementation(property.GetMethod);

            return IsExplicitInterfaceImplementation(property.SetMethod);
        }

    }
}
