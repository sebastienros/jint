using System.Numerics;
using Jint.Runtime;

namespace Jint.Native.TypedArray;

/// <summary>
/// Container for either double or BigInteger.
/// </summary>
internal readonly record struct TypedArrayValue(Types Type, double DoubleValue, BigInteger BigInteger)
{
    public static implicit operator TypedArrayValue(double value)
    {
        return new TypedArrayValue(Types.Number, value, default);
    }

    public static implicit operator TypedArrayValue(BigInteger value)
    {
        return new TypedArrayValue(Types.BigInt, default, value);
    }

    public JsValue ToJsValue()
    {
        return Type == Types.Number
            ? JsNumber.Create(DoubleValue)
            : JsBigInt.Create(BigInteger);
    }
}
