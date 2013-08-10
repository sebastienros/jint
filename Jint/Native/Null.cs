using System;

namespace Jint.Native
{
    public sealed class Null : IPrimitiveType
    {
        public static object Instance = new Null();

        private Null()
        {   
        }

        public override string ToString()
        {
            return "null";
        }

        public TypeCode TypeCode
        {
            get { return TypeCode.Empty; }
        }

        public object PrimitiveValue
        {
            get { return Instance; }
        }
    }
}
