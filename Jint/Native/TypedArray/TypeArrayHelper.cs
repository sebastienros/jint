using Jint.Runtime;

namespace Jint.Native.TypedArray
{
    internal static class TypeArrayHelper
    {
        internal static TypedArrayInstance ValidateTypedArray(this JsValue o, Realm realm)
        {
            var typedArrayInstance = o as TypedArrayInstance;
            if (typedArrayInstance is null)
            {
                ExceptionHelper.ThrowTypeError(realm);
            }

            var buffer = typedArrayInstance._viewedArrayBuffer;
            buffer.AssertNotDetached();

            return typedArrayInstance;
        }
    }
}