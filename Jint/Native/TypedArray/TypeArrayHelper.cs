using Jint.Native.ArrayBuffer;
using Jint.Runtime;

namespace Jint.Native.TypedArray;

internal static class TypeArrayHelper
{
    internal static JsTypedArray ValidateTypedArray(this JsValue o, Realm realm, ArrayBufferOrder order = ArrayBufferOrder.Unordered)
    {
        if (o is not JsTypedArray typedArray)
        {
            ExceptionHelper.ThrowTypeError(realm);
            return null!;
        }

        var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(typedArray, order);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            ExceptionHelper.ThrowTypeError(realm);
        }

        return typedArray;
    }
}
