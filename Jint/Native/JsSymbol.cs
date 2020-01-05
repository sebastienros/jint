using System;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Native
{
    /// <summary>
    /// The _object value of a <see cref="JsSymbol"/> is the [[Description]] internal slot.
    /// </summary>
    public sealed class JsSymbol : JsValue, IEquatable<JsSymbol>
    {
        internal readonly JsValue _value;
        private readonly Key _key;

        internal JsSymbol(JsValue value, int identity) : base(Types.Symbol)
        {
            _value = value;
            _key = new Key(value.IsUndefined() 
                ? ""
                : TypeConverter.ToString(value), identity);
        }

        public override object ToObject()
        {
            return _value;
        }

        internal override Key ToPropertyKey()
        {
            return _key;
        }

        public override string ToString()
        {
            var value = _value.IsUndefined() ? "" : _value.AsString();
            return "Symbol(" + value + ")";
        }

        public override bool Equals(JsValue obj)
        {
            return Equals(obj as JsSymbol);
        }

        public bool Equals(JsSymbol other)
        {
            return other != null && other._key == _key;
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }
    }
}