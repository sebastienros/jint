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
        internal readonly string _value;

        public JsSymbol(string value) : base(Types.Symbol)
        {
            _value = value;
        }

        public override object ToObject()
        {
            return _value;
        }

        public override string ToString()
        {
            return "Symbol(" + _value + ")";
        }

        public override bool Equals(JsValue obj)
        {
            return ReferenceEquals(this, obj);
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