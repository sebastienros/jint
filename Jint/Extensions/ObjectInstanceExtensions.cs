using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors.Specialized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jint.Extensions
{
    /// <summary>
    /// Extensions for reinstating manual property creation on objects.
    /// </summary>
    public static class ObjectInstanceExtensions
    {
        public static void AddDataProperty(this ObjectInstance obj, JsValue name, JsValue value, bool writable, bool enumerable, bool configurable)
        {
            obj.FastAddProperty(name, value, writable, enumerable, configurable);
        }

        public static void AddAccessorProperty(this ObjectInstance obj, JsValue name, JsValue get, JsValue set, bool enumerable, bool configurable)
        {
            obj.SetOwnProperty(name, new GetSetPropertyDescriptor(get, set, enumerable, configurable));
        }
    }
}
