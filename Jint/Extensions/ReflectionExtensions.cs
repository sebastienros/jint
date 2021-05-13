using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Jint.Extensions
{
    internal static class ReflectionExtensions
    {
        internal static void SetValue(this MemberInfo memberInfo, object forObject, object value)
        {
            if (memberInfo.MemberType == MemberTypes.Field)
            {
                var fieldInfo = (FieldInfo) memberInfo;
                if (value != null && fieldInfo.FieldType.IsInstanceOfType(value))
                {
                    fieldInfo.SetValue(forObject, value);
                }
            }
            else if (memberInfo.MemberType == MemberTypes.Property)
            {
                var propertyInfo = (PropertyInfo) memberInfo;
                if (value != null && propertyInfo.PropertyType.IsInstanceOfType(value))
                {
                    propertyInfo.SetValue(forObject, value);
                }
            }
        }

        internal static Type GetDefinedType(this MemberInfo memberInfo)
        {
            return memberInfo switch
            {
                PropertyInfo propertyInfo => propertyInfo.PropertyType,
                FieldInfo fieldInfo => fieldInfo.FieldType,
                _ => null
            };
        }

        internal static IEnumerable<MethodInfo> GetExtensionMethods(this Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.IsExtensionMethod());
        }

        internal static IEnumerable<MethodInfo> GetOperatorOverloadMethods(this Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(m => m.IsSpecialName);
        }

        private static bool IsExtensionMethod(this MethodBase methodInfo)
        {
            return methodInfo.IsDefined(typeof(ExtensionAttribute), true);
        }
    }
}
