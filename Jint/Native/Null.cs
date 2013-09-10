using System;
using Jint.Runtime;

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

        public Types Type
        {
            get { return Types.Null; }
        }

        public object PrimitiveValue
        {
            get { return Instance; }
        }
    }
}
