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

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
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
            var index = (int)TypeConverter.ToInteger(propertyName);
            var len = str.AsString().Length;
            if (len <= index)
            {
                return PropertyDescriptor.Undefined;
            }
            var resultStr = str.AsString()[index].ToString();
            return new PropertyDescriptor(new JsValue(resultStr), false, true, false);
        }
    }
}
