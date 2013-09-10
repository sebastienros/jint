using System;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Number
{
    public class NumberInstance : ObjectInstance, IPrimitiveType
    {
        private static readonly long NegativeZeroBits = BitConverter.DoubleToInt64Bits(-0.0);

        public NumberInstance(Engine engine)
            : base(engine)
        {
        }

        public override string Class
        {
            get
            {
                return "Number";
            }
        }

        Types IPrimitiveType.Type
        {
            get { return Types.Number; }
        }

        object IPrimitiveType.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public double PrimitiveValue { get; set; }

        public static bool IsNegativeZero(double x)
        {
            return BitConverter.DoubleToInt64Bits(x) == NegativeZeroBits;
        }
    }
}
