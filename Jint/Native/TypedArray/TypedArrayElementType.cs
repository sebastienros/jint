using Jint.Runtime;

namespace Jint.Native.TypedArray;

internal enum TypedArrayElementType : byte
{
    // we have signed first to make comparison vaster to check if signed or unsigned type
    Int8,
    Int16,
    Int32,

    BigInt64,

    Float16,
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
    internal static byte GetElementSize(this TypedArrayElementType type)
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
            TypedArrayElementType.Float16 => 2,
            TypedArrayElementType.Float32 => 4,
            TypedArrayElementType.Float64 => 8,
            _ => 0
        };
    }

    internal static string GetTypedArrayName(this TypedArrayElementType type)
    {
        return type switch
        {
            TypedArrayElementType.Int8 => "Int8Array",
            TypedArrayElementType.Uint8 => "Uint8Array",
            TypedArrayElementType.Uint8C => "Uint8ClampedArray",
            TypedArrayElementType.Int16 => "Int16Array",
            TypedArrayElementType.Uint16 => "Uint16Array",
            TypedArrayElementType.Int32 => "Int32Array",
            TypedArrayElementType.Uint32 => "Uint32Array",
            TypedArrayElementType.BigInt64 => "BigInt64Array",
            TypedArrayElementType.BigUint64 => "BigUint64Array",
            TypedArrayElementType.Float16 => "Float16Array",
            TypedArrayElementType.Float32 => "Float32Array",
            TypedArrayElementType.Float64 => "Float64Array",
            _ => ""
        };
    }

    internal static IConstructor GetConstructor(this TypedArrayElementType type, Intrinsics intrinsics)
    {
        return type switch
        {
            TypedArrayElementType.Int8 => intrinsics.Int8Array,
            TypedArrayElementType.Uint8 => intrinsics.Uint8Array,
            TypedArrayElementType.Uint8C => intrinsics.Uint8ClampedArray,
            TypedArrayElementType.Int16 => intrinsics.Int16Array,
            TypedArrayElementType.Uint16 => intrinsics.Uint16Array,
            TypedArrayElementType.Int32 => intrinsics.Int32Array,
            TypedArrayElementType.Uint32 => intrinsics.Uint32Array,
            TypedArrayElementType.BigInt64 => intrinsics.BigInt64Array,
            TypedArrayElementType.BigUint64 => intrinsics.BigUint64Array,
            TypedArrayElementType.Float16 => intrinsics.Float16Array,
            TypedArrayElementType.Float32 => intrinsics.Float32Array,
            TypedArrayElementType.Float64 => intrinsics.Float64Array,
            _ => null!
        };
    }

    internal static bool IsUnsignedElementType(this TypedArrayElementType type)
    {
        return type > TypedArrayElementType.Float64;
    }

    internal static bool FitsInt32(this TypedArrayElementType type)
    {
        return type <= TypedArrayElementType.Int32;
    }

    internal static bool IsBigIntElementType(this TypedArrayElementType type)
    {
        return type is TypedArrayElementType.BigUint64 or TypedArrayElementType.BigInt64;
    }
}
