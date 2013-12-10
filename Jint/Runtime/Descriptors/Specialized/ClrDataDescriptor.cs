using System;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class ClrDataDescriptor : PropertyDescriptor
    {
        public ClrDataDescriptor(Engine engine, Func<JsValue, JsValue[], JsValue> func)
            : base(value: new ClrFunctionInstance(engine, func), writable: null, enumerable: null, configurable: null)
        {
        }
    }
}
