using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Number;

internal class NumberInstance : ObjectInstance, IJsPrimitive
{
    private static readonly long NegativeZeroBits = BitConverter.DoubleToInt64Bits(-0.0);

    private protected NumberInstance(Engine engine, InternalTypes type)
        : base(engine, ObjectClass.Number, type)
    {
        NumberData = JsNumber.PositiveZero;
    }

    public NumberInstance(Engine engine, JsNumber value)
        : base(engine, ObjectClass.Number)
    {
        NumberData = value;
    }

    Types IJsPrimitive.Type => Types.Number;

    JsValue IJsPrimitive.PrimitiveValue => NumberData;

    public JsNumber NumberData { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNegativeZero(double x)
    {
        return x == 0 && BitConverter.DoubleToInt64Bits(x) == NegativeZeroBits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPositiveZero(double x)
    {
        return x == 0 && BitConverter.DoubleToInt64Bits(x) != NegativeZeroBits;
    }
}
