using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime.Descriptors.Specialized;

/// <summary>
/// Data descriptor for a built-in function slot that survives a shaped host's deopt to
/// dictionary mode without forcing the dispatcher function into existence: the function
/// materializes on first value read, exactly as it would have through
/// <see cref="ObjectInstance"/>'s builtin-shape slot materialization. Shared between an
/// alias slot and its target at deopt time so both names keep one function identity.
/// </summary>
internal sealed class LazyBuiltinSlotDescriptor : PropertyDescriptor
{
    private readonly IBuiltinShaped _shaped;
    private readonly ushort _functionSlot;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LazyBuiltinSlotDescriptor(IBuiltinShaped shaped, ushort functionSlot, PropertyFlag flags)
        : base(null, flags | PropertyFlag.CustomJsValue)
    {
        _flags &= ~PropertyFlag.NonData;
        _shaped = shaped;
        _functionSlot = functionSlot;
    }

    protected internal override JsValue? CustomValue
    {
        get
        {
            var value = _value;
            if (value is null)
            {
                _value = value = _shaped.MakeBuiltinFunction(_functionSlot);
                // Once materialized this is semantically a plain data descriptor; clearing the
                // flag lets value reads/writes skip the CustomValue indirection and admits the
                // descriptor to the global-binding and member-write inline caches.
                _flags &= ~PropertyFlag.CustomJsValue;
            }
            return value;
        }
        set => _value = value;
    }
}
