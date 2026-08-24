using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Converters;

public class NegateBoolConverter : ObjectConverter
{
    public override bool TryConvert(Engine engine, object value, out JsValue result)
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