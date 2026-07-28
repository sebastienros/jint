using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// Lazy layout slots: the storage side of <c>JsObjectLayout.Builder.AddLazy</c>.
/// </summary>
public sealed partial class JsObject
{
    /// <summary>
    /// Reads a slot the way every path that observes a slot's <em>value</em> must: materializing it first if
    /// it is still a lazy layout slot whose factory has not run. Callers that only need keys, flags or slot
    /// counts must keep using the raw accessors — an existence question never runs a factory.
    /// <para>
    /// The check is a single type test against a value the caller has already loaded, and the branch is
    /// perfectly predicted (a given slot is a sentinel at most once in its life). The hot interpreter lanes
    /// avoid even that by gating on <see cref="InternalTypes.HasLazySlots"/> and routing here only for the
    /// objects that can actually have one.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue GetSlotForRead(int slot)
    {
        var value = GetSlot(slot);
        if (value is UnmaterializedSlots sentinel)
        {
            return MaterializeSlot(sentinel, slot);
        }

        return value;
    }

    /// <summary>
    /// Runs one lazy slot's factory and memoizes the result into the slot, so the slot itself is the memo and
    /// every read lane sees the value immediately (nothing anywhere caches a slot's value — the inline caches
    /// cache the slot <em>index</em> and re-read the slot on every hit).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue MaterializeSlot(UnmaterializedSlots sentinel, int slot)
    {
        var factory = sentinel.Layout.GetFactory(slot);
        System.Diagnostics.Debug.Assert(factory is not null, "a slot holding the sentinel must have a factory");
        var value = factory!(this, sentinel.State) ?? Undefined;

        // A factory is morally a getter body, so it may have mutated this very object while it ran: written
        // the property being read, deleted an unrelated one (deopting to a dictionary), or frozen the object.
        // Memoize only while the slot is still the one this read started from; otherwise the mutation stands
        // and this read merely returns the factory's value, which is exactly how a JavaScript getter that
        // mutates its own object behaves.
        if ((_type & InternalTypes.ShapeMode) != InternalTypes.Empty
            && slot < _shape!.SlotCount
            && ReferenceEquals(GetSlot(slot), sentinel))
        {
            SetSlot(slot, value);
            ClearLazyFlagIfFullyMaterialized();
        }

        return value;
    }

    /// <summary>
    /// Drops <see cref="InternalTypes.HasLazySlots"/> once no slot holds a sentinel any more, returning the
    /// object to the plain shape read lanes — from then on it is indistinguishable from an object literal of
    /// the same layout. O(slot count), bounded by the maximum a hidden class can describe, and paid only
    /// on the materializations themselves.
    /// </summary>
    /// <seealso cref="Shape.MaxShapeProperties"/>
    private void ClearLazyFlagIfFullyMaterialized()
    {
        var slotCount = _shape!.SlotCount;
        for (var i = 0; i < slotCount; i++)
        {
            if (GetSlot(i) is UnmaterializedSlots)
            {
                return;
            }
        }

        _type &= ~InternalTypes.HasLazySlots;
    }

    /// <summary>
    /// Installs one shared sentinel in every lazy slot of <paramref name="layout"/> and flags the object, so
    /// the read lanes know to check. One allocation per object regardless of how many lazy slots it has.
    /// </summary>
    internal void InstallLazySlots(JsObjectLayout layout, object? lazySlotState)
    {
        var sentinel = new UnmaterializedSlots(layout, lazySlotState);
        var count = layout.Count;
        for (var i = 0; i < count; i++)
        {
            if (layout.GetFactory(i) is not null)
            {
                SetSlot(i, sentinel);
            }
        }

        _type |= InternalTypes.HasLazySlots;
    }

    /// <summary>
    /// The placeholder a lazy layout slot holds until its factory runs, carrying everything that run needs:
    /// the layout (which owns the per-slot factories) and the per-object state. One instance is shared by all
    /// of an object's lazy slots, so an object with four lazy members still costs one extra allocation.
    /// <para>
    /// Never observable: it is <see cref="Types.Empty"/>, like <see cref="JsEmpty"/>, and every path that
    /// observes a slot's value goes through <see cref="GetSlotForRead"/>, the descriptor view
    /// (<c>SlotPropertyDescriptor</c>) or the dictionary deopt, each of which resolves it first.
    /// </para>
    /// </summary>
    internal sealed class UnmaterializedSlots : JsValue
    {
        internal readonly JsObjectLayout Layout;
        internal readonly object? State;

        internal UnmaterializedSlots(JsObjectLayout layout, object? state) : base(Types.Empty)
        {
            Layout = layout;
            State = state;
        }

        public override object? ToObject() => null;

        public override string ToString() => "[unmaterialized lazy slot]";
    }
}
