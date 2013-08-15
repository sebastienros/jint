using System;
using Jint.Native.Object;

namespace Jint.Native.Date
{
    public sealed class DateInstance : ObjectInstance, IPrimitiveType
    {
        public DateInstance(Engine engine, ObjectInstance prototype)
            : base(engine, prototype)
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
