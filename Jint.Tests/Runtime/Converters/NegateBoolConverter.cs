using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Converters
{
    public class NegateBoolConverter : IObjectConverter
    {
        public bool TryConvert(object value, out JsValue result)
        {
            if (value is bool)
            {
                result = !(bool) value;
                return true;
            }

            result = JsValue.Null;
            return false;
        }
    }
}
