using Jint.Runtime;

namespace Jint.Native;

public partial class JsValue : IConvertible
{
    TypeCode IConvertible.GetTypeCode()
    {
        var type = _type & ~InternalTypes.InternalFlags;
        return type switch
        {
            InternalTypes.Boolean => TypeCode.Boolean,
            InternalTypes.String => TypeCode.String,
            InternalTypes.Number => TypeCode.Double,
            InternalTypes.Integer => TypeCode.Int32,
            InternalTypes.PrivateName => TypeCode.String,
            InternalTypes.Undefined => TypeCode.Object,
            InternalTypes.Null => TypeCode.Object,
            InternalTypes.Object => TypeCode.Object,
            InternalTypes.PlainObject => TypeCode.Object,
            InternalTypes.Array => TypeCode.Object,
            _ => TypeCode.Empty
        };
    }

    bool IConvertible.ToBoolean(IFormatProvider? provider)
    {
        return this.AsBoolean();
    }

    byte IConvertible.ToByte(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    char IConvertible.ToChar(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    DateTime IConvertible.ToDateTime(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    decimal IConvertible.ToDecimal(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    double IConvertible.ToDouble(IFormatProvider? provider)
    {
        return this.AsNumber();
    }

    short IConvertible.ToInt16(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    int IConvertible.ToInt32(IFormatProvider? provider)
    {
        return this.AsInteger();
    }

    long IConvertible.ToInt64(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    sbyte IConvertible.ToSByte(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    float IConvertible.ToSingle(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    string IConvertible.ToString(IFormatProvider? provider)
    {
        return this.AsString();
    }

    object IConvertible.ToType(Type conversionType, IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    ushort IConvertible.ToUInt16(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    uint IConvertible.ToUInt32(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    ulong IConvertible.ToUInt64(IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }
}
