using System;
using Jint.Runtime;

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

        public Types Type
        {
            get { return Types.Undefined; }
        }

        public object PrimitiveValue
        {
            get { return Instance; }
        }
    }
}
