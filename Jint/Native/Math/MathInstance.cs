using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Math
{
    public sealed class MathInstance : ObjectInstance
    {
        public MathInstance(ObjectInstance prototype)
            : base(prototype)
        {
        }

        public override string Class
        {
            get
            {
                return "Math";
            }
        }

        public static MathInstance CreateMathObject(Engine engine, ObjectInstance prototype)
        {
            var math = new MathInstance(prototype);
            math.DefineOwnProperty("abs", new ClrDataDescriptor<MathInstance, double>(engine, Abs), false);

            return math;
        }

        private static double Abs(MathInstance thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return System.Math.Abs(x);
        }

    }
}
