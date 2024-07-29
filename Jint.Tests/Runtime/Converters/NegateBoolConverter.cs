using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Converters;

public class NegateBoolConverter : IObjectConverter
{
    public bool TryConvert(Engine engine, object value, out JsValue result)
    {
        if (value is bool b)
        {
            result = !b;
            return true;
        }

        result = JsValue.Null;
        return false;
    }
}