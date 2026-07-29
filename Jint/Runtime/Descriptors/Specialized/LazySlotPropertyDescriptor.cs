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

    // Configurable + enumerable + writable, with the three "attribute present" bits spelled out. Semantically
    // identical to PropertyFlag.ConfigurableEnumerableWritable — every ConfigurableSet/EnumerableSet/
    // WritableSet getter already reports true when the corresponding attribute bit is on — but it matters to
    // one raw-bit test: ValidateAndApplyPropertyDescriptor's "every field absent" fast-out inspects those
    // three bits directly and, when they are all clear, goes on to read the descriptor's VALUE, which for a
    // lazy descriptor is the one thing that must not happen. Object.freeze and Object.seal redefine every own
    // key attribute-only, so without this a freeze would run every lazy factory on the object.
    private const PropertyFlag LazySlotFlags = PropertyFlag.ConfigurableEnumerableWritable
                                               | PropertyFlag.ConfigurableSet
                                               | PropertyFlag.EnumerableSet
                                               | PropertyFlag.WritableSet
                                               | PropertyFlag.CustomJsValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LazySlotPropertyDescriptor(JsObject owner, JsObject.UnmaterializedSlots sentinel, int slot)
        : base(null, LazySlotFlags)
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
            }

            if (value is not null)
            {
                // Once materialized this is semantically a plain data descriptor; clearing the
                // flag lets value reads/writes skip the CustomValue indirection and admits the
                // descriptor to the global-binding and member-write inline caches. Reached with a
                // value already stored when something wrote the inherited field directly —
                // ObjectInstance.Set's dictionary fast path does exactly that — which is just as
                // materialized.
                _flags &= ~PropertyFlag.CustomJsValue;
            }

            return value;
        }
        set
        {
            _value = value;
            if (value is not null)
            {
                // A write materializes too: the factory can never run after this, since the getter
                // above now finds a stored value.
                _flags &= ~PropertyFlag.CustomJsValue;
            }
        }
    }
}
