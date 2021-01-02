using System;
using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Number
{
    public class NumberInstance : ObjectInstance, IPrimitiveInstance
    {
        private static readonly long NegativeZeroBits = BitConverter.DoubleToInt64Bits(-0.0);

        public NumberInstance(Engine engine)
            : base(engine, ObjectClass.Number)
        {
        }

        public NumberInstance(Engine engine, JsNumber value)
            : base(engine, ObjectClass.Number)
        {
            NumberData = value;
        }

        Types IPrimitiveInstance.Type => Types.Number;

        JsValue IPrimitiveInstance.PrimitiveValue => NumberData;

        public JsNumber NumberData { get; set; }

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
}