using System;
using Jint.Native.Object;

namespace Jint.Native.String
{
    public class StringInstance : ObjectInstance, IPrimitiveType
    {
        public StringInstance(ObjectInstance prototype)
            : base(prototype)
        {
        }

        public override string Class
        {
            get
            {
                return "String";
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

        public string PrimitiveValue { get; set; }
    }
}
