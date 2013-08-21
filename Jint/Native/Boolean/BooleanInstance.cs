using System;
using Jint.Native.Object;

namespace Jint.Native.Boolean
{
    public class BooleanInstance : ObjectInstance, IPrimitiveType
    {
        public BooleanInstance(Engine engine)
            : base(engine)
        {
        }

        public override string Class
        {
            get
            {
                return "Boolean";
            }
        }

        TypeCode IPrimitiveType.TypeCode
        {
            get { return TypeCode.Boolean; }
        }

        object IPrimitiveType.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public bool PrimitiveValue { get; set; }
    }
}
