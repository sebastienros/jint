using System;
using System.Reflection;

namespace Jint.Extensions
{
    internal static class ReflectionExtensions
    {
        internal static void SetValue(this MemberInfo memberInfo, object forObject, object value)
        {
            switch (memberInfo.MemberType)
            {
                case MemberTypes.Field:
                    var fieldInfo = (FieldInfo) memberInfo;
                    if (value != null && fieldInfo.FieldType.IsAssignableFrom(value.GetType()))
                    {
                        fieldInfo.SetValue(forObject, value);
                    }

                    break;
                case MemberTypes.Property:
                    var propertyInfo = (PropertyInfo) memberInfo;
                    if (value != null && propertyInfo.PropertyType.IsAssignableFrom(value.GetType()))
                    {
                        propertyInfo.SetValue(forObject, value);
                    }
                    break;
            }
        }

        internal static Type GetDefinedType(this MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    return propertyInfo.PropertyType;
                case FieldInfo fieldInfo:
                    return fieldInfo.FieldType;
            }

            return null;
        }
    }
}
