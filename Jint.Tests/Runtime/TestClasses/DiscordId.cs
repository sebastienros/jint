#nullable enable

using Jint.Native.Date;

namespace Jint.Tests.Runtime.TestClasses;

public class DiscordTestClass
{
    public string Echo(DiscordId id)
    {
        return id.ToString();
    }

    public DiscordId Create(uint value)
    {
        return new DiscordId(value);
    }
}

public readonly struct DiscordId : IConvertible, IEquatable<DiscordId>
{
    private readonly string? _value;

    private string Value => _value ?? "0";

    public override string ToString() => Value;

    public DiscordId(string s)
    {
        if (ulong.TryParse(s, out _))
            _value = s;
        else
            throw new FormatException($"{nameof(s)} must consist of decimal digits and cannot be too large");
    }

    public DiscordId(ulong u)
    {
        _value = u.ToString();
    }

    public override bool Equals(object? obj) => obj is DiscordId id && Equals(id);
    public bool Equals(DiscordId id) => _value == id._value;

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public DateTimeOffset CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds((long)((ulong.Parse(Value) >> 22) + (ulong) (DateConstructor.Epoch.Ticks / 1000)));

    TypeCode IConvertible.GetTypeCode() => TypeCode.String;
    bool IConvertible.ToBoolean(IFormatProvider? provider) => Convert.ToBoolean(Value, provider);
    byte IConvertible.ToByte(IFormatProvider? provider) => Convert.ToByte(Value, provider);
    char IConvertible.ToChar(IFormatProvider? provider) => Convert.ToChar(Value, provider);
    DateTime IConvertible.ToDateTime(IFormatProvider? provider) => CreatedAt.UtcDateTime;
    decimal IConvertible.ToDecimal(IFormatProvider? provider) => Convert.ToDecimal(Value, provider);
    double IConvertible.ToDouble(IFormatProvider? provider) => Convert.ToDouble(Value, provider);
    short IConvertible.ToInt16(IFormatProvider? provider) => Convert.ToInt16(Value, provider);
    int IConvertible.ToInt32(IFormatProvider? provider) => Convert.ToInt32(Value, provider);
    long IConvertible.ToInt64(IFormatProvider? provider) => Convert.ToInt64(Value, provider);
    sbyte IConvertible.ToSByte(IFormatProvider? provider) => Convert.ToSByte(Value, provider);
    float IConvertible.ToSingle(IFormatProvider? provider) => Convert.ToSingle(Value, provider);
    string IConvertible.ToString(IFormatProvider? provider) => Value;
    object IConvertible.ToType(Type conversionType, IFormatProvider? provider) => Convert.ChangeType(Value, conversionType, provider);
    ushort IConvertible.ToUInt16(IFormatProvider? provider) => Convert.ToUInt16(Value, provider);
    uint IConvertible.ToUInt32(IFormatProvider? provider) => Convert.ToUInt32(Value, provider);
    ulong IConvertible.ToUInt64(IFormatProvider? provider) => Convert.ToUInt64(Value, provider);

    public static implicit operator DiscordId(ulong u) => new(u);
    public static explicit operator DiscordId(string s) => new(s);
}
