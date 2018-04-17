using System;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.References
{
    /// <summary>
    /// Represents the Reference Specification Type
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.7
    /// </summary>
    public sealed class Reference
    {
        private JsValue _baseValue;
        private string _name;
        private bool _strict;

        public Reference(JsValue baseValue, string name, bool strict)
        {
            _baseValue = baseValue;
            _name = name;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue GetBase()
        {
            return _baseValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetReferencedName()
        {
            return _name;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStrict()
        {
            return _strict;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasPrimitiveBase()
        {
            return _baseValue.IsPrimitive();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUnresolvableReference()
        {
            return _baseValue.IsUndefined();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPropertyReference()
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-8.7
            return _baseValue.IsPrimitive() || (_baseValue.IsObject() && !(_baseValue is EnvironmentRecord));
        }

        internal Reference Reassign(JsValue baseValue, string name, bool strict)
        {
            _baseValue = baseValue;
            _name = name;
            _strict = strict;

            return this;
        }
    }
}
