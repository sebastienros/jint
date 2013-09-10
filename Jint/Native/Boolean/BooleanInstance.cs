using System;
using Jint.Native.Object;
using Jint.Runtime;

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

        Types IPrimitiveType.Type
        {
            get { return Types.Boolean; }
        }

        object IPrimitiveType.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public bool PrimitiveValue { get; set; }
    }
}
