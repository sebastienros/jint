using System;
using Jint.Runtime;

namespace Jint.Native
{
    /// <summary>
    /// The _object value of a <see cref="JsSymbol"/> is the [[Description]] internal slot.
    /// </summary>
    public sealed class JsSymbol : JsValue, IEquatable<JsSymbol>
    {
        private readonly string _value;

        public JsSymbol(string value)
        {
            _value = value;
        }

        public override Types Type => Types.Symbol;

        public override object ToObject()
        {
            return _value;
        }

        public override string AsSymbol()
        {
            if (_value == null)
            {
                throw new ArgumentException("The value is not defined");
            }

            return _value;
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

        public bool Equals(JsSymbol other)
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