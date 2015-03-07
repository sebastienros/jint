using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jint.Tests.Runtime.Domain
{
    public class ArrayConverterTestClass
    {
        public string MethodAcceptsArrayOfStrings(string[] arrayOfStrings)
        {
            return SerializeToString(arrayOfStrings);
        }

        public string MethodAcceptsArrayOfInt(int[] arrayOfInt)
        {
            return SerializeToString(arrayOfInt);
        }

        public string MethodAcceptsArrayOfBool(int[] arrayOfBool)
        {
            return SerializeToString(arrayOfBool);
        }

        private static string SerializeToString<T>(IEnumerable<T> array)
        {
            return String.Join(",", array);
        }
    }

    public class ArrayConverterItem : IConvertible
    {
        private readonly int _value;

        public ArrayConverterItem(int value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return ToString(CultureInfo.InvariantCulture);
        }

        public string ToString(IFormatProvider provider)
        {
            return _value.ToString(provider);
        }

        public int ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(_value, provider);
        }

        #region NotImplemented
        public TypeCode GetTypeCode()
        {
            throw new NotImplementedException();
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public char ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public byte ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public short ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public float ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public double ToDouble(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            throw new NotImplementedException();
        }
        #endregion


    }
}
