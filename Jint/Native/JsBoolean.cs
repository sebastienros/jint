using System;
using System.Diagnostics.Contracts;
using Jint.Runtime;

namespace Jint.Native
{
    public sealed class JsBoolean : JsValue, IEquatable<JsBoolean>
    {
        private readonly bool _value;

        public static readonly JsValue False = new JsBoolean(false);
        public static readonly JsValue True = new JsBoolean(true);

        public JsBoolean(bool value)
        {
            _value = value;
        }

        public override Types Type => Types.Boolean;

        [Pure]
        public override bool AsBoolean()
        {
            return _value;
        }

        public override object ToObject()
        {
            return _value;
        }

        public override string ToString()
        {
            return _value ? bool.TrueString : bool.FalseString;
        }

        public override bool Equals(JsValue obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (!(obj is JsBoolean number))
            {
                return false;
            }

            return Equals(number);
        }

        public bool Equals(JsBoolean other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return _value == other._value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}