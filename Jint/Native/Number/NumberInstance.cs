using System;
using Jint.Native.Object;

namespace Jint.Native.Number
{
    public sealed class NumberInstance : ObjectInstance, IPrimitiveType
    {
        public NumberInstance(Engine engine, ObjectInstance prototype)
            : base(engine, prototype)
        {
        }

        public override string Class
        {
            get
            {
                return "Number";
            }
        }

        TypeCode IPrimitiveType.TypeCode
        {
            get { return TypeCode.Double; }
        }

        object IPrimitiveType.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public double PrimitiveValue { get; set; }
    }
}
