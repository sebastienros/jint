using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized;

/// <summary>
/// Data descriptor that carries a still-unmaterialized lazy layout slot through a shaped object's deopt to
/// dictionary mode, so laziness survives the deopt: deleting one key, redefining an unrelated one or freezing
/// the object must not force every lazy member's factory to run. The factory runs on the first read of
/// <em>this</em> property, exactly as it would have through the slot.
/// <para>
/// Follows the <see cref="LazyBuiltinSlotDescriptor"/> memo pattern exactly — resolve into the inherited
/// <c>_value</c>, then clear <see cref="PropertyFlag.CustomJsValue"/> — which is what
/// <see cref="IFieldBackedLazyDescriptor"/> claims and what lets a global snapshot capture and restore this
/// descriptor (including back to unmaterialized) by writing those two fields.
/// </para>
/// </summary>
internal sealed class LazySlotPropertyDescriptor : PropertyDescriptor, IFieldBackedLazyDescriptor
{
    private readonly JsObject _owner;
    private readonly JsObject.UnmaterializedSlots _sentinel;
    private readonly int _slot;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LazySlotPropertyDescriptor(JsObject owner, JsObject.UnmaterializedSlots sentinel, int slot)
        : base(null, PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.CustomJsValue)
    {
        _flags &= ~PropertyFlag.NonData;
        _owner = owner;
        _sentinel = sentinel;
        _slot = slot;
    }

    protected internal override JsValue? CustomValue
    {
        get
        {
            var value = _value;
            if (value is null)
            {
                _value = value = _sentinel.Layout.GetFactory(_slot)!(_owner, _sentinel.State) ?? JsValue.Undefined;
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
