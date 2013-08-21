using System;
using Jint.Native.Object;

namespace Jint.Native.Date
{
    public class DateInstance : ObjectInstance, IPrimitiveType
    {
        public DateInstance(Engine engine)
            : base(engine)
        {
        }

        public override string Class
        {
            get
            {
                return "Date";
            }
        }

        TypeCode IPrimitiveType.TypeCode
        {
            get { return TypeCode.DateTime; }
        }

        object IPrimitiveType.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public DateTime PrimitiveValue { get; set; }
    }
}
