using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jint
{
    public static class TypeExtensions
    {
        private static Dictionary<TypeInfo, string> _typeNames = new Dictionary<TypeInfo, string>();
        
        private static Dictionary<Type, TypeCode> TypeCodes = new Dictionary<Type, TypeCode>
        {
                {typeof(object), TypeCode.Object},
                {typeof(string), TypeCode.String},
                {typeof(char), TypeCode.Char},
                {typeof(bool), TypeCode.Boolean},
                {typeof(SByte), TypeCode.SByte},
                {typeof(Int16), TypeCode.Int16},
                {typeof(UInt16), TypeCode.UInt16},
                {typeof(Int32), TypeCode.Int32},
                {typeof(UInt32), TypeCode.UInt32},
                {typeof(Int64), TypeCode.Int64},
                {typeof(UInt64), TypeCode.UInt64},
                {typeof(Single), TypeCode.Single},
                {typeof(Double), TypeCode.Double},
                {typeof(Decimal), TypeCode.Decimal},
                {typeof(DateTime), TypeCode.DateTime}
        };

        public static TypeCode GetTypeCode(this Type type)
        {
            TypeCode typeCode;
            if (TypeCodes.TryGetValue(type, out typeCode))
            {
                return typeCode;
            }

            return TypeCode.Empty;
        }
    }
}
