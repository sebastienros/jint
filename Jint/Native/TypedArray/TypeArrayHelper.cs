using Jint.Native.ArrayBuffer;
using Jint.Runtime;

namespace Jint.Native.TypedArray;

internal static class TypeArrayHelper
{
    internal static IntrinsicTypedArrayPrototype.TypedArrayWithBufferWitnessRecord ValidateTypedArray(this JsValue o, Realm realm, ArrayBufferOrder order = ArrayBufferOrder.Unordered)
    {
        if (o is not JsTypedArray typedArray)
        {
            Throw.TypeError(realm);
            return default;
        }

        var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(typedArray, order);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            Throw.TypeError(realm, "TypedArray is out of bounds");
        }

        return taRecord;
    }
}
