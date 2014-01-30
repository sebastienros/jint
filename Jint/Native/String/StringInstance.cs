using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.String
{
    public class StringInstance : ObjectInstance, IPrimitiveInstance
    {
        public StringInstance(Engine engine)
            : base(engine)
        {
        }

        public override string Class
        {
            get
            {
                return "String";
            }
        }

        Types IPrimitiveInstance.Type
        {
            get { return Types.String; }
        }

        JsValue IPrimitiveInstance.PrimitiveValue
        {
            get { return PrimitiveValue; }
        }

        public JsValue PrimitiveValue { get; set; }

        private static bool IsInt(double d)
        {
            if (d >= long.MinValue && d <= long.MaxValue)
            {
                var l = (long)d;
                return l >= int.MinValue && l <= int.MaxValue;
            }
            else 
                return false;
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            if(propertyName == "Infinity")
                return PropertyDescriptor.Undefined;

            var desc = base.GetOwnProperty(propertyName);
            if (desc != PropertyDescriptor.Undefined)
            {
                return desc;
            }

            if (propertyName != System.Math.Abs(TypeConverter.ToInteger(propertyName)).ToString())
            {
                return PropertyDescriptor.Undefined;
            }

            var str = PrimitiveValue;
            var dIndex = TypeConverter.ToInteger(propertyName);
            if(!IsInt(dIndex))
                return PropertyDescriptor.Undefined;

            var index = (int)dIndex;
            var len = str.AsString().Length;
            if (len <= index || index < 0)
            {
                return PropertyDescriptor.Undefined;
            }
            var resultStr = str.AsString()[index].ToString();
            return new PropertyDescriptor(new JsValue(resultStr), false, true, false);
        }
    }
}
