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
        internal string _name;
        internal bool _strict;

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
            return _baseValue._type != Types.Object && _baseValue._type != Types.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUnresolvableReference()
        {
            return _baseValue._type == Types.Undefined;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPropertyReference()
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-8.7
            return _baseValue._type != Types.Object && _baseValue._type != Types.None
                   || _baseValue._type == Types.Object && !(_baseValue is EnvironmentRecord);
        }

        internal Reference Reassign(JsValue baseValue, string name, bool strict)
        {
            _baseValue = baseValue;
            _name = name;
            _strict = strict;

            return this;
        }

        internal void AssertValid(Engine engine)
        {
            if (_strict && (_name == "eval" || _name == "arguments") && _baseValue is EnvironmentRecord)
            {
                ExceptionHelper.ThrowSyntaxError(engine);
            }
        }
    }
}
