using System;
using Jint.Native;
using Jint.Native.Object;

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

        public Reference(object baseValue, string name, bool strict)
        {
            _baseValue = baseValue;
            _name = name;
            _strict = strict;
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
            var type = TypeConverter.GetType(_baseValue);

            return (type == TypeCode.Boolean)
                || (type == TypeCode.String)
                || (type == TypeCode.Double)
                ;
        }

        public bool IsUnresolvableReference()
        {
            return _baseValue == Undefined.Instance || _baseValue == Null.Instance;
        }

        public bool IsPropertyReference()
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-8.7
            return _baseValue is ObjectInstance || HasPrimitiveBase();
        }
    }
}
