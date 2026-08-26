using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Extensions;

internal static class ReflectionExtensions
{
    private static readonly Type nullableType = typeof(Nullable<>);

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
            _ => null!
        };
    }

    internal static IEnumerable<MethodInfo> GetExtensionMethods([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] this Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static m => m.IsExtensionMethod());
    }

    private static readonly ConcurrentDictionary<Type, List<MethodInfo>> _operatorOverloadMethodCache = new();

    internal static List<MethodInfo> GetOperatorOverloadMethods([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] this Type type)
    {
        // The scan is written out here rather than in a GetOrAdd lambda because a lambda parameter cannot
        // carry [DynamicallyAccessedMembers]: passing `type` through the closure dropped its annotation and
        // reported the GetMethods call as unannotated in every trimming build, while the caller had already
        // promised exactly what the scan needs.
        if (_operatorOverloadMethodCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(static m => m.IsSpecialName)
            .ToList();

        // GetOrAdd's value overload rather than TryAdd, so that a race still hands every caller the same
        // list the dictionary holds - which is what the factory overload guaranteed.
        return _operatorOverloadMethodCache.GetOrAdd(type, methods);
    }

    private static bool IsExtensionMethod(this MethodBase methodInfo)
    {
        return methodInfo.IsDefined(typeof(ExtensionAttribute), inherit: true);
    }

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
                Throw.ArgumentException("Cannot convert " + type);
                return null;
        }
    }

    public static bool TryConvertViaTypeCoercion(
        Type? memberType,
        ValueCoercionType valueCoercionType,
        JsValue value,
        [NotNullWhen(true)] out object? converted)
    {
        if (value.IsInteger())
        {
            // safe and doesn't require configuration. The box must carry the declared member type:
            // reflection binders widen a boxed int to a long parameter, but a direct unbox (a
            // generic collection element write, a compiled setter, a plain cast) does not.
            if (memberType == typeof(int))
            {
                converted = value.AsInteger();
                return true;
            }

            if (memberType == typeof(long))
            {
                converted = (long) value.AsInteger();
                return true;
            }
        }

        if (memberType == typeof(bool) && (valueCoercionType & ValueCoercionType.Boolean) != ValueCoercionType.None)
        {
            converted = TypeConverter.ToBoolean(value);
            return true;
        }

        if (memberType == typeof(string)
            && !value.IsNullOrUndefined()
            && (valueCoercionType & ValueCoercionType.String) != ValueCoercionType.None)
        {
            // we know how to print out correct string presentation for primitives
            // that are non-null and non-undefined
            converted = TypeConverter.ToString(value);
            return true;
        }

        if (memberType is not null && memberType.IsClrNumericCoercible() && (valueCoercionType & ValueCoercionType.Number) != ValueCoercionType.None)
        {
            // we know how to print out correct string presentation for primitives
            // that are non-null and non-undefined
            var number = TypeConverter.ToNumber(value);
            converted = number.AsNumberOfType(Type.GetTypeCode(memberType));
            return true;
        }

        converted = null;
        return false;
    }
}
