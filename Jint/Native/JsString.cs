using System;
using System.Text;
using Jint.Native.Array;
using Jint.Runtime;

namespace Jint.Native
{
    public class JsString : JsValue, IEquatable<JsString>
    {
        private const int AsciiMax = 126;
        private static readonly JsString[] _charToJsValue;
        private static readonly JsString[] _charToStringJsValue;
        private static readonly JsString[] _intToStringJsValue;

        public static readonly JsString Empty = new JsString("");
        private static readonly JsString NullString = new JsString("null");
        internal static readonly JsString UndefinedString = new JsString("undefined");
        internal static readonly JsString ObjectString = new JsString("object");
        internal static readonly JsString FunctionString = new JsString("function");
        internal static readonly JsString BooleanString = new JsString("boolean");
        internal static readonly JsString StringString = new JsString("string");
        internal static readonly JsString NumberString = new JsString("number");
        internal static readonly JsString SymbolString = new JsString("symbol");
        internal static readonly JsString DefaultString = new JsString("default");
        internal static readonly JsString NumberZeroString = new JsString("0");
        internal static readonly JsString NumberOneString = new JsString("1");
        internal static readonly JsString TrueString = new JsString("true");
        internal static readonly JsString FalseString = new JsString("false");
        internal static readonly JsString LengthString = new JsString("length");

        internal string _value;

        static JsString()
        {
            _charToJsValue = new JsString[AsciiMax + 1];
            _charToStringJsValue = new JsString[AsciiMax + 1];

            for (var i = 0; i <= AsciiMax; i++)
            {
                _charToJsValue[i] = new JsString((char) i);
                _charToStringJsValue[i] = new JsString(((char) i).ToString());
            }

            _intToStringJsValue = new JsString[1024];
            for (var i = 0; i < _intToStringJsValue.Length; ++i)
            {
                _intToStringJsValue[i] = new JsString(TypeConverter.ToString(i));
            }
        }

        public JsString(string value) : this(value, InternalTypes.String)
        {
        }

        private JsString(string value, InternalTypes type) : base(type)
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

        public static bool operator ==(JsValue a, JsString b)
        {
            if (a is JsString s && b is object)
            {
                return s.ToString() == b.ToString();
            }

            if ((object) a == null)
            {
                return (object) b == null;
            }

            return (object) b != null && a.Equals(b);
        }

        public static bool operator ==(JsString a, JsValue b)
        {
            if (a is object && b is JsString s)
            {
                return s.ToString() == b.ToString();
            }

            if ((object) a == null)
            {
                return (object) b == null;
            }

            return (object) b != null && a.Equals(b);
        }

        public static bool operator !=(JsString a, JsValue b)
        {
            return !(a == b);
        }

        public static bool operator !=(JsValue a, JsString b)
        {
            return !(a == b);
        }

        public virtual char this[int index] => _value[index];

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

        public virtual int Length => _value.Length;

        internal static JsString Create(string value)
        {
            if (value.Length > 1)
            {
                return new JsString(value);
            }

            if (value.Length == 0)
            {
                return Empty;
            }

            var i = (uint) value[0];
            if (i < (uint) _charToStringJsValue.Length)
            {
                return _charToStringJsValue[i];
            }
            return new JsString(value);
        }

        internal static JsString Create(char value)
        {
            if (value < (uint) _charToJsValue.Length)
            {
                return _charToJsValue[value];
            }

            return new JsString(value);
        }

        internal static JsString Create(int value)
        {
            if (value < (uint) _intToStringJsValue.Length)
            {
                return _intToStringJsValue[value];
            }

            return new JsString(TypeConverter.ToString(value));
        }

        internal static JsValue Create(uint value)
        {
            if (value < (uint) _intToStringJsValue.Length)
            {
                return _intToStringJsValue[value];
            }

            return new JsString(TypeConverter.ToString(value));
        }

        internal static JsValue Create(ulong value)
        {
            if (value < (uint) _intToStringJsValue.Length)
            {
                return _intToStringJsValue[value];
            }

            return new JsString(TypeConverter.ToString(value));
        }

        public override string ToString()
        {
            return _value;
        }

        public ArrayInstance ToArray(Engine engine)
        {
            var array = engine.Array.ConstructFast((uint) _value.Length);
            for (int i = 0; i < _value.Length; ++i)
            {
                array.SetIndexValue((uint) i, _value[i], updateLength: false);
            }

            return array;
        }

        internal int IndexOf(string value, StringComparison comparisonType)
        {
            return ToString().IndexOf(value, comparisonType);
        }

        internal int IndexOf(char value)
        {
            return ToString().IndexOf(value);
        }

        internal string Substring(int startIndex, int length)
        {
            return ToString().Substring(startIndex, length);
        }

        internal string Substring(int startIndex)
        {
            return ToString().Substring(startIndex);
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

            return _value == other.ToString();
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is JsString other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        internal sealed class ConcatenatedString : JsString
        {
            private StringBuilder _stringBuilder;
            private bool _dirty;

            internal ConcatenatedString(string value, int capacity = 0)
                : base(value, InternalTypes.String | InternalTypes.RequiresCloning)
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

            public override char this[int index] => _stringBuilder?[index] ?? _value[index];

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

            public override int Length => _stringBuilder?.Length ?? _value?.Length ?? 0;

            public override object ToObject() => ToString();

            public override bool Equals(JsValue other)
            {
                if (other is ConcatenatedString cs)
                {
                    if (_stringBuilder != null && cs._stringBuilder != null)
                    {
                        return _stringBuilder.Equals(cs._stringBuilder);
                    }

                    return ToString() == cs.ToString();
                }

                if (other is JsString jsString)
                {
                    if (jsString._value.Length != Length)
                    {
                        return false;
                    }

                    return ToString() == jsString._value;
                }

                return base.Equals(other);
            }

            public override int GetHashCode()
            {
                return _stringBuilder?.GetHashCode() ?? _value.GetHashCode();
            }

            internal override JsValue DoClone()
            {
                return new JsString(ToString());
            }
        }
    }
}
