using System;
using Jint.Native.Object;

namespace Jint.Native.Boolean
{
    public sealed class BooleanInstance : ObjectInstance, IPrimitiveType
    {
        public BooleanInstance(Engine engine, ObjectInstance prototype)
            : base(engine, prototype)
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
