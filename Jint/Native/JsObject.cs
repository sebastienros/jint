using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// Dynamically constructed JavaScript object instance.
/// </summary>
public sealed class JsObject : ObjectInstance
{
    // Hidden-class shape storage lives here (not on base ObjectInstance) so only plain objects — the
    // population that benefits from shapes — carry this reference; JsDate/JsArray/TypedArray/wrappers/
    // built-ins keep their (smaller) size. A single array holds the shared Shape at index [0] and the
    // per-instance slot values at [1..], so an unshaped JsObject pays only one extra reference (+8 B)
    // rather than two, and a shaped object is allocation-neutral vs. separate shape+slots fields (the
    // object saves a field, the array grows one element). Null = dictionary mode (mutually exclusive
    // with the base _properties for string keys). Reached only via `this is JsObject` / the ShapeMode
    // type flag.
    private object[]? _store;

    public JsObject(Engine engine) : base(engine, type: InternalTypes.Object | InternalTypes.PlainObject)
    {
        // A dynamically constructed object has no lazy intrinsic members to populate (Initialize() is the
        // base no-op), so mark it initialized up front. This skips the per-object virtual Initialize() call
        // that EnsureInitialized() would otherwise make on first property access, and lets value-creating
        // fast paths (e.g. CreateDataProperty) run without that overhead.
        _initialized = true;
    }

    /// <summary>The hidden-class shape (stored at <c>_store[0]</c>). Only valid when in shape mode.</summary>
    internal Shape ShapeOf
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Unsafe.As<Shape>(_store![0]);
    }

    /// <summary>
    /// Installs the combined shape+slots store (shape at [0], slot values at [1..]) and flips on shape
    /// mode. The store is built by the caller so a shape-mode object allocates exactly one array.
    /// </summary>
    internal void SetStore(object[] store)
    {
        _store = store;
        _type |= InternalTypes.ShapeMode;
        unchecked { _propertiesVersion++; }
    }

    // Slot accessors used by the hot shape paths. The slot is always proven in range by the caller (it
    // comes from this object's Shape, and the store is sized to exactly Shape.SlotCount + 1), so the
    // bounds check is redundant and elided via GetArrayDataReference on runtimes that expose it. Values
    // live at [slot + 1] (index 0 is the shape). Only ever called when ShapeMode is set (=> _store != null).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue GetSlot(int slot)
    {
#if NET6_0_OR_GREATER
        return Unsafe.As<JsValue>(Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_store!), slot + 1));
#else
        return Unsafe.As<JsValue>(_store![slot + 1]);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetSlot(int slot, JsValue value)
    {
#if NET6_0_OR_GREATER
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_store!), slot + 1) = value;
#else
        _store![slot + 1] = value;
#endif
    }

    /// <summary>Drops shape mode back to dictionary mode's starting state (clears the store and the
    /// <see cref="InternalTypes.ShapeMode"/> flag). Callers must populate <c>_properties</c>.</summary>
    internal void ClearShape()
    {
        _store = null;
        _type &= ~(InternalTypes.ShapeMode | InternalTypes.ShapeBuilding);
    }

    // store[0] is the shape; values start at [1], so a fresh builder has room for 3 properties before regrow.
    private const int InitialBuildCapacity = 4;

    /// <summary>
    /// Starts incremental shape mode for a still-empty object (a constructor's <c>this</c>): installs the
    /// prototype's empty root shape and a small growable store, so subsequent <c>this.x=</c> adds transition
    /// the shape (interned, shared across <c>new T()</c> instances) instead of building a dictionary.
    /// </summary>
    internal void StartShapeBuilding(Shape emptyRoot)
    {
        var store = new object[InitialBuildCapacity];
        store[0] = emptyRoot;
        _store = store;
        _type |= InternalTypes.ShapeMode | InternalTypes.ShapeBuilding;
        unchecked { _propertiesVersion++; }
    }

    /// <summary>
    /// Adds a brand-new configurable+enumerable+writable data property to a shape-mode object by
    /// transitioning to the interned child shape and growing the store. Returns <c>false</c> — leaving the
    /// object untouched — when a megamorphic guard trips (too many own properties, or this shape already
    /// has too many distinct child transitions), so the caller deopts to the dictionary representation. The
    /// key must be known-absent (callers check).
    /// </summary>
    internal bool TryShapeAdd(in Key key, JsValue value)
    {
        var store = _store!;
        var shape = Unsafe.As<Shape>(store[0]);
        if (shape.SlotCount >= Shape.MaxShapeProperties || shape.TransitionCount >= Shape.MaxFanout)
        {
            return false;
        }

        var valueIndex = shape.SlotCount + 1; // store[0] is the shape; the new value goes at SlotCount + 1
        if (store.Length <= valueIndex)
        {
            System.Array.Resize(ref store, store.Length * 2);
            _store = store;
        }

        store[valueIndex] = value;
        store[0] = shape.Add(key);
        unchecked { _propertiesVersion++; }
        return true;
    }
}
