using System;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Number
{
    public class NumberInstance : ObjectInstance, IPrimitiveInstance
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

        Types IPrimitiveInstance.Type
        {
            get { return Types.Number; }
        }

        JsValue IPrimitiveInstance.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public JsValue PrimitiveValue { get; set; }

        public static bool IsNegativeZero(double x)
        {
            return x == 0 && BitConverter.DoubleToInt64Bits(x) == NegativeZeroBits;
        }

        public static bool IsPositiveZero(double x)
        {
            return x == 0 && BitConverter.DoubleToInt64Bits(x) != NegativeZeroBits;
        }

    }
}
