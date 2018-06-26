using System;
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

        internal string _value;

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

        public JsString(string value) : base(Types.String)
        {
            _value = value;
        }

        public override object ToObject()
        {
            return _value;
        }

        public JsString(char value) : base(Types.String)
        {
            _value = value.ToString();
        }

        public virtual JsString Append(JsValue jsValue)
        {
            return new ConcatenatedString(string.Concat(_value, TypeConverter.ToString(jsValue)));
        }

        internal virtual JsString EnsureCapacity(int capacity)
        {
            return new ConcatenatedString(_value, capacity);
        }

        internal virtual bool IsNullOrEmpty()
        {
            return string.IsNullOrEmpty(_value);
        }

        internal static JsString Create(string value)
        {
            switch (value.Length)
            {
                case 0:
                    return Empty;
                case 1 when value[0] >= 0 && value[0] <= AsciiMax:
                    return _charToStringJsValue[value[0]];
                case 4 when value == Native.Null.Text:
                    return NullString;
                default:
                    return new JsString(value);
            }
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

            public override string ToString()
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

            internal override bool IsNullOrEmpty()
            {
                return _stringBuilder == null && string.IsNullOrEmpty(_value)
                    || _stringBuilder != null && _stringBuilder.Length == 0;
            }

            public override object ToObject()
            {
                return _stringBuilder.ToString();
            }

            public override bool Equals(JsValue other)
            {
                if (other is ConcatenatedString cs)
                {
                    return _stringBuilder.Equals(cs._stringBuilder);
                }

                if (other.Type == Types.String)
                {
                    var otherString = other.AsStringWithoutTypeCheck();
                    if (otherString.Length != _stringBuilder.Length)
                    {
                        return false;
                    }

                    return ToString() == otherString;
                }

                return base.Equals(other);
            }
        }
    }
}
