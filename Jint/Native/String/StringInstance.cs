using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.String
{
    public class StringInstance : ObjectInstance, IPrimitiveInstance
    {
        public StringInstance(Engine engine)
            : base(engine)
        {
        }

        public override string Class => "String";

        Types IPrimitiveInstance.Type => Types.String;

        JsValue IPrimitiveInstance.PrimitiveValue => PrimitiveValue;

        public JsValue PrimitiveValue { get; set; }

        private static bool IsInt(double d)
        {
            if (d >= long.MinValue && d <= long.MaxValue)
            {
                var l = (long) d;
                return l >= int.MinValue && l <= int.MaxValue;
            }

            return false;
        }

        public override IPropertyDescriptor GetOwnProperty(string propertyName)
        {
            if (propertyName == "Infinity")
            {
                return PropertyDescriptor.Undefined;
            }

            var desc = base.GetOwnProperty(propertyName);
            if (desc != PropertyDescriptor.Undefined)
            {
                return desc;
            }

            var integer = TypeConverter.ToInteger(propertyName);
            if (integer == 0 && propertyName != "0" || propertyName != System.Math.Abs(integer).ToString())
            {
                return PropertyDescriptor.Undefined;
            }

            var str = PrimitiveValue;
            var dIndex = integer;
            if (!IsInt(dIndex))
                return PropertyDescriptor.Undefined;

            var index = (int) dIndex;
            var len = str.AsString().Length;
            if (len <= index || index < 0)
            {
                return PropertyDescriptor.Undefined;
            }

            var resultStr = TypeConverter.ToString(str.AsString()[index]);
            return new EnumerablePropertyDescriptor(resultStr);
        }
    }
}