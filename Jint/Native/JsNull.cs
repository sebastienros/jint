using System;
using Jint.Runtime;

namespace Jint.Native
{
    public sealed class JsNull : JsValue, IEquatable<JsNull>
    {
        internal JsNull() : base(Types.Null)
        {
        }

        public override object ToObject()
        {
            return null;
        }

        public override string ToString()
        {
            return "null";
        }

        public override bool Equals(JsValue obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is JsNull s && Equals(s);
        }

        public bool Equals(JsNull other)
        {
            return !ReferenceEquals(null, other);
        }
    }
}