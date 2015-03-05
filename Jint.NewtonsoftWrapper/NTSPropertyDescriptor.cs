using Jint.Native;
using Jint.Native.Object;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Runtime.Descriptors
{
    public class NTSPropertyDescriptor : PropertyDescriptor
    {
        private Engine engine;
        private Newtonsoft.Json.Linq.JProperty prop;
        private NTSObjectInstance parent;
        private Native.JsValue? convertedValue = null;

        public NTSPropertyDescriptor(Engine engine, NTSObjectInstance parent, Newtonsoft.Json.Linq.JProperty prop)
        {
            this.engine = engine;
            this.parent = parent;
            this.prop = prop;
            Writable = JsValue.True;
            Configurable = JsValue.True;
            Enumerable = JsValue.True;
        }

        public override Native.JsValue? Value
        {
            get
            {
                if (convertedValue == null && prop.Value != null)
                    convertedValue = parent.Convert(prop.Value);
                return convertedValue;
            }
            set
            {
                convertedValue = value;
                if (!value.HasValue)
                    prop.Value = null;
                else
                    prop.Value = parent.ConvertBack(prop.Value.Type, value.Value);
            }
        }
    }
}
