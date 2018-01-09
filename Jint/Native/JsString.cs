using System.Diagnostics.Contracts;
using System.Text;
using Jint.Runtime;

namespace Jint.Native
{
    public class JsString : JsValue
    {
        public JsString(string value) : base(value)
        {
        }

        public virtual JsString Append(JsValue jsValue)
        {
            return new ConcatenatedString(string.Concat(TypeConverter.ToString(this), TypeConverter.ToString(jsValue)));
        }

        public override bool Equals(JsValue other)
        {
            if (other.IsString())
            {
                var otherString = other.AsString();
                return AsString().Equals(otherString);
            }

            return base.Equals(other);
        }

        internal class ConcatenatedString : JsString
        {
            private readonly StringBuilder _stringBuilder;
            private bool _dirty;

            internal ConcatenatedString(string value) : base(value)
            {
                _stringBuilder = new StringBuilder(value);
            }

            [Pure]
            public override string AsString()
            {
                if (_dirty)
                {
                    _object = _stringBuilder.ToString();
                    _dirty = false;
                }

                return base.AsString();
            }

            public override JsString Append(JsValue jsValue)
            {
                var value = TypeConverter.ToString(jsValue);
                _stringBuilder.Append(value);
                _dirty = true;

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