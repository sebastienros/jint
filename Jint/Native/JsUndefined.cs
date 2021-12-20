using System;
using Jint.Runtime;

namespace Jint.Native
{
    public sealed class JsUndefined : JsValue, IEquatable<JsUndefined>
    {
        internal JsUndefined() : base(Types.Undefined)
        {
        }

        public override object ToObject()
        {
            return null;
        }

        public override string ToString()
        {
            return "undefined";
        }

        public override bool Equals(JsValue obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is JsUndefined s && Equals(s);
        }

        public bool Equals(JsUndefined other)
        {
            return !ReferenceEquals(null, other);
        }
    }
}