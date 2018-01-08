using System;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class ClrAccessDescriptor : IPropertyDescriptor
    {
        public ClrAccessDescriptor(Engine engine, Func<JsValue, JsValue> get, Action<JsValue, JsValue> set = null)
        {
            Get = new GetterFunctionInstance(engine, get);
            Set = set == null ? Undefined.Instance : new SetterFunctionInstance(engine, set);
        }

        public JsValue Get { get; }
        public JsValue Set { get; }

        public bool? Enumerable => null;
        public bool? Writable => null;
        public bool? Configurable { get; set; }

        public JsValue Value { get; set; }
    }
}
