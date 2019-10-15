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
        internal JsValue _baseValue;
        private Key _name;
        internal bool _strict;

        public Reference(JsValue baseValue, in Key name, bool strict)
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
        public ref readonly Key GetReferencedName()
        {
            return ref _name;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStrict()
        {
            return _strict;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasPrimitiveBase()
        {
            return _baseValue._type != InternalTypes.Object && _baseValue._type != InternalTypes.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUnresolvableReference()
        {
            return _baseValue._type == InternalTypes.Undefined;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPropertyReference()
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-8.7
            return _baseValue._type != InternalTypes.Object && _baseValue._type != InternalTypes.None
                   || _baseValue._type == InternalTypes.Object && !(_baseValue is EnvironmentRecord);
        }

        internal Reference Reassign(JsValue baseValue, in Key name, bool strict)
        {
            _baseValue = baseValue;
            _name = name;
            _strict = strict;

            return this;
        }

        internal void AssertValid(Engine engine)
        {
            if (_strict && (_name == KnownKeys.Eval || _name == KnownKeys.Arguments) && _baseValue is EnvironmentRecord)
            {
                ExceptionHelper.ThrowSyntaxError(engine);
            }
        }
    }
}
