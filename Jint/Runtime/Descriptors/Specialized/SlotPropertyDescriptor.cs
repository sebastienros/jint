using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized;

/// <summary>
/// A flyweight <see cref="PropertyDescriptor"/> that reads and writes a shape-mode object's slot live,
/// so the slow paths that expect a <see cref="PropertyDescriptor"/> (GetOwnProperty, enumeration,
/// Object.getOwnPropertyDescriptor, JSON, spread, ...) keep working without the object materializing a
/// real descriptor per property. It is allocated only on those slow paths; the hot read/write paths
/// index the slot array directly.
/// <para>
/// Every shape-mode property is configurable + enumerable + writable data (v1), so the flags are fixed
/// and the value is reported through the existing <see cref="PropertyDescriptor.CustomValue"/>
/// indirection (the same mechanism LazyPropertyDescriptor uses).
/// </para>
/// </summary>
internal sealed class SlotPropertyDescriptor : PropertyDescriptor
{
    private readonly JsObject _owner;
    private readonly int _slot;

    internal SlotPropertyDescriptor(JsObject owner, int slot)
        : base(PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.CustomJsValue)
    {
        _owner = owner;
        _slot = slot;
    }

    protected internal override JsValue? CustomValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner.GetSlot(_slot);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _owner.SetSlot(_slot, value!);
    }
}
