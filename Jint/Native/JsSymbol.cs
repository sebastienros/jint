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

        internal JsSymbol(string value) : this(new JsString(value))
        {
        }

        internal JsSymbol(JsValue value) : base(Types.Symbol)
        {
            _value = value;
        }

        public override object ToObject()
        {
            return _value;
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
            return ReferenceEquals(this, other);
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }
    }
}