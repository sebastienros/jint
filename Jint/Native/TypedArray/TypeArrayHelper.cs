using Jint.Runtime;

namespace Jint.Native.TypedArray;

internal static class TypeArrayHelper
{
    internal static JsTypedArray ValidateTypedArray(this JsValue o, Realm realm)
    {
        var typedArrayInstance = o as JsTypedArray;
        if (typedArrayInstance is null)
        {
            ExceptionHelper.ThrowTypeError(realm);
        }

        var buffer = typedArrayInstance._viewedArrayBuffer;
        buffer.AssertNotDetached();

        return typedArrayInstance;
    }
}
