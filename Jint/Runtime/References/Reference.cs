using Jint.Native;
using Jint.Native.Errors;
using Jint.Native.Object;
using Jint.Runtime.Environments;

namespace Jint.Runtime.References
{
    /// <summary>
    /// Represents the Reference Specification Type
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.7
    /// </summary>
    public class Reference
    {

        private readonly object _baseValue;
        private readonly string _name;
        private readonly bool _strict;
        private readonly EnvironmentRecord _record;

        public Reference(object baseValue, string name, bool strict)
        {
            _baseValue = baseValue;
            _name = name;
            _strict = strict;

            _record = baseValue as EnvironmentRecord;
        }

        public object GetBase()
        {
            return _baseValue;
        }

        public string GetReferencedName()
        {
            return _name;
        }

        public bool IsStrict()
        {
            return _strict;
        }

        public bool HasPrimitiveBase()
        {
            return false;
                // (_baseValue is BooleanInstance)
                // || (_baseValue is StringInstance)
                // || (_baseValue is NumberInstance)
                ;
        }

        public bool IsUnresolvableReference()
        {
            return _baseValue == Undefined.Instance;
        }

        public bool IsPropertyReference()
        {
            /// todo: complete implementation  http://www.ecma-international.org/ecma-262/5.1/#sec-8.7
            return _baseValue is ObjectInstance || HasPrimitiveBase();
        }
    }
}
