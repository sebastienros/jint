using System;
using System.Reflection;
using Jint.Runtime;

namespace Jint.Extensions
{
    internal static class ReflectionExtensions
    {
        internal static void SetValue(this MemberInfo memberInfo, object forObject, object? value)
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
                _ => ExceptionHelper.ThrowArgumentException<Type>()
            };
        }
    }
}
