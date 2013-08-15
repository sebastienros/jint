using System;
using Jint.Native.Object;

namespace Jint.Native.String
{
    public sealed class StringInstance : ObjectInstance, IPrimitiveType
    {
        public StringInstance(ObjectInstance prototype)
            : base(prototype)
        {
        }

        public override string Class
        {
            get
            {
                return "String";
            }
        }

        TypeCode IPrimitiveType.TypeCode
        {
            get { return TypeCode.Boolean; }
        }

        object IPrimitiveType.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public string PrimitiveValue { get; set; }

        public override Runtime.Descriptors.PropertyDescriptor GetOwnProperty(string propertyName)
        {
            // todo: http://www.ecma-international.org/ecma-262/5.1/#sec-15.5.5.2

            return base.GetOwnProperty(propertyName);
        }
    }
}
