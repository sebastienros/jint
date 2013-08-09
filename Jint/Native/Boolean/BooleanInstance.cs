using System;
using Jint.Native.Object;

namespace Jint.Native.Boolean
{
    public class BooleanInstance : ObjectInstance, IPrimitiveType
    {
        public BooleanInstance(ObjectInstance prototype)
            : base(prototype)
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
