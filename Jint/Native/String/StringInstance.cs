using System.Collections.Generic;
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

        public override bool HasOwnProperty(string propertyName)
        {
            var desc = this.GetOwnProperty(propertyName);
            if (desc != PropertyDescriptor.Undefined)
            {
                return true;
            }

            return false;
        }

        public override IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
        {
            var str = PrimitiveValue.AsString();

            for (var i = 0; i < str.Length; i++)
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(i.ToString(), new PropertyDescriptor(str[i], true, true, false));
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            if (propertyName == "Infinity")
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
            if (!IsInt(dIndex))
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
