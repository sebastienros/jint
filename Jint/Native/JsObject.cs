using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// Dynamically constructed JavaScript object instance.
/// </summary>
public sealed class JsObject : ObjectInstance
{
    // Hidden-class shape storage lives here (not on base ObjectInstance) so only plain objects — the
    // population that benefits from shapes — carry these fields; JsDate/JsArray/TypedArray/wrappers/
    // built-ins keep their (smaller) size. In-object properties: a small shaped object keeps its shape
    // reference and its first InlineCapacity slot values in-object (no separate array), so it allocates a
    // SINGLE object; only an object whose layout exceeds InlineCapacity spills the surplus into _overflow.
    // Null _shape = dictionary mode (mutually exclusive with the base _properties for string keys).
    // Reached only via `this is JsObject` / the ShapeMode type flag.
    private Shape? _shape;
    private InlineSlots _inline;
    private JsValue[]? _overflow;

    // Slots [0, InlineCapacity) live in-object; the rest (rare) in _overflow[slot - InlineCapacity].
    private const int InlineCapacity = 4;

    public JsObject(Engine engine) : base(engine, type: InternalTypes.Object | InternalTypes.PlainObject)
    {
        // A dynamically constructed object has no lazy intrinsic members to populate (Initialize() is the
        // base no-op), so mark it initialized up front. This skips the per-object virtual Initialize() call
        // that EnsureInitialized() would otherwise make on first property access, and lets value-creating
        // fast paths (e.g. CreateDataProperty) run without that overhead.
        _initialized = true;
    }

    /// <summary>The hidden-class shape. Only valid when in shape mode.</summary>
    internal Shape ShapeOf
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _shape!;
    }

    /// <summary>
    /// Installs <paramref name="shape"/> and prepares slot storage — allocating the overflow array only
    /// when the layout exceeds the in-object capacity — then flips on shape mode. The caller fills the
    /// slots via <see cref="SetSlot"/>, so a small object allocates no slot array at all.
    /// </summary>
    internal void InitShape(Shape shape)
    {
        _shape = shape;
        var slotCount = shape.SlotCount;
        if (slotCount > InlineCapacity)
        {
            _overflow = new JsValue[slotCount - InlineCapacity];
        }
        _type |= InternalTypes.ShapeMode;
        unchecked { _propertiesVersion++; }
    }

    // Slot accessors used by the hot shape paths. The slot is always proven in range by the caller (it
    // comes from this object's Shape). Slots below InlineCapacity are read straight from the in-object
    // fields (no array indirection); the rest from the overflow array.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue GetSlot(int slot)
    {
        if (slot < InlineCapacity)
        {
            return _inline[slot]!;
        }
        return _overflow![slot - InlineCapacity];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetSlot(int slot, JsValue value)
    {
        if (slot < InlineCapacity)
        {
            _inline[slot] = value;
        }
        else
        {
            _overflow![slot - InlineCapacity] = value;
        }
    }

    /// <summary>Drops shape mode back to dictionary mode's starting state (clears the shape and slot
    /// storage and the <see cref="InternalTypes.ShapeMode"/> flag). Callers must populate <c>_properties</c>.</summary>
    internal void ClearShape()
    {
        _shape = null;
        _overflow = null;
        _inline = default; // release in-object slot references so they don't outlive shape mode
        _type &= ~(InternalTypes.ShapeMode | InternalTypes.ShapeBuilding);
    }

    /// <summary>
    /// Starts incremental shape mode for a still-empty object (a constructor's <c>this</c>): installs the
    /// prototype's empty root shape with no slot storage, so subsequent <c>this.x=</c> adds transition the
    /// shape (interned, shared across <c>new T()</c> instances) and fill an in-object slot — a constructor
    /// whose instance stays within InlineCapacity properties allocates no slot array.
    /// </summary>
    internal void StartShapeBuilding(Shape emptyRoot)
    {
        _shape = emptyRoot;
        _type |= InternalTypes.ShapeMode | InternalTypes.ShapeBuilding;
        unchecked { _propertiesVersion++; }
    }

    /// <summary>
    /// Adds a brand-new configurable+enumerable+writable data property to a shape-mode object by
    /// transitioning to the interned child shape and storing the value in the next slot (in-object, or
    /// growing the overflow array). Returns <c>false</c> — leaving the object untouched — when a megamorphic
    /// guard trips (too many own properties, or this shape already has too many distinct child transitions),
    /// so the caller deopts to the dictionary representation. The key must be known-absent (callers check).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryShapeAdd(in Key key, JsValue value) => TryShapeAdd(key, value, out _);

    /// <summary>
    /// Same as <see cref="TryShapeAdd(in Key, JsValue)"/>, additionally reporting whether the shape
    /// transition was newly interned rather than reused (see <see cref="Shape.Add(in Key, out bool)"/>)
    /// so callers can bound transition-tree growth.
    /// </summary>
    internal bool TryShapeAdd(in Key key, JsValue value, out bool created)
    {
        var shape = _shape!;
        if (shape.SlotCount >= Shape.MaxShapeProperties || shape.TransitionCount >= Shape.MaxFanout)
        {
            created = false;
            return false;
        }

        var slot = shape.SlotCount; // the new value occupies slot index == current SlotCount
        if (slot < InlineCapacity)
        {
            _inline[slot] = value;
        }
        else
        {
            var overflowIndex = slot - InlineCapacity;
            if (_overflow is null)
            {
                _overflow = new JsValue[4];
            }
            else if (overflowIndex >= _overflow.Length)
            {
                System.Array.Resize(ref _overflow, _overflow.Length * 2);
            }
            _overflow[overflowIndex] = value;
        }

        _shape = shape.Add(key, out created);
        unchecked { _propertiesVersion++; }
        return true;
    }

    // The in-object slot block. On modern runtimes this is a single-field [InlineArray] so `_inline[i]`
    // lowers to a bounds-check-free field-offset access; on legacy TFMs it is an equivalent hand-rolled
    // fixed struct. InlineCapacity must equal the element count.
#if NET8_0_OR_GREATER
    [System.Runtime.CompilerServices.InlineArray(InlineCapacity)]
    private struct InlineSlots
    {
        private JsValue? _slot0;
    }
#else
    private struct InlineSlots
    {
        private JsValue? _slot0;
        private JsValue? _slot1;
        private JsValue? _slot2;
        private JsValue? _slot3;

        public JsValue? this[int index]
        {
            get => index switch { 0 => _slot0, 1 => _slot1, 2 => _slot2, _ => _slot3 };
            set
            {
                switch (index)
                {
                    case 0: _slot0 = value; break;
                    case 1: _slot1 = value; break;
                    case 2: _slot2 = value; break;
                    default: _slot3 = value; break;
                }
            }
        }
    }
#endif
}
