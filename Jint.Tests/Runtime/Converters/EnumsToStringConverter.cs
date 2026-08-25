using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Converters;

public class EnumsToStringConverter : ObjectConverter
{
    public override bool TryConvert(Engine engine, object value, out JsValue result)
    {
        if (value is Enum)
        {
            result = value.ToString();
            return true;
        }

        result = JsValue.Null;
        return false;
    }
}