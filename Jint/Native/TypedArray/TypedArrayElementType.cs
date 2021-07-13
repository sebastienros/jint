namespace Jint.Native.TypedArray
{
    internal enum TypedArrayElementType
    {
        // we have signed first to make comparison vaster to check if signed or unsigned type
        Int8,
        Int16,
        Int32,
        BigInt64,
        Float32,
        Float64,

        Uint8,
        Uint8C,
        Uint16,
        Uint32,
        BigUint64
    }

    internal static class TypedArrayExtensions
    {
        internal static int GetElementSize(this TypedArrayElementType type)
        {
            return type switch
            {
                TypedArrayElementType.Int8 => 1,
                TypedArrayElementType.Uint8 => 1,
                TypedArrayElementType.Uint8C => 1,
                TypedArrayElementType.Int16 => 2,
                TypedArrayElementType.Uint16 => 2,
                TypedArrayElementType.Int32 => 4,
                TypedArrayElementType.Uint32 => 4,
                TypedArrayElementType.BigInt64 => 8,
                TypedArrayElementType.BigUint64 => 8,
                TypedArrayElementType.Float32 => 4,
                TypedArrayElementType.Float64 => 8,
                _ => -1
            };
        }

        internal static bool IsUnsignedElementType(this TypedArrayElementType type)
        {
            return type > TypedArrayElementType.Float64;
        }

        internal static bool IsBigIntElementType(this TypedArrayElementType type)
        {
            return type is TypedArrayElementType.BigUint64 or TypedArrayElementType.BigInt64;
        }
    }
}