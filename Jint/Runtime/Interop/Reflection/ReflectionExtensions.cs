using System;

namespace Jint.Runtime.Interop.Reflection
{
    internal static class ReflectionExtensions
    {
        private static readonly Type nullableType = typeof(Nullable<>);

        public static bool IsNullable(this Type type)
        {
            return type is { IsGenericType: true } && type.GetGenericTypeDefinition() == nullableType;
        }

        public static bool IsNumeric(this Type type)
        {
            if (type == null || type.IsEnum)
            {
                return false;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsClrNumericCoercible(this Type type)
        {
            if (type == null || type.IsEnum)
            {
                return false;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return true;
                default:
                    return false;
            }
        }

        public static object AsNumberOfType(this double d, TypeCode type)
        {
            switch (type)
            {
                case TypeCode.Decimal:
                    return (decimal) d;
                case TypeCode.Double:
                    return d;
                case TypeCode.Int32:
                    return (int) d;
                case TypeCode.Int64:
                    return (long) d;
                default:
                    ExceptionHelper.ThrowArgumentException("Cannot convert " + type);
                    return null;
            }
        }
    }
}