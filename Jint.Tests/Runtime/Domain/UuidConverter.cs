using Jint.Native;
using Jint.Runtime.Interop;
using System;

namespace Jint.Tests.Runtime.Domain
{
    public class UuidConverter : IObjectConverter
    {
        internal UuidConverter()
        {
        }

        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            switch (value)
            {
                case Guid g:
                    result = new JsUuid(g);
                    return true;
            }

            result = JsValue.Undefined;
            return false;
        }
    }
}
