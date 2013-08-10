using System;

namespace Jint.Native
{
    public class Undefined : IPrimitiveType
    {
        public static object Instance = new Undefined();

        private Undefined()
        {   
        }

        public override string ToString()
        {
            return "undefined";
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
