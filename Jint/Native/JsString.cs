using System;
using System.Diagnostics.Contracts;
using System.Text;
using Jint.Runtime;

namespace Jint.Native
{
    public class JsString : JsValue, IEquatable<JsString>
    {
        private const int AsciiMax = 126;
        private static readonly JsString[] _charToJsValue;
        private static readonly JsString[] _charToStringJsValue;

        private static readonly JsString Empty = new JsString("");
        private static readonly JsString NullString = new JsString("null");

        private string _value;

        static JsString()
        {
            _charToJsValue = new JsString[AsciiMax + 1];
            _charToStringJsValue = new JsString[AsciiMax + 1];

            for (int i = 0; i <= AsciiMax; i++)
            {
                _charToJsValue[i] = new JsString((char) i);
                _charToStringJsValue[i] = new JsString(((char) i).ToString());
            }
        }

        public JsString(string value)
        {
            _value = value;
        }

        public override object ToObject()
        {
            return _value;
        }

        public JsString(char value)
        {
            _value = value.ToString();
        }

        public override Types Type => Types.String;

        [Pure]
        public override string AsString()
        {
            if (_value == null)
            {
                throw new ArgumentException("The value is not defined");
            }

            return _value;
        }

        public virtual JsString Append(JsValue jsValue)
        {
            return new ConcatenatedString(string.Concat(_value, TypeConverter.ToString(jsValue)));
        }

        internal virtual JsString EnsureCapacity(int capacity)
        {
            return new ConcatenatedString(_value, capacity);
        }

        internal static JsString Create(string value)
        {
            if (value.Length <= 1)
            {
                if (value == "")
                {
                    return Empty;
                }

                if (value.Length == 1)
                {
                    if (value[0] >= 0 && value[0] <= AsciiMax)
                    {
                        return _charToStringJsValue[value[0]];
                    }
                }
            }
            else if (value == Native.Null.Text)
            {
                return NullString;
            }

            return new JsString(value);
        }

        internal static JsString Create(char value)
        {
            if (value >= 0 && value <= AsciiMax)
            {
                return _charToJsValue[value];
            }

            return new JsString(value);
        }

        public override string ToString()
        {
            return _value;
        }

        public override bool Equals(JsValue obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (!(obj is JsString s))
            {
                return false;
            }

            return Equals(s);
        }

        public bool Equals(JsString other)
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

        internal sealed class ConcatenatedString : JsString
        {
            private StringBuilder _stringBuilder;
            private bool _dirty;

            internal ConcatenatedString(string value, int capacity = 0) : base(value)
            {
                if (capacity > 0)
                {
                    _stringBuilder = new StringBuilder(value, capacity);
                }
                else
                {
                    _value = value;
                }
            }

            [Pure]
            public override string AsString()
            {
                if (_dirty)
                {
                    _value = _stringBuilder.ToString();
                    _dirty = false;
                }

                return _value;
            }

            public override JsString Append(JsValue jsValue)
            {
                var value = TypeConverter.ToString(jsValue);
                if (_stringBuilder == null)
                {
                    _stringBuilder = new StringBuilder(_value, _value.Length + value.Length);
                }

                _stringBuilder.Append(value);
                _dirty = true;

                return this;
            }

            internal override JsString EnsureCapacity(int capacity)
            {
                _stringBuilder.EnsureCapacity(capacity);
                return this;
            }

            public override bool Equals(JsValue other)
            {
                if (other is ConcatenatedString cs)
                {
                    return _stringBuilder.Equals(cs._stringBuilder);
                }

                if (other.Type == Types.String)
                {
                    var otherString = other.AsString();
                    if (otherString.Length != _stringBuilder.Length)
                    {
                        return false;
                    }

                    return AsString().Equals(otherString);
                }

                return base.Equals(other);
            }
        }
    }
}